using System;
using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;

namespace gta.Worlds
{
    internal sealed class NorthYanktonAliveService
    {
        private static readonly Vector3 Center = new Vector3(3217.69f, -4834.51f, 111.81f);
        private static readonly Vector3 AreaMin = new Vector3(2980f, -5150f, 40f);
        private static readonly Vector3 AreaMax = new Vector3(3500f, -4620f, 190f);

        private const float ActiveRadius = 950f;
        private const float PedDensity = 0.35f;
        private const float ScenarioPedDensity = 0.8f;
        private const float VehicleDensity = 0.45f;
        private const float ParkedVehicleDensity = 0.65f;

        private readonly NorthYanktonLoader _loader;
        private readonly NorthYanktonAmbientService _ambient;

        private bool _isLoaded;
        private bool _isEnabled;
        private DateTime _nextPopulationFillUtc = DateTime.MinValue;
        private DateTime _nextStaticRefreshUtc = DateTime.MinValue;
        private DateTime _nextWeatherRefreshUtc = DateTime.MinValue;

        public NorthYanktonAliveService(NorthYanktonLoader loader)
        {
            _loader = loader;
            _ambient = new NorthYanktonAmbientService();
        }

        public void Load()
        {
            _loader.Load();
            _isLoaded = true;
            ApplySnowWeather();
            PrepareNativeWorld();
        }

        public void Toggle()
        {
            if (_isEnabled)
            {
                Disable();
                return;
            }

            Enable();
        }

        public void Update()
        {
            if (!_isEnabled)
            {
                return;
            }

            var now = DateTime.UtcNow;

            if (!IsPlayerNearYankton())
            {
                _ambient.Clear();
                return;
            }

            ApplyFramePopulationSettings();
            RequestPathNodes();
            _ambient.Update(now);

            if (now >= _nextStaticRefreshUtc)
            {
                PrepareNativeWorld();
                _nextStaticRefreshUtc = now.AddSeconds(5);
            }

            if (now >= _nextPopulationFillUtc)
            {
                FillNativePopulation();
                _nextPopulationFillUtc = now.AddSeconds(20);
            }

            if (now >= _nextWeatherRefreshUtc)
            {
                ApplySnowWeather();
                _nextWeatherRefreshUtc = now.AddSeconds(20);
            }
        }

        private void Enable()
        {
            if (!_isLoaded)
            {
                Load();
            }

            _isEnabled = true;
            _nextPopulationFillUtc = DateTime.MinValue;
            _nextStaticRefreshUtc = DateTime.MinValue;
            _nextWeatherRefreshUtc = DateTime.MinValue;
            _ambient.Enable();
            Notifier.Show("Население Северного Янктона включено");
        }

        private void Disable()
        {
            _isEnabled = false;
            _ambient.Disable();
            Function.Call(Hash.SET_CREATE_RANDOM_COPS, true);
            Function.Call(Hash.SET_CREATE_RANDOM_COPS_ON_SCENARIOS, true);
            Function.Call(Hash.SET_CREATE_RANDOM_COPS_NOT_ON_SCENARIOS, true);
            Notifier.Show("Население Северного Янктона выключено");
        }

        private static bool IsPlayerNearYankton()
        {
            return Game.Player.Character.Position.DistanceTo(Center) <= ActiveRadius;
        }

        private static void PrepareNativeWorld()
        {
            Function.Call(Hash.LOAD_ALL_PATH_NODES, true);
            Function.Call(Hash.SET_ROADS_IN_AREA, AreaMin.X, AreaMin.Y, AreaMin.Z, AreaMax.X, AreaMax.Y, AreaMax.Z, true, true);
            Function.Call(Hash.RESET_SCENARIO_GROUPS_ENABLED);
            Function.Call(Hash.RESET_SCENARIO_TYPES_ENABLED);
            Function.Call(Hash.CLEAR_SCENARIO_SPAWN_HISTORY);
            Function.Call(Hash.CLEAR_PED_NON_CREATION_AREA);
            Function.Call(Hash.SET_CREATE_RANDOM_COPS, false);
            Function.Call(Hash.SET_CREATE_RANDOM_COPS_ON_SCENARIOS, false);
            Function.Call(Hash.SET_CREATE_RANDOM_COPS_NOT_ON_SCENARIOS, false);
            Function.Call(Hash.SET_PED_POPULATION_BUDGET, 5);
            Function.Call(Hash.SET_VEHICLE_POPULATION_BUDGET, 4);
            Function.Call(Hash.SET_NUMBER_OF_PARKED_VEHICLES, 12);
            Function.Call(Hash.ADD_POP_MULTIPLIER_AREA, AreaMin.X, AreaMin.Y, AreaMin.Z, AreaMax.X, AreaMax.Y, AreaMax.Z, 1.0f, 1.0f, false, true);
            Function.Call(Hash.ADD_POP_MULTIPLIER_SPHERE, Center.X, Center.Y, Center.Z, ActiveRadius, 1.0f, 1.0f, false, true);
        }

        private static void ApplyFramePopulationSettings()
        {
            var playerPosition = Game.Player.Character.Position;

            Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, PedDensity);
            Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, ScenarioPedDensity, ScenarioPedDensity);
            Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, VehicleDensity);
            Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, VehicleDensity);
            Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, ParkedVehicleDensity);
            Function.Call(Hash.SET_AMBIENT_PED_RANGE_MULTIPLIER_THIS_FRAME, 1.0f);
            Function.Call(Hash.SET_AMBIENT_VEHICLE_RANGE_MULTIPLIER_THIS_FRAME, 1.0f);
            Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, 1.0f, 1.0f);
            Function.Call(Hash.SET_SCENARIO_PEDS_SPAWN_IN_SPHERE_AREA, playerPosition.X, playerPosition.Y, playerPosition.Z, 180f, 24);
            Function.Call(Hash.SET_SCENARIO_PEDS_SPAWN_IN_SPHERE_AREA, Center.X, Center.Y, Center.Z, ActiveRadius, 48);
            Function.Call(Hash.SET_PED_WALLA_DENSITY, 1.0f);
            Function.Call(Hash.SET_PED_INTERIOR_WALLA_DENSITY, 1.0f);
            Function.Call(Hash.ADJUST_AMBIENT_PED_SPAWN_DENSITIES_THIS_FRAME, 1.0f, 1.0f, 1.0f, 1.0f);
            Function.Call(Hash.USE_SCRIPT_CAM_FOR_AMBIENT_POPULATION_ORIGIN_THIS_FRAME, false, false);
            Function.Call(Hash.REQUEST_PATH_NODES_IN_AREA_THIS_FRAME, AreaMin.X, AreaMin.Y, AreaMax.X, AreaMax.Y);
        }

        private static void RequestPathNodes()
        {
            Function.Call(Hash.ADD_NAVMESH_REQUIRED_REGION, Center.X, Center.Y, 20.0f);
            Function.Call(Hash.REQUEST_PATH_NODES_IN_AREA_THIS_FRAME, AreaMin.X, AreaMin.Y, AreaMax.X, AreaMax.Y);
        }

        private static void FillNativePopulation()
        {
            Function.Call(Hash.INSTANTLY_FILL_PED_POPULATION);
            Function.Call(Hash.INSTANTLY_FILL_VEHICLE_POPULATION);
        }

        private static void ApplySnowWeather()
        {
            Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST, "XMAS");
            Function.Call(Hash.SET_WEATHER_TYPE_PERSIST, "XMAS");
            Function.Call(Hash.SET_OVERRIDE_WEATHER, "XMAS");
        }
    }
}
