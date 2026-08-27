using System;
using System.Collections.Generic;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;

namespace gta.Vehicles
{
    internal enum NitroFlameMode
    {
        DuringBoost = 0,
        AlwaysOn = 1,
        Disabled = 2
    }

    internal struct VehicleNitroConfig
    {
        public bool IsNitroEnabled;
        public NitroFlameMode FlameMode;

        public VehicleNitroConfig(bool isEnabled, NitroFlameMode flameMode)
        {
            IsNitroEnabled = isEnabled;
            FlameMode = flameMode;
        }
    }

    internal sealed class VehicleNitroService
    {
        private const ulong SetOverrideNitrousLevelHash = 0xC8E9B6B71B8E660D;

        private static readonly Dictionary<int, VehicleNitroConfig> VehicleConfigs =
            new Dictionary<int, VehicleNitroConfig>();

        private bool _isBoosting;
        private DateTime _lastSoundUtc = DateTime.MinValue;
        private int _lastVehicleHandle;

        public bool IsBoosting => _isBoosting;

        public VehicleNitroService()
        {
            RequestParticleAssets();
        }

        public static bool GetNitroEnabledForVehicle(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return false;
            }

            if (VehicleConfigs.TryGetValue(vehicle.Model.Hash, out var cfg))
            {
                return cfg.IsNitroEnabled;
            }

            return false; // Disabled by default for all vehicles
        }

        public static NitroFlameMode GetFlameModeForVehicle(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return NitroFlameMode.DuringBoost;
            }

            if (VehicleConfigs.TryGetValue(vehicle.Model.Hash, out var cfg))
            {
                return cfg.FlameMode;
            }

            return NitroFlameMode.DuringBoost;
        }

        public static void SetNitroConfigForVehicle(Vehicle vehicle, bool isEnabled, NitroFlameMode flameMode)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return;
            }

            VehicleConfigs[vehicle.Model.Hash] = new VehicleNitroConfig(isEnabled, flameMode);
        }

        private static void RequestParticleAssets()
        {
            if (!Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, "veh_xs_vehicle_mods"))
            {
                Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, "veh_xs_vehicle_mods");
            }
        }

        public void Update()
        {
            var character = Game.Player.Character;
            if (character == null || !character.Exists() || !character.IsInVehicle())
            {
                if (_isBoosting || _lastVehicleHandle != 0)
                {
                    StopBoosting(null);
                }

                return;
            }

            var vehicle = character.CurrentVehicle;
            if (vehicle == null || !vehicle.Exists() || vehicle.Driver != character || vehicle.IsDead || !vehicle.IsDriveable)
            {
                if (_isBoosting || _lastVehicleHandle != 0)
                {
                    StopBoosting(vehicle);
                }

                return;
            }

            // Clean up previous vehicle handle if changed
            if (_lastVehicleHandle != 0 && _lastVehicleHandle != vehicle.Handle)
            {
                DisableNativeNitroHandle(_lastVehicleHandle);
            }

            _lastVehicleHandle = vehicle.Handle;

            bool isNitroEnabled = GetNitroEnabledForVehicle(vehicle);
            NitroFlameMode flameMode = GetFlameModeForVehicle(vehicle);

            // AlwaysOn flame mode
            if (flameMode == NitroFlameMode.AlwaysOn && vehicle.IsEngineRunning)
            {
                EnableNativeNitro(vehicle);
            }

            // Check if Shift is actively held
            bool isShiftHeld = Game.IsKeyPressed(Keys.ShiftKey) || Game.IsKeyPressed(Keys.LShiftKey) || Game.IsKeyPressed(Keys.RShiftKey);

            if (isShiftHeld && isNitroEnabled && vehicle.IsEngineRunning)
            {
                ApplyNitro(vehicle, flameMode);
            }
            else
            {
                if (_isBoosting)
                {
                    _isBoosting = false;
                    Function.Call(Hash.ANIMPOSTFX_STOP, "RaceTurbo");
                    GameplayCamera.StopShaking();
                }

                if (flameMode != NitroFlameMode.AlwaysOn)
                {
                    DisableNativeNitro(vehicle);
                }
            }
        }

        private void ApplyNitro(Vehicle vehicle, NitroFlameMode flameMode)
        {
            if (!_isBoosting)
            {
                _isBoosting = true;
                Function.Call(Hash.ANIMPOSTFX_PLAY, "RaceTurbo", 0, true);
                GameplayCamera.Shake(CameraShake.RoadVibration, 0.35f);
            }

            if (flameMode != NitroFlameMode.Disabled)
            {
                EnableNativeNitro(vehicle);
            }

            // Smooth forward rocket propulsion without pitch torque
            float speed = vehicle.Speed;
            if (speed < 90.0f) // ~320 km/h cap
            {
                vehicle.ForwardSpeed = speed + 0.7f;
            }

            // Stabilizing downforce to keep all wheels firmly on asphalt
            if (vehicle.IsOnAllWheels)
            {
                vehicle.ApplyForceRelative(new Vector3(0, 0, -0.5f));
            }

            // Audio effect
            if ((DateTime.UtcNow - _lastSoundUtc).TotalMilliseconds > 180)
            {
                _lastSoundUtc = DateTime.UtcNow;
                Function.Call(Hash.PLAY_SOUND_FROM_ENTITY, -1, "Boost", "DLC_EXEC_ROCKET_VOLTIC_SOUNDS", vehicle.Handle, 0, 0, 0);
            }
        }

        private static void EnableNativeNitro(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return;
            }

            RequestParticleAssets();
            Function.Call((Hash)SetOverrideNitrousLevelHash, vehicle.Handle, true, 2500f, 2.0f, 1.0f, false);
            Function.Call(Hash.SET_VEHICLE_BOOST_ACTIVE, vehicle.Handle, true);
        }

        private static void DisableNativeNitro(Vehicle vehicle)
        {
            if (vehicle != null && vehicle.Exists())
            {
                DisableNativeNitroHandle(vehicle.Handle);
            }
        }

        private static void DisableNativeNitroHandle(int handle)
        {
            if (handle == 0)
            {
                return;
            }

            Function.Call((Hash)SetOverrideNitrousLevelHash, handle, false, 0f, 0f, 0f, true);
            Function.Call(Hash.SET_VEHICLE_BOOST_ACTIVE, handle, false);
        }

        private void StopBoosting(Vehicle vehicle)
        {
            _isBoosting = false;
            DisableNativeNitro(vehicle);

            if (_lastVehicleHandle != 0)
            {
                DisableNativeNitroHandle(_lastVehicleHandle);
                _lastVehicleHandle = 0;
            }

            Function.Call(Hash.ANIMPOSTFX_STOP, "RaceTurbo");
            GameplayCamera.StopShaking();
        }

        public void Abort()
        {
            StopBoosting(null);
        }
    }
}
