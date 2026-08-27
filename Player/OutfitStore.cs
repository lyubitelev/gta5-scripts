using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GTA;
using GTA.Native;
using gta.Core;

namespace gta.Player
{
    public class OutfitSlotItem
    {
        public int Id { get; set; }
        public int Drawable { get; set; }
        public int Texture { get; set; }
    }

    public class SavedOutfit
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; }
        public int ModelHash { get; set; }
        public string ModelName { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public List<OutfitSlotItem> Components { get; set; } = new List<OutfitSlotItem>();
        public List<OutfitSlotItem> Props { get; set; } = new List<OutfitSlotItem>();
    }

    internal sealed class OutfitStore
    {
        private readonly string _filePath;
        private List<SavedOutfit> _outfits = new List<SavedOutfit>();
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public OutfitStore(string filePath = null)
        {
            _filePath = filePath ?? ScriptPaths.OutfitsPath;
            Load();
        }

        public IReadOnlyList<SavedOutfit> Outfits => _outfits;

        public void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    _outfits = JsonSerializer.Deserialize<List<SavedOutfit>>(json, JsonOptions) ?? new List<SavedOutfit>();
                }
                else
                {
                    _outfits = new List<SavedOutfit>();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Log("OUTFIT", $"Failed to load outfits: {ex.Message}");
                _outfits = new List<SavedOutfit>();
            }
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(_outfits, JsonOptions);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                ModLogger.Log("OUTFIT", $"Failed to save outfits: {ex.Message}");
            }
        }

        public SavedOutfit SaveCurrentOutfit(Ped ped, string customName = null)
        {
            if (ped == null || !ped.Exists()) return null;

            int modelHash = ped.Model.Hash;
            string modelName = GetFriendlyModelName(ped.Model);

            var outfit = new SavedOutfit
            {
                ModelHash = modelHash,
                ModelName = modelName,
                CreatedAtUtc = DateTime.UtcNow,
                Name = customName ?? $"Наряд #{_outfits.Count + 1} ({modelName}) - {DateTime.Now:dd.MM HH:mm}"
            };

            // Capture all components (0 to 11)
            for (int compId = 0; compId <= 11; compId++)
            {
                int drawable = Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, ped.Handle, compId);
                int texture = Function.Call<int>(Hash.GET_PED_TEXTURE_VARIATION, ped.Handle, compId);
                outfit.Components.Add(new OutfitSlotItem
                {
                    Id = compId,
                    Drawable = drawable,
                    Texture = texture
                });
            }

            // Capture all props (0 to 7)
            for (int propId = 0; propId <= 7; propId++)
            {
                int propIndex = Function.Call<int>(Hash.GET_PED_PROP_INDEX, ped.Handle, propId);
                int propTexture = Function.Call<int>(Hash.GET_PED_PROP_TEXTURE_INDEX, ped.Handle, propId);
                outfit.Props.Add(new OutfitSlotItem
                {
                    Id = propId,
                    Drawable = propIndex,
                    Texture = propTexture
                });
            }

            _outfits.Add(outfit);
            Save();
            ModLogger.Log("OUTFIT", $"Saved outfit '{outfit.Name}' (ID: {outfit.Id})");
            return outfit;
        }

        public bool ApplyOutfit(Ped ped, SavedOutfit outfit)
        {
            if (ped == null || !ped.Exists() || outfit == null) return false;

            // If model is different, change model first
            if (ped.Model.Hash != outfit.ModelHash)
            {
                var model = new Model(outfit.ModelHash);
                if (model.IsInCdImage && model.Request(1000))
                {
                    Game.Player.ChangeModel(model);
                    model.MarkAsNoLongerNeeded();
                    ped = Game.Player.Character;
                }
            }

            // Clear all props first
            for (int propId = 0; propId <= 7; propId++)
            {
                Function.Call(Hash.CLEAR_PED_PROP, ped.Handle, propId);
            }

            // Apply components
            if (outfit.Components != null)
            {
                foreach (var comp in outfit.Components)
                {
                    if (comp.Drawable >= 0)
                    {
                        Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped.Handle, comp.Id, comp.Drawable, comp.Texture, 0);
                    }
                }
            }

            // Apply props
            if (outfit.Props != null)
            {
                foreach (var prop in outfit.Props)
                {
                    if (prop.Drawable >= 0)
                    {
                        Function.Call(Hash.SET_PED_PROP_INDEX, ped.Handle, prop.Id, prop.Drawable, prop.Texture, true);
                    }
                }
            }

            ModLogger.Log("OUTFIT", $"Applied outfit '{outfit.Name}'");
            return true;
        }

        public bool DeleteOutfit(string outfitId)
        {
            int removed = _outfits.RemoveAll(o => o.Id == outfitId);
            if (removed > 0)
            {
                Save();
                return true;
            }
            return false;
        }

        private static string GetFriendlyModelName(Model model)
        {
            if (model == new Model("mp_m_freemode_01")) return "MP Мужчина";
            if (model == new Model("mp_f_freemode_01")) return "MP Женщина";
            if (model == new Model(PedHash.Michael)) return "Майкл";
            if (model == new Model(PedHash.Franklin)) return "Франклин";
            if (model == new Model(PedHash.Trevor)) return "Тревор";
            if (model == new Model(PedHash.Cop01SMY)) return "Офицер LSPD";
            if (model == new Model(PedHash.Swat01SMY)) return "NOOSE Спецназ";
            return "Персонаж";
        }
    }
}
