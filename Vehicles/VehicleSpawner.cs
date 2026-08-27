using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;

namespace gta.Vehicles
{
    internal sealed class VehicleSpawner
    {
        private const int MaxTrackedVehicles = 16;
        private const int DefaultWindowTint = 1;
        private const int DefaultPrimaryColor = 0;
        private const int DefaultSecondaryColor = 0;
        private const int DefaultPearlescentColor = 0;

        private readonly WorldVehicleStore _worldVehicleStore;
        private Vehicle _currentVehicle;
        private readonly Queue<Vehicle> _spawnedVehicles = new Queue<Vehicle>();
        private bool _replaceExistingVehicle = true;

        public bool ReplaceExistingVehicle => _replaceExistingVehicle;

        public VehicleSpawner(WorldVehicleStore worldVehicleStore = null)
        {
            _worldVehicleStore = worldVehicleStore;
        }

        public void ToggleReplaceExistingVehicle()
        {
            _replaceExistingVehicle = !_replaceExistingVehicle;
            Notifier.Show(_replaceExistingVehicle ? "Замена машины: включена" : "Замена машины: выключена");
        }

        public void Spawn(string vehicleName)
        {
            int vehicleHash;
            if (TryGetVehicleHash(vehicleName, out vehicleHash))
            {
                Spawn(vehicleHash, vehicleName);
                return;
            }

            Spawn(new Model(vehicleName), vehicleName);
        }

        public void Spawn(int vehicleHash, string displayName)
        {
            Spawn(new Model(vehicleHash), displayName);
        }

        public void Spawn(IReadOnlyList<string> vehicleNames, int currentVehicleIndex)
        {
            if (currentVehicleIndex < 0 || currentVehicleIndex >= vehicleNames.Count)
            {
                return;
            }

            Spawn(vehicleNames[currentVehicleIndex]);
        }

        public int ClearPool()
        {
            CleanupTrackedVehicles();
            return TrimTrackedVehicles(0);
        }

        private void Spawn(Model carModel, string displayName)
        {
            try
            {
                if (!carModel.IsInCdImage || !carModel.IsVehicle)
                {
                    Notifier.Show("Модель транспорта недоступна: " + displayName);
                    return;
                }

                if (!carModel.Request(1000))
                {
                    Notifier.Show("Модель транспорта не загрузилась: " + displayName);
                    return;
                }

                var player = Game.Player.Character;
                var playerVehicle = GetPlayerCurrentVehicle();
                if (playerVehicle != null && playerVehicle.Exists())
                {
                    ReplaceCurrentVehicle(carModel, displayName, playerVehicle);
                    return;
                }

                if (_replaceExistingVehicle)
                {
                    RemovePreviouslySpawnedVehicles();
                }
                else
                {
                    CleanupTrackedVehicles();
                    TrimTrackedVehicles(MaxTrackedVehicles - 1);
                }

                Vector3 spawnPosition;
                float heading;

                if (player != null && player.Exists())
                {
                    Vector3 forward = player.ForwardVector;
                    forward.Z = 0f;
                    if (forward.Length() > 0.001f)
                    {
                        forward.Normalize();
                    }
                    else
                    {
                        forward = Vector3.RelativeFront;
                    }

                    spawnPosition = player.Position + forward * 5.0f;
                    spawnPosition.Z = GetGroundZ(spawnPosition);
                    heading = (player.Heading + 90f) % 360f;
                }
                else
                {
                    spawnPosition = GameplayCamera.Position + GameplayCamera.Direction * ModSettings.VehicleSpawnDistance;
                    spawnPosition.Z = GetGroundZ(spawnPosition);
                    heading = GameplayCamera.RelativeHeading;
                }

                _currentVehicle = GTA.World.CreateVehicle(carModel, spawnPosition, heading);
                if (_currentVehicle == null || !_currentVehicle.Exists())
                {
                    carModel.MarkAsNoLongerNeeded();
                    Notifier.Show("Не удалось создать: " + displayName);
                    return;
                }

                _spawnedVehicles.Enqueue(_currentVehicle);
                ApplyDefaultPaint(_currentVehicle);
                Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, _currentVehicle.Handle);

                carModel.MarkAsNoLongerNeeded();
                Notifier.Show(displayName + " создан");
            }
            catch (Exception ex)
            {
                Notifier.Show(ex.Message);
            }
        }

        private static void ApplyDefaultPaint(Vehicle vehicle)
        {
            var pearlescent = new OutputArgument();
            var wheel = new OutputArgument();
            Function.Call(Hash.GET_VEHICLE_EXTRA_COLOURS, vehicle.Handle, pearlescent, wheel);

            Function.Call(Hash.SET_VEHICLE_WINDOW_TINT, vehicle.Handle, DefaultWindowTint);
            Function.Call(Hash.SET_VEHICLE_COLOURS, vehicle.Handle, DefaultPrimaryColor, DefaultSecondaryColor);
            Function.Call(Hash.SET_VEHICLE_EXTRA_COLOURS, vehicle.Handle, DefaultPearlescentColor, wheel.GetResult<int>());
        }

