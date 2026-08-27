using System;
using GTA;
using GTA.Native;
using gta.Core;

namespace gta.Vehicles
{
    internal enum SirenMode
    {
        Off = 0,
        LightsOnly = 1,
        LightsAndSound = 2
    }

    internal sealed class VehicleSirenService
    {
        private SirenMode _currentMode = SirenMode.Off;
        private int _lastVehicleHandle = -1;
        private int _pressTime;

        public SirenMode CurrentMode => _currentMode;

        public void Update()
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists() || !player.IsInVehicle())
            {
                _lastVehicleHandle = -1;
                _currentMode = SirenMode.Off;
                return;
            }

            var vehicle = player.CurrentVehicle;
            if (vehicle == null || !vehicle.Exists() || vehicle.Driver != player)
            {
                _lastVehicleHandle = -1;
                _currentMode = SirenMode.Off;
                return;
            }

            if (!IsEmergencyVehicle(vehicle))
            {
                return;
            }

            // Sync tracked handle if player switched vehicle
            if (vehicle.Handle != _lastVehicleHandle)
            {
                _lastVehicleHandle = vehicle.Handle;
                _currentMode = DetermineCurrentMode(vehicle);
            }

            // Disable default GTA V horn/siren toggle so our 3-state control has full priority
            Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, (int)Control.VehicleHorn, true);

            // Handle press of Horn button (E key on PC / Horn on Gamepad)
            if (Game.IsControlJustPressed(Control.VehicleHorn))
            {
                _pressTime = Game.GameTime;
                CycleSirenMode(vehicle);
            }
            // Only sound the horn if E / Horn button is HELD DOWN for > 250ms (prevents horn sound on quick taps)
            else if (Game.IsControlPressed(Control.VehicleHorn))
            {
                if (Game.GameTime - _pressTime > 250 && _currentMode != SirenMode.LightsAndSound)
                {
                    Function.Call(Hash.START_VEHICLE_HORN, vehicle.Handle, 200, 0, false);
                }
            }

            // Maintain muted state if in LightsOnly mode
            if (_currentMode == SirenMode.LightsOnly && vehicle.IsSirenActive)
            {
                MuteSirenSound(vehicle, true);
            }
        }

        public void CycleSirenMode(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return;

            switch (_currentMode)
            {
                case SirenMode.Off:
                    _currentMode = SirenMode.LightsOnly;
                    vehicle.IsSirenActive = true;
                    MuteSirenSound(vehicle, true);
                    Notifier.Show("Мигалки: Включены (без звука)");
                    ModLogger.Log("SIREN", $"Vehicle {vehicle.Handle} siren set to LightsOnly");
                    break;

                case SirenMode.LightsOnly:
                    _currentMode = SirenMode.LightsAndSound;
                    vehicle.IsSirenActive = true;
                    MuteSirenSound(vehicle, false);
                    Notifier.Show("Сирена: Включена (со звуком)");
                    ModLogger.Log("SIREN", $"Vehicle {vehicle.Handle} siren set to LightsAndSound");
                    break;

                case SirenMode.LightsAndSound:
                default:
                    _currentMode = SirenMode.Off;
                    vehicle.IsSirenActive = false;
                    MuteSirenSound(vehicle, false);
                    Notifier.Show("Сирена: Выключена");
                    ModLogger.Log("SIREN", $"Vehicle {vehicle.Handle} siren set to Off");
                    break;
            }
        }

        private static bool IsEmergencyVehicle(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return false;
            if (vehicle.ClassType == VehicleClass.Emergency) return true;

            return vehicle.IsSirenActive;
        }

        private static SirenMode DetermineCurrentMode(Vehicle vehicle)
        {
            if (!vehicle.IsSirenActive)
            {
                return SirenMode.Off;
            }

            // If siren is active, check if siren audio is muted
            bool isMuted = Function.Call<bool>(Hash.IS_VEHICLE_SIREN_AUDIO_ON, vehicle.Handle) == false;
            return isMuted ? SirenMode.LightsOnly : SirenMode.LightsAndSound;
        }

        private static void MuteSirenSound(Vehicle vehicle, bool mute)
        {
            // SET_VEHICLE_HAS_MUTED_SIRENS
            Function.Call(Hash.SET_VEHICLE_HAS_MUTED_SIRENS, vehicle.Handle, mute);
        }
    }
}
