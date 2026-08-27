using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GTA;
using GTA.Native;
using gta.Core;

namespace gta.Player
{
    internal sealed class WeaponConfigStore
    {
        private const int Version = 1;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private readonly string _directory;

        public WeaponConfigStore(string directory)
        {
            _directory = directory;
            Directory.CreateDirectory(directory);
        }

        public string GetPath(int weaponHash)
        {
            return Path.Combine(_directory, $"weapon_{weaponHash:X8}.json");
        }

        public bool HasConfig(int weaponHash)
        {
            return File.Exists(GetPath(weaponHash));
        }

        public void Save(Ped character, int weaponHash, IEnumerable<WeaponComponentHash> allMenuComponents)
        {
            var config = Capture(character, weaponHash, allMenuComponents);
            File.WriteAllText(GetPath(weaponHash), JsonSerializer.Serialize(config, JsonOptions));
        }

        public static WeaponCustomizationConfig Capture(Ped character, int weaponHash, IEnumerable<WeaponComponentHash> allMenuComponents)
        {
            var config = new WeaponCustomizationConfig
            {
                Version = Version,
                WeaponHash = weaponHash,
                TintIndex = Function.Call<int>(Hash.GET_PED_WEAPON_TINT_INDEX, character.Handle, weaponHash),
                ComponentHashes = new List<int>()
            };

            foreach (var component in allMenuComponents)
            {
                var componentHash = unchecked((int)(uint)component);
                if (Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON_COMPONENT, character.Handle, weaponHash, componentHash))
                {
                    config.ComponentHashes.Add(componentHash);
                }
            }

            return config;
        }

        public bool Apply(Ped character, int weaponHash, IEnumerable<WeaponComponentHash> allMenuComponents)
        {
            var path = GetPath(weaponHash);
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                var config = JsonSerializer.Deserialize<WeaponCustomizationConfig>(File.ReadAllText(path));
                if (config == null || config.WeaponHash != weaponHash)
                {
                    return false;
                }

                Apply(character, weaponHash, config, allMenuComponents);
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Log("WEAPON", $"Error applying weapon config {weaponHash:X8}: {ex.Message}");
                return false;
            }
        }

        public static void Apply(Ped character, int weaponHash, WeaponCustomizationConfig config, IEnumerable<WeaponComponentHash> allMenuComponents)
        {
            if (config == null) return;

            // Remove all current menu components that the weapon takes to have a clean slate
            foreach (var comp in allMenuComponents)
            {
                var compHash = unchecked((int)(uint)comp);
                if (Function.Call<bool>(Hash.DOES_WEAPON_TAKE_WEAPON_COMPONENT, weaponHash, compHash) &&
                    Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON_COMPONENT, character.Handle, weaponHash, compHash))
                {
                    Function.Call(Hash.REMOVE_WEAPON_COMPONENT_FROM_PED, character.Handle, weaponHash, compHash);
                }
            }

            // Add saved components
            if (config.ComponentHashes != null)
            {
                foreach (var compHash in config.ComponentHashes)
                {
                    if (Function.Call<bool>(Hash.DOES_WEAPON_TAKE_WEAPON_COMPONENT, weaponHash, compHash))
                    {
                        Function.Call(Hash.GIVE_WEAPON_COMPONENT_TO_PED, character.Handle, weaponHash, compHash);
                    }
                }
            }

            // Set tint
            if (config.TintIndex >= 0)
            {
                Function.Call(Hash.SET_PED_WEAPON_TINT_INDEX, character.Handle, weaponHash, config.TintIndex);
            }
        }
    }

    internal sealed class WeaponCustomizationConfig
    {
        public int Version { get; set; }
        public int WeaponHash { get; set; }
        public int TintIndex { get; set; }
        public List<int> ComponentHashes { get; set; } = new List<int>();
    }
}
