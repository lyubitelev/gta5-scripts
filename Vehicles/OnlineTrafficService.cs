using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;

namespace gta.Vehicles
{
    internal sealed class OnlineTrafficService
    {
        private static readonly int[] RealisticColorIds = new int[]
        {
            0,   // Metallic Black
            1,   // Metallic Graphite Black
            2,   // Metallic Black Steel
            3,   // Metallic Dark Silver
            4,   // Metallic Silver
            5,   // Metallic Blue Silver
            6,   // Metallic Steel Gray
            7,   // Metallic Shadow Silver
            12,  // Matte Black
            27,  // Metallic Red
            49,  // Metallic Dark Green
            62,  // Metallic Dark Blue
            64,  // Metallic Saxon Blue
            96,  // Metallic Chocolate Brown
            111, // Metallic White
            112, // Frost White
            141, // Metallic Midnight Blue
            143, // Metallic Wine Red
            150  // Metallic Lava Red
        };

        private readonly Dictionary<VehicleClass, List<string>> _fileCatalog = new Dictionary<VehicleClass, List<string>>();
        private readonly HashSet<int> _processedVehicles = new HashSet<int>();
        private readonly Random _random = new Random();
        private readonly WorldVehicleStore _worldVehicleStore;
        private readonly GeneratedVehicleCatalog _generatedVehicles;

        private bool _isEnabled = true;
        private float _spawnChance = 0.50f; // 50% chance
        private int _nextCheckTime;

        private string _loadingModelName;
        private int _targetVehicleHandle = -1;

        public OnlineTrafficService(WorldVehicleStore worldVehicleStore = null, GeneratedVehicleCatalog generatedVehicles = null)
        {
            _worldVehicleStore = worldVehicleStore;
            _generatedVehicles = generatedVehicles;
            BuildFileCatalog();
        }

        public bool IsEnabled => _isEnabled;

        public void ToggleEnabled()
        {
            _isEnabled = !_isEnabled;
            Notifier.Show(_isEnabled ? "Трафик GTA Online: Включён" : "Трафик GTA Online: Выключен");
            ModLogger.Log("TRAFFIC", $"GTA Online traffic toggle: {_isEnabled}");
        }

        public void Update()
        {
            if (!_isEnabled) return;

            int currentTime = Game.GameTime;
            if (currentTime < _nextCheckTime) return;
            _nextCheckTime = currentTime + 500; // Check every 0.5 seconds in non-blocking mode

            CleanupProcessedHandles();
            ProcessAmbientTraffic();
        }

        private void BuildFileCatalog()
        {
            if (_generatedVehicles == null || _generatedVehicles.Vehicles == null) return;

            int loadedCount = 0;
            foreach (var vehicleName in _generatedVehicles.Vehicles)
            {
                if (string.IsNullOrWhiteSpace(vehicleName)) continue;

                var model = new Model(vehicleName);
                if (!model.IsInCdImage || !model.IsVehicle) continue;

                VehicleClass vc = (VehicleClass)Function.Call<int>(Hash.GET_VEHICLE_CLASS_FROM_NAME, (uint)model.Hash);
                if (!_fileCatalog.TryGetValue(vc, out var list))
                {
                    list = new List<string>();
                    _fileCatalog[vc] = list;
                }
                list.Add(vehicleName);
                loadedCount++;
            }

            ModLogger.Log("TRAFFIC", $"BuildFileCatalog loaded {loadedCount} vehicles across {_fileCatalog.Count} vehicle classes from file.");
        }

        private void ProcessAmbientTraffic()
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            // Handle pending non-blocking model request first
            if (!string.IsNullOrEmpty(_loadingModelName) && _targetVehicleHandle != -1)
            {
                var targetVeh = Entity.FromHandle(_targetVehicleHandle) as Vehicle;
                var pendingModel = new Model(_loadingModelName);

                if (pendingModel.IsLoaded)
                {
                    if (targetVeh != null && targetVeh.Exists())
                    {
                        ReplaceWithDlcVehicle(targetVeh, _loadingModelName);
                    }
                    pendingModel.MarkAsNoLongerNeeded();
                    _loadingModelName = null;
                    _targetVehicleHandle = -1;
                    return;
                }

                // Request model in background without blocking main thread
                pendingModel.Request();
                return;
            }

            var nearbyVehicles = GTA.World.GetNearbyVehicles(player.Position, 200.0f);
            if (nearbyVehicles == null || nearbyVehicles.Length == 0) return;

