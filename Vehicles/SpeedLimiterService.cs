using System;
using System.Drawing;
using GTA;
using GTA.Native;
using gta.Core;

namespace gta.Vehicles
{
    internal sealed class SpeedLimiterService
    {
        private bool _isLimiterActive;
        private float _limitSpeedMs;
        private Vehicle _limitedVehicle;
        private bool _isMetric;
        private int _lastMeasurementCheck;

        public bool IsLimiterActive => _isLimiterActive;
        public Vehicle LimitedVehicle => _limitedVehicle;

        public SpeedLimiterService()
        {
            _isMetric = Game.MeasurementSystem == MeasurementSystem.Metric;
            _lastMeasurementCheck = Game.GameTime;
        }

        public void ToggleLimiter()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.IsInVehicle())
            {
                Notifier.Show("Сначала сядь в транспорт");
                return;
            }

            Vehicle veh = player.CurrentVehicle;
            if (veh == null || !veh.Exists())
            {
                return;
            }

            float multiplier = _isMetric ? 3.6f : 2.23694f;
            string unit = _isMetric ? "км/ч" : "миль/ч";
            bool isSpecial = veh.Model.IsHelicopter;
            int visualOffset = isSpecial ? (_isMetric ? -10 : -6) : 0;

            if (!_isLimiterActive)
            {
                _limitedVehicle = veh;
                _limitSpeedMs = veh.Speed;
                _isLimiterActive = true;
                veh.MaxSpeed = _limitSpeedMs;

                int displaySpeed = (int)(_limitSpeedMs * multiplier) + visualOffset;
                Notifier.Show($"Лимитер скорости: ~b~{(displaySpeed < 0 ? 0 : displaySpeed)}~s~ {unit}");
            }
            else
            {
                DisableLimiter();
            }
        }

        public void IncreaseLimit()
        {
            if (!_isLimiterActive || _limitedVehicle == null || !_limitedVehicle.Exists())
            {
                return;
            }

            Ped player = Game.Player.Character;
            if (player == null || player.CurrentVehicle != _limitedVehicle)
            {
                return;
            }

            float multiplier = _isMetric ? 3.6f : 2.23694f;
            string unit = _isMetric ? "км/ч" : "миль/ч";
            bool isSpecial = _limitedVehicle.Model.IsHelicopter;
            int visualOffset = isSpecial ? (_isMetric ? -10 : -6) : 0;

            _limitSpeedMs += (5f / multiplier);
            _limitedVehicle.MaxSpeed = _limitSpeedMs;

            int displaySpeed = (int)(_limitSpeedMs * multiplier) + visualOffset;
            Notifier.Show($"Лимитер скорости: ~b~{(displaySpeed < 0 ? 0 : displaySpeed)}~s~ {unit}");
        }

        public void DecreaseLimit()
        {
            if (!_isLimiterActive || _limitedVehicle == null || !_limitedVehicle.Exists())
            {
                return;
            }

            Ped player = Game.Player.Character;
            if (player == null || player.CurrentVehicle != _limitedVehicle)
            {
                return;
            }

            float multiplier = _isMetric ? 3.6f : 2.23694f;
            string unit = _isMetric ? "км/ч" : "миль/ч";
            bool isSpecial = _limitedVehicle.Model.IsHelicopter;
            int visualOffset = isSpecial ? (_isMetric ? -10 : -6) : 0;

            if ((_limitSpeedMs * multiplier) <= 5.1f)
            {
                _limitSpeedMs = 0.0001f;
            }
            else
            {
                _limitSpeedMs -= (5f / multiplier);
            }
            _limitedVehicle.MaxSpeed = _limitSpeedMs;

            int displaySpeed = (int)(_limitSpeedMs * multiplier) + visualOffset;
            Notifier.Show($"Лимитер скорости: ~b~{(displaySpeed < 0 ? 0 : displaySpeed)}~s~ {unit}");
        }

        public void Update()
        {
            if (Game.GameTime > _lastMeasurementCheck + 5000)
            {
                _isMetric = Game.MeasurementSystem == MeasurementSystem.Metric;
                _lastMeasurementCheck = Game.GameTime;
            }

            if (!_isLimiterActive || _limitedVehicle == null || !_limitedVehicle.Exists())
            {
                return;
            }

            Ped player = Game.Player.Character;
            if (player == null)
            {
                return;
            }

            Vehicle currentVeh = player.IsInVehicle() ? player.CurrentVehicle : player.LastVehicle;
            if (currentVeh == null || currentVeh != _limitedVehicle)
            {
                // Игрок вышел из машины или пересел в другую
                ResetVehicleLimits(_limitedVehicle);
                _limitedVehicle = null;
                _isLimiterActive = false;
                return;
            }

            Vehicle veh = _limitedVehicle;

            if (veh.Model.IsBoat)
            {
                bool shouldAnchor = _limitSpeedMs < (2.5f / 3.6f);
                Function.Call(Hash.SET_BOAT_ANCHOR, veh, shouldAnchor);
            }

            if (veh.Speed > _limitSpeedMs)
            {
                if (veh.Model.IsBoat || (int)veh.ClassType == 20)
                {
                    float currentZ = veh.Velocity.Z;
                    GTA.Math.Vector3 horizontalVel = new GTA.Math.Vector3(veh.Velocity.X, veh.Velocity.Y, 0f);
                    if (horizontalVel.Length() > 0.1f)
                    {
                        horizontalVel = horizontalVel.Normalized * _limitSpeedMs;
                        veh.Velocity = new GTA.Math.Vector3(horizontalVel.X, horizontalVel.Y, currentZ);
                    }
                    else if (_limitSpeedMs == 0f)
                    {
                        veh.Velocity = new GTA.Math.Vector3(0f, 0f, currentZ);
                    }
                }
                veh.EngineTorqueMultiplier = 0.0f;
                veh.MaxSpeed = _limitSpeedMs;
            }
            else if (veh.Speed >= (_limitSpeedMs - 0.5f) && _limitSpeedMs > 0.1f)
            {
                veh.EngineTorqueMultiplier = 0.1f;
            }
            else if (_limitSpeedMs <= 0.1f)
            {
                veh.EngineTorqueMultiplier = 0.0f;
                veh.MaxSpeed = 0f;
            }
            else
            {
                veh.EngineTorqueMultiplier = 1.0f;
            }
        }

        public void Abort()
        {
            if (_limitedVehicle != null && _limitedVehicle.Exists())
            {
                ResetVehicleLimits(_limitedVehicle);
            }
        }

        private void DisableLimiter()
        {
            _isLimiterActive = false;
            if (_limitedVehicle != null && _limitedVehicle.Exists())
            {
                ResetVehicleLimits(_limitedVehicle);
            }
            _limitedVehicle = null;
            Notifier.Show("Лимитер скорости отключен");
        }

        private void ResetVehicleLimits(Vehicle veh)
        {
            if (veh != null && veh.Exists())
            {
                veh.MaxSpeed = float.MaxValue; // В нашем проекте защита использует float.MaxValue, вернем к нему
                veh.EngineTorqueMultiplier = 1.0f;
                if (veh.Model.IsBoat)
                {
                    Function.Call(Hash.SET_BOAT_ANCHOR, veh, false);
                }
            }
        }
    }
}
