using GTA;
using GTA.Native;
using gta.Core;

namespace gta.Vehicles
{
    internal sealed class VehicleService
    {
        private const int VehicleLightsNormal = 0;
        private const int VehicleLightsForcedOn = 2;

        private bool _forceHeadlights;

        public void ProtectCurrentVehicle(bool skipMaxSpeedLimit)
        {
            var currentVehicle = Game.Player.Character.CurrentVehicle;
            if (currentVehicle == null)
            {
                return;
            }

            if (!skipMaxSpeedLimit)
            {
                currentVehicle.MaxSpeed = float.MaxValue;
            }
            currentVehicle.CanBeVisiblyDamaged = false;
            currentVehicle.BodyHealth = ModSettings.MaxStat;
            currentVehicle.Health = ModSettings.MaxStat;
        }

        public void ToggleForcedHeadlights()
        {
            var currentVehicle = GetCurrentVehicle();
            if (currentVehicle == null)
            {
                Notifier.Show("Сначала сядь в транспорт");
                return;
            }

            _forceHeadlights = !_forceHeadlights;

            if (_forceHeadlights)
            {
                ForceHeadlightsOn(currentVehicle);
                Notifier.Show("Фары всегда включены");
                return;
            }

            RestoreHeadlights(currentVehicle);
            Notifier.Show("Фары в обычном режиме");
        }

        public void ApplyForcedHeadlights()
        {
            if (!_forceHeadlights)
            {
                return;
            }

            var currentVehicle = GetCurrentVehicle();
            if (currentVehicle == null)
            {
                return;
            }

            ForceHeadlightsOn(currentVehicle);
        }

        public void RepairCurrentVehicle()
        {
            var currentVehicle = Game.Player.Character.CurrentVehicle;
            if (currentVehicle == null)
            {
                return;
            }

            currentVehicle.Repair();
            Notifier.Show("Восстановлено");
        }

        public void BoostCurrentVehicle()
        {
            var player = Game.Player.Character;
            if (!player.IsInVehicle())
            {
                Notifier.Show("Вы не в машине!");
                return;
            }

            var car = player.CurrentVehicle;
            if (car != null && car.Exists())
            {
                car.Speed *= 2f;
            }
        }

        private static Vehicle GetCurrentVehicle()
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

        private static void ForceHeadlightsOn(Vehicle vehicle)
        {
            Function.Call(Hash.SET_VEHICLE_LIGHTS, vehicle.Handle, VehicleLightsForcedOn);
        }

        private static void RestoreHeadlights(Vehicle vehicle)
        {
            Function.Call(Hash.SET_VEHICLE_LIGHTS, vehicle.Handle, VehicleLightsNormal);
        }
    }
}