            foreach (var oldVehicle in nearbyVehicles)
            {
                if (!IsValidTarget(oldVehicle, player)) continue;

                _processedVehicles.Add(oldVehicle.Handle);

                // Random chance check
                if (_random.NextDouble() > _spawnChance) continue;

                // Pick DLC model for vehicle's class from file catalog
                string modelName = GetRandomDlcModelName(oldVehicle.ClassType);
                if (string.IsNullOrEmpty(modelName)) continue;

                var model = new Model(modelName);
                if (!model.IsInCdImage || !model.IsVehicle) continue;

                if (model.IsLoaded)
                {
                    ReplaceWithDlcVehicle(oldVehicle, modelName);
                    model.MarkAsNoLongerNeeded();
                }
                else
                {
                    // Non-blocking request: request model to stream asynchronously
                    model.Request();
                    _loadingModelName = modelName;
                    _targetVehicleHandle = oldVehicle.Handle;
                }

                break; // Process max 1 vehicle request per cycle
            }
        }

        private bool IsValidTarget(Vehicle vehicle, Ped player)
        {
            if (vehicle == null || !vehicle.Exists()) return false;
            if (_processedVehicles.Contains(vehicle.Handle)) return false;

            // Distance check (target vehicles around player)
            float distance = vehicle.Position.DistanceTo(player.Position);
            if (distance < 25.0f || distance > 220.0f) return false;

            // Camera visibility check: Never replace vehicles directly in front/visible to camera unless far away (>110m)
            bool isVisibleToCamera = Function.Call<bool>(Hash.IS_SPHERE_VISIBLE, vehicle.Position.X, vehicle.Position.Y, vehicle.Position.Z, 3.5f);
            if (isVisibleToCamera && distance < 110.0f) return false;

            // Skip player vehicle, saved vehicles, emergency/service vehicles
            if (player.IsInVehicle() && player.CurrentVehicle == vehicle) return false;
            if (_worldVehicleStore != null && _worldVehicleStore.IsVehicleSaved(vehicle)) return false;

            // 1. Strict road class filter: Never replace planes, helicopters, boats, submarines, trains, emergency, or military vehicles
            if (vehicle.ClassType == VehicleClass.Planes
                || vehicle.ClassType == VehicleClass.Helicopters
                || vehicle.ClassType == VehicleClass.Boats
                || vehicle.ClassType == VehicleClass.Trains
                || vehicle.ClassType == VehicleClass.Emergency
                || vehicle.ClassType == VehicleClass.Service
                || vehicle.ClassType == VehicleClass.Commercial
                || vehicle.ClassType == VehicleClass.Industrial
                || vehicle.ClassType == VehicleClass.Military)
            {
                return false;
            }

            // 2. Strict Water & Air checks: Never replace vehicles in water, rivers, ocean, or flying mid-air
            if (vehicle.IsInWater || Function.Call<bool>(Hash.IS_ENTITY_IN_WATER, vehicle.Handle)) return false;
            if (Function.Call<bool>(Hash.IS_ENTITY_IN_AIR, vehicle.Handle)) return false;
            if (vehicle.HeightAboveGround > 2.5f) return false;

            var outWaterZ = new OutputArgument();
            if (Function.Call<bool>(Hash.GET_WATER_HEIGHT, vehicle.Position.X, vehicle.Position.Y, vehicle.Position.Z, outWaterZ))
            {
                float waterZ = outWaterZ.GetResult<float>();
                if (vehicle.Position.Z <= waterZ + 1.5f) return false;
            }

            // If driven by a ped, verify it's not the player or a dead ped
            var driver = vehicle.Driver;
            if (driver != null && driver.Exists() && (driver.IsPlayer || driver.IsDead)) return false;

            // ANTI-STACKING / ANTI-CLIPPING CHECK:
            // Ensure no other vehicle is within 4.5 meters of target position to prevent stacking on top of each other
            var nearby = GTA.World.GetNearbyVehicles(vehicle.Position, 4.5f);
            if (nearby != null)
            {
                foreach (var other in nearby)
                {
                    if (other != null && other.Exists() && other.Handle != vehicle.Handle)
                    {
                        return false; // Proximity collision risk: skip replacing
                    }
                }
            }

            return true;
        }

