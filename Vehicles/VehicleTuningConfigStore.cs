using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GTA;
using GTA.Native;
using gta.Core;

namespace gta.Vehicles
{
    internal sealed class VehicleTuningConfigStore
    {
        private const int Version = 1;
        private const int MaxExtraId = 20;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private readonly string _directory;

        public VehicleTuningConfigStore(string directory)
        {
            _directory = directory;
            Directory.CreateDirectory(directory);
        }

        public bool HasConfig(Vehicle vehicle)
        {
            return File.Exists(GetPath(vehicle));
        }

        public void Save(Vehicle vehicle)
        {
            var config = Capture(vehicle);
            File.WriteAllText(GetPath(vehicle), JsonSerializer.Serialize(config, JsonOptions));
        }

        public bool Apply(Vehicle vehicle)
        {
            var path = GetPath(vehicle);
            if (!File.Exists(path))
            {
                return false;
            }

            var config = JsonSerializer.Deserialize<VehicleTuningConfig>(File.ReadAllText(path));
            if (config == null || config.ModelHash != vehicle.Model.Hash)
            {
                return false;
            }

            Apply(vehicle, config);
            return true;
        }

        public static VehicleTuningConfig Capture(Vehicle vehicle)
        {
            var primary = new OutputArgument();
            var secondary = new OutputArgument();
            Function.Call(Hash.GET_VEHICLE_COLOURS, vehicle.Handle, primary, secondary);

            var pearlescent = new OutputArgument();
            var wheelColor = new OutputArgument();
            Function.Call(Hash.GET_VEHICLE_EXTRA_COLOURS, vehicle.Handle, pearlescent, wheelColor);

            var neonRed = new OutputArgument();
            var neonGreen = new OutputArgument();
            var neonBlue = new OutputArgument();
            Function.Call(Hash.GET_VEHICLE_NEON_COLOUR, vehicle.Handle, neonRed, neonGreen, neonBlue);

            var smokeRed = new OutputArgument();
            var smokeGreen = new OutputArgument();
            var smokeBlue = new OutputArgument();
            Function.Call(Hash.GET_VEHICLE_TYRE_SMOKE_COLOR, vehicle.Handle, smokeRed, smokeGreen, smokeBlue);

            var config = new VehicleTuningConfig
            {
                Version = Version,
                ModelHash = vehicle.Model.Hash,
                ModKit = GetModKitIndexForConfig(vehicle),
                WheelType = Function.Call<int>(Hash.GET_VEHICLE_WHEEL_TYPE, vehicle.Handle),
                WindowTint = Function.Call<int>(Hash.GET_VEHICLE_WINDOW_TINT, vehicle.Handle),
                PlateType = Function.Call<int>(Hash.GET_VEHICLE_NUMBER_PLATE_TEXT_INDEX, vehicle.Handle),
                PlateText = Function.Call<string>(Hash.GET_VEHICLE_NUMBER_PLATE_TEXT, vehicle.Handle),
                PrimaryColor = primary.GetResult<int>(),
                SecondaryColor = secondary.GetResult<int>(),
                PearlescentColor = pearlescent.GetResult<int>(),
                WheelColor = wheelColor.GetResult<int>(),
                XenonColor = Function.Call<int>(Hash.GET_VEHICLE_XENON_LIGHT_COLOR_INDEX, vehicle.Handle),
                Livery = Function.Call<int>(Hash.GET_VEHICLE_LIVERY, vehicle.Handle),
                Livery2 = Function.Call<int>(Hash.GET_VEHICLE_LIVERY2, vehicle.Handle),
                BulletproofTires = !Function.Call<bool>(Hash.GET_VEHICLE_TYRES_CAN_BURST, vehicle.Handle),
                NeonRed = neonRed.GetResult<int>(),
                NeonGreen = neonGreen.GetResult<int>(),
                NeonBlue = neonBlue.GetResult<int>(),
                TireSmokeRed = smokeRed.GetResult<int>(),
                TireSmokeGreen = smokeGreen.GetResult<int>(),
                TireSmokeBlue = smokeBlue.GetResult<int>(),
                NitroEnabled = VehicleNitroService.GetNitroEnabledForVehicle(vehicle),
                NitroFlameMode = (int)VehicleNitroService.GetFlameModeForVehicle(vehicle),
                WindowsDown = VehicleUpgradeService.GetWindowsDown(vehicle),
                ConvertibleRoofState = Function.Call<int>(Hash.GET_CONVERTIBLE_ROOF_STATE, vehicle.Handle),
                KeepEngineRunning = VehicleUpgradeService.GetKeepEngineRunning(vehicle),
                ForcedBrakeLights = VehicleUpgradeService.GetForcedBrakeLights(vehicle)
            };

            for (var i = 0; i < 4; i++)
            {
                config.Neons.Add(Function.Call<bool>(Hash.GET_VEHICLE_NEON_ENABLED, vehicle.Handle, i));
            }

            for (var modType = 0; modType <= ModSettings.MaxVehicleModType; modType++)
            {
                config.Mods.Add(new VehicleModConfig
                {
                    Type = modType,
                    Value = Function.Call<int>(Hash.GET_VEHICLE_MOD, vehicle.Handle, modType),
                    Variation = Function.Call<bool>(Hash.GET_VEHICLE_MOD_VARIATION, vehicle.Handle, modType),
                    IsToggle = IsToggleModType(modType),
                    ToggleValue = IsToggleModType(modType) && Function.Call<bool>(Hash.IS_TOGGLE_MOD_ON, vehicle.Handle, modType)
                });
            }

            for (var extraId = 1; extraId <= MaxExtraId; extraId++)
            {
                if (!Function.Call<bool>(Hash.DOES_EXTRA_EXIST, vehicle.Handle, extraId))
                {
                    continue;
                }

                config.Extras.Add(new VehicleExtraConfig
                {
                    Id = extraId,
                    Enabled = Function.Call<bool>(Hash.IS_VEHICLE_EXTRA_TURNED_ON, vehicle.Handle, extraId)
                });
            }

            return config;
        }

