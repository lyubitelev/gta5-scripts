using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;

namespace gta.Vehicles
{
    internal sealed class SavedWorldVehicleConfig
    {
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public float Heading { get; set; }
        public VehicleTuningConfig Tuning { get; set; }
    }

    internal sealed class WorldVehicleStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };
        private readonly string _directory;
        private readonly Dictionary<int, string> _activeSavedVehicles = new Dictionary<int, string>();
        private bool _isRestored;

        public WorldVehicleStore()
        {
            _directory = ScriptPaths.SavedVehiclesDirectory;
            Directory.CreateDirectory(_directory);
        }

        public bool IsVehicleSaved(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return false;
            return _activeSavedVehicles.ContainsKey(vehicle.Handle);
        }

        public bool IsVehicleSaved(int handle)
        {
            return _activeSavedVehicles.ContainsKey(handle);
        }

        public void ToggleSaveCurrentVehicle(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                Notifier.Show("Сначала сядьте в транспорт");
                return;
            }

            CleanupInvalidHandles();

            if (_activeSavedVehicles.TryGetValue(vehicle.Handle, out var existingFilePath))
            {
                RemoveVehicle(vehicle, existingFilePath);
            }
            else
            {
                SaveVehicle(vehicle);
            }
        }

        public void RestoreSavedVehiclesOnTick()
        {
            if (_isRestored) return;
            var player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            _isRestored = true;
            LoadAndSpawnSavedVehicles();
        }

        public void Abort()
        {
            foreach (var kvp in _activeSavedVehicles)
            {
                if (Function.Call<bool>(Hash.DOES_ENTITY_EXIST, kvp.Key))
                {
                    var entity = Entity.FromHandle(kvp.Key);
                    if (entity != null && entity.Exists())
                    {
                        entity.MarkAsNoLongerNeeded();
                    }
                }
            }
            _activeSavedVehicles.Clear();
        }

        private void SaveVehicle(Vehicle vehicle)
        {
            var pos = vehicle.Position;
            var heading = vehicle.Heading;
            var tuning = VehicleTuningConfigStore.Capture(vehicle);

            var config = new SavedWorldVehicleConfig
            {
                PositionX = pos.X,
                PositionY = pos.Y,
                PositionZ = pos.Z,
                Heading = heading,
                Tuning = tuning
            };

            var fileName = Guid.NewGuid().ToString("N") + ".json";
            var path = Path.Combine(_directory, fileName);
            File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOptions));

            vehicle.IsPersistent = true;
            _activeSavedVehicles[vehicle.Handle] = path;

            Notifier.Show("Транспорт сохранён в мире");
            ModLogger.Log("WORLD_VEHICLE", $"Vehicle {vehicle.Model.Hash} (Handle={vehicle.Handle}) saved to {path}");
        }

        private void RemoveVehicle(Vehicle vehicle, string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Log("WORLD_VEHICLE", $"Error deleting file {filePath}: {ex.Message}");
            }

            _activeSavedVehicles.Remove(vehicle.Handle);

            if (vehicle.Exists())
            {
                vehicle.MarkAsNoLongerNeeded();
                vehicle.Delete();
            }

            Notifier.Show("Транспорт удалён из мира");
            ModLogger.Log("WORLD_VEHICLE", $"Vehicle (Handle={vehicle.Handle}) deleted from world and file removed");
        }

        private int LoadAndSpawnSavedVehicles()
        {
            if (!Directory.Exists(_directory)) return 0;

            var files = Directory.GetFiles(_directory, "*.json");
            int count = 0;
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var config = JsonSerializer.Deserialize<SavedWorldVehicleConfig>(json);
                    if (config == null || config.Tuning == null) continue;

                    var pos = new Vector3(config.PositionX, config.PositionY, config.PositionZ);
                    ClearNearbyExistingVehicles(pos, 3.5f);

                    var model = new Model(config.Tuning.ModelHash);
                    model.Request(2000);
                    if (!model.IsInCdImage || !model.IsValid) continue;

                    var vehicle = GTA.World.CreateVehicle(model, pos, config.Heading);
                    if (vehicle != null && vehicle.Exists())
                    {
                        vehicle.IsPersistent = true;
                        VehicleTuningConfigStore.Apply(vehicle, config.Tuning);
                        Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, vehicle.Handle);
                        _activeSavedVehicles[vehicle.Handle] = file;
                        count++;
                    }
                    model.MarkAsNoLongerNeeded();
                }
                catch (Exception ex)
                {
                    ModLogger.Log("WORLD_VEHICLE", $"Error loading vehicle from {file}: {ex.Message}");
                }
            }

            if (count > 0)
            {
                Notifier.Show($"Восстановлено машин в мире: {count}");
            }
            ModLogger.Log("WORLD_VEHICLE", $"Restored {count} saved world vehicles");
            return count;
        }

        private static void ClearNearbyExistingVehicles(Vector3 pos, float radius)
        {
            var nearby = GTA.World.GetNearbyVehicles(pos, radius);
            if (nearby == null) return;
            foreach (var vehicle in nearby)
            {
                if (vehicle != null && vehicle.Exists())
                {
                    vehicle.MarkAsNoLongerNeeded();
                    vehicle.Delete();
                }
            }
        }

        private void CleanupInvalidHandles()
        {
            var keysToRemove = new List<int>();
            foreach (var kvp in _activeSavedVehicles)
            {
                if (!Function.Call<bool>(Hash.DOES_ENTITY_EXIST, kvp.Key))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                _activeSavedVehicles.Remove(key);
            }
        }
    }
}