        private string GetRandomDlcModelName(VehicleClass vehicleClass)
        {
            if (_fileCatalog.Count == 0) return null;

            // 1. Try matching exact vehicle class from file catalog
            if (_fileCatalog.TryGetValue(vehicleClass, out var fileModels) && fileModels != null && fileModels.Count > 0)
            {
                return fileModels[_random.Next(fileModels.Count)];
            }

            // 2. Fallback: only pick from valid road car/bike classes in file catalog
            var validRoadClasses = new List<VehicleClass>();
            foreach (var vc in _fileCatalog.Keys)
            {
                if (vc != VehicleClass.Planes && vc != VehicleClass.Helicopters && vc != VehicleClass.Boats && vc != VehicleClass.Trains)
                {
                    validRoadClasses.Add(vc);
                }
            }

            if (validRoadClasses.Count == 0) return null;

            var randomClass = validRoadClasses[_random.Next(validRoadClasses.Count)];
            var randomList = _fileCatalog[randomClass];
            return randomList.Count > 0 ? randomList[_random.Next(randomList.Count)] : null;
        }

        private void ReplaceWithDlcVehicle(Vehicle oldVehicle, string dlcModelName)
        {
            var model = new Model(dlcModelName);
            if (!model.IsInCdImage || !model.IsVehicle) return;

            try
            {
                Vector3 pos = oldVehicle.Position;
                float heading = oldVehicle.Heading;

                // Robust detection of whether vehicle was moving in ambient traffic vs parked
                var oldDriver = oldVehicle.Driver;
                bool hasActiveDriver = oldDriver != null && oldDriver.Exists() && !oldDriver.IsDead;
                bool isMoving = oldVehicle.Speed > 0.5f;
                bool isEngineRunning = oldVehicle.IsEngineRunning;

                bool isDriving = hasActiveDriver || isMoving || isEngineRunning;
                float currentSpeed = oldVehicle.Speed > 2.0f ? oldVehicle.Speed : (isDriving ? 12.0f : 0.0f);

                // 1. Delete old ambient vehicle
                oldVehicle.MarkAsNoLongerNeeded();
                oldVehicle.Delete();

                // Double check position clearance after deletion
                var remaining = GTA.World.GetNearbyVehicles(pos, 3.0f);
                if (remaining != null && remaining.Length > 0)
                {
                    model.MarkAsNoLongerNeeded();
                    return;
                }

                // 2. Create replacement DLC vehicle
                var newVehicle = GTA.World.CreateVehicle(model, pos, heading);
                if (newVehicle == null || !newVehicle.Exists())
                {
                    model.MarkAsNoLongerNeeded();
                    return;
                }

                _processedVehicles.Add(newVehicle.Handle);
                Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, newVehicle.Handle);

                if (isDriving)
                {
                    // Moving vehicle: create driver ped, start engine, match speed and drive wander task
                    newVehicle.IsEngineRunning = true;
                    newVehicle.Speed = Math.Max(currentSpeed, 8.0f);

                    Ped driverPed = newVehicle.CreateRandomPedOnSeat(VehicleSeat.Driver);
                    if (driverPed != null && driverPed.Exists())
                    {
                        float driveSpeed = Math.Max(currentSpeed, 12.0f);
                        Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driverPed.Handle, newVehicle.Handle, driveSpeed, 786603);
                        driverPed.MarkAsNoLongerNeeded();
                    }
                }
                else
                {
                    // Parked vehicle: keep empty and engine off so it stays cleanly parked on the lot/roadside
                    newVehicle.IsEngineRunning = false;
                    newVehicle.Speed = 0.0f;
                }

                // 3. Apply authentic realistic GTA V traffic color palette (safe enum Hash.SET_VEHICLE_COLOURS)
                ApplyRealisticColors(newVehicle);

                // 4. Release vehicle to game engine ownership
                newVehicle.MarkAsNoLongerNeeded();

                ModLogger.Log("TRAFFIC", $"Replaced ambient vehicle with Online DLC car '{dlcModelName}' (isDriving={isDriving}, speed={currentSpeed:F1}, Handle={newVehicle.Handle})");
            }
            catch (Exception ex)
            {
                ModLogger.Log("TRAFFIC", $"Error replacing vehicle with {dlcModelName}: {ex.Message}");
            }
            finally
            {
                model.MarkAsNoLongerNeeded();
            }
        }

        private void ApplyRealisticColors(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return;

            int primaryColor = RealisticColorIds[_random.Next(RealisticColorIds.Length)];
            int secondaryColor = RealisticColorIds[_random.Next(RealisticColorIds.Length)];
            Function.Call(Hash.SET_VEHICLE_COLOURS, vehicle.Handle, primaryColor, secondaryColor);
        }

        private void CleanupProcessedHandles()
        {
            if (_processedVehicles.Count > 200)
            {
                _processedVehicles.Clear();
            }
        }
    }
}