        public static void Apply(Vehicle vehicle, VehicleTuningConfig config)
        {
            var modKitCount = Function.Call<int>(Hash.GET_NUM_MOD_KITS, vehicle.Handle);
            if (modKitCount > 0)
            {
                Function.Call(Hash.SET_VEHICLE_MOD_KIT, vehicle.Handle, NormalizeModKitIndex(config.ModKit, modKitCount));
            }

            Function.Call(Hash.SET_VEHICLE_WHEEL_TYPE, vehicle.Handle, config.WheelType);
            Function.Call(Hash.SET_VEHICLE_WINDOW_TINT, vehicle.Handle, config.WindowTint);
            Function.Call(Hash.SET_VEHICLE_NUMBER_PLATE_TEXT_INDEX, vehicle.Handle, config.PlateType);

            if (!string.IsNullOrWhiteSpace(config.PlateText))
            {
                Function.Call(Hash.SET_VEHICLE_NUMBER_PLATE_TEXT, vehicle.Handle, config.PlateText);
            }

            Function.Call(Hash.SET_VEHICLE_COLOURS, vehicle.Handle, config.PrimaryColor, config.SecondaryColor);
            Function.Call(Hash.SET_VEHICLE_EXTRA_COLOURS, vehicle.Handle, config.PearlescentColor, config.WheelColor);

            foreach (var extra in config.Extras)
            {
                if (!Function.Call<bool>(Hash.DOES_EXTRA_EXIST, vehicle.Handle, extra.Id))
                {
                    continue;
                }

                Function.Call(Hash.SET_VEHICLE_EXTRA, vehicle.Handle, extra.Id, !extra.Enabled);
            }

            foreach (var mod in config.Mods)
            {
                if (mod.IsToggle)
                {
                    Function.Call(Hash.TOGGLE_VEHICLE_MOD, vehicle.Handle, mod.Type, mod.ToggleValue);
                    continue;
                }

                var numMods = Function.Call<int>(Hash.GET_NUM_VEHICLE_MODS, vehicle.Handle, mod.Type);
                if (numMods <= 0)
                {
                    continue;
                }

                if (mod.Value < 0)
                {
                    Function.Call(Hash.REMOVE_VEHICLE_MOD, vehicle.Handle, mod.Type);
                    continue;
                }

                if (mod.Value < numMods)
                {
                    Function.Call(Hash.SET_VEHICLE_MOD, vehicle.Handle, mod.Type, mod.Value, mod.Variation);
                }
            }

            if (Function.Call<int>(Hash.GET_VEHICLE_LIVERY_COUNT, vehicle.Handle) > 0)
            {
                Function.Call(Hash.SET_VEHICLE_LIVERY, vehicle.Handle, config.Livery);
            }

            if (Function.Call<int>(Hash.GET_VEHICLE_LIVERY2_COUNT, vehicle.Handle) > 0)
            {
                Function.Call(Hash.SET_VEHICLE_LIVERY2, vehicle.Handle, config.Livery2);
            }

            Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, vehicle.Handle, !config.BulletproofTires);
            Function.Call(Hash.SET_VEHICLE_XENON_LIGHT_COLOR_INDEX, vehicle.Handle, config.XenonColor);
            Function.Call(Hash.SET_VEHICLE_TYRE_SMOKE_COLOR, vehicle.Handle, config.TireSmokeRed, config.TireSmokeGreen, config.TireSmokeBlue);
            Function.Call(Hash.SET_VEHICLE_NEON_COLOUR, vehicle.Handle, config.NeonRed, config.NeonGreen, config.NeonBlue);

