using GTA;
using GTA.Native;

namespace gta.Vehicles
{
    internal sealed class VehicleIndicatorService
    {
        private bool _leftIndicatorOn;
        private bool _rightIndicatorOn;

        public void ToggleLeft()
        {
            _leftIndicatorOn = !_leftIndicatorOn;
            _rightIndicatorOn = false;
        }

        public void ToggleRight()
        {
            _rightIndicatorOn = !_rightIndicatorOn;
            _leftIndicatorOn = false;
        }

        public void ApplyToCurrentVehicle()
        {
            var player = Game.Player.Character;
            if (!player.IsInVehicle())
            {
                return;
            }

            var vehicle = player.CurrentVehicle;
            Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, vehicle, 0, _leftIndicatorOn);
            Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, vehicle, 1, _rightIndicatorOn);
        }
    }
}