        private void CleanupTrackedVehicles()
        {
            var count = _spawnedVehicles.Count;
            for (var i = 0; i < count; i++)
            {
                var vehicle = _spawnedVehicles.Dequeue();
                if (vehicle != null && vehicle.Exists())
                {
                    _spawnedVehicles.Enqueue(vehicle);
                }
            }
        }

        private void ReplaceCurrentVehicle(Model carModel, string displayName, Vehicle playerVehicle)
        {
            bool isPlayerVehicleSaved = IsVehicleSaved(playerVehicle);
            Vector3 spawnPosition = isPlayerVehicleSaved
                ? playerVehicle.Position + playerVehicle.RightVector * 3.5f
                : playerVehicle.Position;
            float heading = playerVehicle.Heading;
            float speed = playerVehicle.Speed;
            Vector3 velocity = playerVehicle.Velocity;

            var player = Game.Player.Character;

            if (!isPlayerVehicleSaved)
            {
                playerVehicle.MarkAsNoLongerNeeded();
                playerVehicle.Delete();
            }

            var replacementVehicle = GTA.World.CreateVehicle(carModel, spawnPosition, heading);
            if (replacementVehicle == null || !replacementVehicle.Exists())
            {
                carModel.MarkAsNoLongerNeeded();
                Notifier.Show("Не удалось заменить текущую машину: " + displayName);
                return;
            }

            _spawnedVehicles.Clear();
            _currentVehicle = replacementVehicle;
            _spawnedVehicles.Enqueue(replacementVehicle);

            ApplyDefaultPaint(replacementVehicle);
            Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, replacementVehicle.Handle);

            if (player != null && player.Exists())
            {
                Function.Call(Hash.SET_PED_INTO_VEHICLE, player.Handle, replacementVehicle.Handle, (int)VehicleSeat.Driver);
            }

            if (speed > 1.0f)
            {
                replacementVehicle.Speed = speed;
                replacementVehicle.Velocity = velocity;
            }

            carModel.MarkAsNoLongerNeeded();
            Notifier.Show(displayName + (isPlayerVehicleSaved ? " создан рядом" : " заменил текущую машину"));
        }

        private void RemovePreviouslySpawnedVehicles()
        {
            var playerVehicle = GetPlayerCurrentVehicle();
            var vehiclesToKeep = new Queue<Vehicle>();

            while (_spawnedVehicles.Count > 0)
            {
                var vehicle = _spawnedVehicles.Dequeue();
                if (vehicle == null || !vehicle.Exists())
                {
                    continue;
                }

                if (IsSameVehicle(vehicle, playerVehicle) || IsVehicleSaved(vehicle))
                {
                    vehiclesToKeep.Enqueue(vehicle);
                    continue;
                }

                vehicle.MarkAsNoLongerNeeded();
                vehicle.Delete();
            }

            while (vehiclesToKeep.Count > 0)
            {
                _spawnedVehicles.Enqueue(vehiclesToKeep.Dequeue());
            }
        }

        private int TrimTrackedVehicles(int maxCount)
        {
            var playerVehicle = GetPlayerCurrentVehicle();
            var checkedCount = _spawnedVehicles.Count;
            var removedCount = 0;

            for (var i = 0; i < checkedCount; i++)
            {
                if (_spawnedVehicles.Count <= maxCount)
                {
                    break;
                }

                var vehicle = _spawnedVehicles.Dequeue();
                if (vehicle == null || !vehicle.Exists())
                {
                    continue;
                }

                if (IsSameVehicle(vehicle, playerVehicle) || IsVehicleSaved(vehicle))
                {
                    _spawnedVehicles.Enqueue(vehicle);
                    continue;
                }

                vehicle.MarkAsNoLongerNeeded();
                vehicle.Delete();
                removedCount++;
            }

            return removedCount;
        }

        private bool IsVehicleSaved(Vehicle vehicle)
        {
            return _worldVehicleStore != null && _worldVehicleStore.IsVehicleSaved(vehicle);
        }

        private static float GetGroundZ(Vector3 position)
        {
            float groundZ;
            return GTA.World.GetGroundHeight(position + new Vector3(0f, 0f, 3f), out groundZ, GetGroundHeightMode.Normal)
                ? groundZ
                : position.Z;
        }

        private static Vehicle GetPlayerCurrentVehicle()
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists() || !player.IsInVehicle())
            {
                return null;
            }

            var vehicle = player.CurrentVehicle;
            return vehicle != null && vehicle.Exists()
                ? vehicle
                : null;
        }

        private static bool IsSameVehicle(Vehicle first, Vehicle second)
        {
            return first != null &&
                   second != null &&
                   first.Exists() &&
                   second.Exists() &&
                   first.Handle == second.Handle;
        }

        private static bool TryGetVehicleHash(string vehicleName, out int vehicleHash)
        {
            vehicleHash = 0;

            try
            {
                var parsedHash = (VehicleHash)Enum.Parse(typeof(VehicleHash), vehicleName, true);
                vehicleHash = unchecked((int)(uint)parsedHash);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