            for (var i = 0; i < config.Neons.Count && i < 4; i++)
            {
                Function.Call(Hash.SET_VEHICLE_NEON_ENABLED, vehicle.Handle, i, config.Neons[i]);
            }

            if (config.WindowsDown)
            {
                Function.Call(Hash.ROLL_DOWN_WINDOWS, vehicle.Handle);
                VehicleUpgradeService.SetWindowsDown(vehicle, true);
            }
            else
            {
                for (var w = 0; w < 4; w++)
                {
                    Function.Call(Hash.ROLL_UP_WINDOW, vehicle.Handle, w);
                }
                VehicleUpgradeService.SetWindowsDown(vehicle, false);
            }

            if (Function.Call<bool>(Hash.IS_VEHICLE_A_CONVERTIBLE, vehicle.Handle, false))
            {
                if (config.ConvertibleRoofState == 2)
                {
                    Function.Call(Hash.LOWER_CONVERTIBLE_ROOF, vehicle.Handle, true);
                }
                else
                {
                    Function.Call(Hash.RAISE_CONVERTIBLE_ROOF, vehicle.Handle, true);
                }
            }

            VehicleUpgradeService.SetKeepEngineRunning(vehicle, config.KeepEngineRunning);
            VehicleUpgradeService.SetForcedBrakeLights(vehicle, config.ForcedBrakeLights);
            VehicleNitroService.SetNitroConfigForVehicle(vehicle, config.NitroEnabled, (NitroFlameMode)config.NitroFlameMode);
        }

        private string GetPath(Vehicle vehicle)
        {
            var modelHash = unchecked((uint)vehicle.Model.Hash).ToString("X8");
            return Path.Combine(_directory, modelHash + ".json");
        }

        private static int GetModKitIndexForConfig(Vehicle vehicle)
        {
            var count = Function.Call<int>(Hash.GET_NUM_MOD_KITS, vehicle.Handle);
            if (count <= 0)
            {
                return -1;
            }

            return NormalizeModKitIndex(Function.Call<int>(Hash.GET_VEHICLE_MOD_KIT, vehicle.Handle), count);
        }

        private static int NormalizeModKitIndex(int modKit, int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            return modKit >= 0 && modKit < count
                ? modKit
                : 0;
        }

        private static bool IsToggleModType(int modType)
        {
            return modType == 18 || modType == 20 || modType == 22;
        }
    }

    internal sealed class VehicleTuningConfig
    {
        public int Version { get; set; }
        public int ModelHash { get; set; }
        public int ModKit { get; set; }
        public int WheelType { get; set; }
        public int WindowTint { get; set; }
        public int PlateType { get; set; }
        public string PlateText { get; set; }
        public int PrimaryColor { get; set; }
        public int SecondaryColor { get; set; }
        public int PearlescentColor { get; set; }
        public int WheelColor { get; set; }
        public int XenonColor { get; set; }
        public int Livery { get; set; }
        public int Livery2 { get; set; }
        public bool BulletproofTires { get; set; }
        public int NeonRed { get; set; }
        public int NeonGreen { get; set; }
        public int NeonBlue { get; set; }
        public int TireSmokeRed { get; set; }
        public int TireSmokeGreen { get; set; }
        public int TireSmokeBlue { get; set; }
        public bool NitroEnabled { get; set; } = false;
        public int NitroFlameMode { get; set; } = 0;
        public bool WindowsDown { get; set; } = false;
        public int ConvertibleRoofState { get; set; } = 0;
        public bool KeepEngineRunning { get; set; } = false;
        public bool ForcedBrakeLights { get; set; } = false;
        public List<bool> Neons { get; set; } = new List<bool>();
        public List<VehicleModConfig> Mods { get; set; } = new List<VehicleModConfig>();
        public List<VehicleExtraConfig> Extras { get; set; } = new List<VehicleExtraConfig>();
    }

    internal sealed class VehicleModConfig
    {
        public int Type { get; set; }
        public int Value { get; set; }
        public bool Variation { get; set; }
        public bool IsToggle { get; set; }
        public bool ToggleValue { get; set; }
    }

    internal sealed class VehicleExtraConfig
    {
        public int Id { get; set; }
        public bool Enabled { get; set; }
    }
}
