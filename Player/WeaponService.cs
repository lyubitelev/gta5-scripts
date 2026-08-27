using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using gta.Core;

namespace gta.Player
{
    internal sealed class WeaponService
    {
        private const int ItemsPerPage = 10;
        private const int DefaultAmmo = 9999;

        private static readonly int UnarmedHash = GetHashKey("WEAPON_UNARMED");

        private static readonly string[] OnlineWeaponNames =
        {
            "WEAPON_ACIDPACKAGE",
            "WEAPON_AUTOSHOTGUN",
            "WEAPON_BATTLEAXE",
            "WEAPON_BATTLERIFLE",
            "WEAPON_CANDYCANE",
            "WEAPON_CERAMICPISTOL",
            "WEAPON_COMBATMG_MK2",
            "WEAPON_COMBATPDW",
            "WEAPON_COMBATSHOTGUN",
            "WEAPON_COMPACTLAUNCHER",
            "WEAPON_COMPACTRIFLE",
            "WEAPON_DBSHOTGUN",
            "WEAPON_DOUBLEACTION",
            "WEAPON_EMPLAUNCHER",
            "WEAPON_FERTILIZERCAN",
            "WEAPON_FIREWORK",
            "WEAPON_GADGETPISTOL",
            "WEAPON_HAZARDCAN",
            "WEAPON_HEAVYRIFLE",
            "WEAPON_HEAVYSNIPER_MK2",
            "WEAPON_HOMINGLAUNCHER",
            "WEAPON_KNUCKLE",
            "WEAPON_MACHINEPISTOL",
            "WEAPON_MARKSMANPISTOL",
            "WEAPON_MARKSMANRIFLE_MK2",
            "WEAPON_MILITARYRIFLE",
            "WEAPON_MINISMG",
            "WEAPON_NAVYREVOLVER",
            "WEAPON_PIPEBOMB",
            "WEAPON_POOLCUE",
            "WEAPON_PRECISIONRIFLE",
            "WEAPON_PROXMINE",
            "WEAPON_PUMPSHOTGUN_MK2",
            "WEAPON_RAILGUNXM3",
            "WEAPON_RAYCARBINE",
            "WEAPON_RAYMINIGUN",
            "WEAPON_RAYPISTOL",
            "WEAPON_REVOLVER",
            "WEAPON_REVOLVER_MK2",
            "WEAPON_SERVICECARBINE",
            "WEAPON_SMG_MK2",
            "WEAPON_SNOWLAUNCHER",
            "WEAPON_SNSPISTOL_MK2",
            "WEAPON_STONE_HATCHET",
            "WEAPON_STUNGUN_MP",
            "WEAPON_SWITCHBLADE",
            "WEAPON_TACTICALRIFLE",
            "WEAPON_TECPISTOL",
            "WEAPON_UNHOLYHELLBRINGER",
            "WEAPON_UPNATOMIZER",
            "WEAPON_WIDOWMAKER",
            "WEAPON_WM29PISTOL",
            "WEAPON_WRENCH"
        };

        private static readonly int[] OnlineWeaponHashes = OnlineWeaponNames
            .Select(GetHashKey)
            .ToArray();

        private static readonly WeaponConversion[] Mk2Conversions =
        {
            new WeaponConversion("Pistol Mk II", "WEAPON_PISTOL", "WEAPON_PISTOL_MK2"),
            new WeaponConversion("SNS Pistol Mk II", "WEAPON_SNSPISTOL", "WEAPON_SNSPISTOL_MK2"),
            new WeaponConversion("Revolver Mk II", "WEAPON_REVOLVER", "WEAPON_REVOLVER_MK2"),
            new WeaponConversion("SMG Mk II", "WEAPON_SMG", "WEAPON_SMG_MK2"),
            new WeaponConversion("Assault Rifle Mk II", "WEAPON_ASSAULTRIFLE", "WEAPON_ASSAULTRIFLE_MK2"),
            new WeaponConversion("Carbine Rifle Mk II", "WEAPON_CARBINERIFLE", "WEAPON_CARBINERIFLE_MK2"),
            new WeaponConversion("Special Carbine Mk II", "WEAPON_SPECIALCARBINE", "WEAPON_SPECIALCARBINE_MK2"),
            new WeaponConversion("Bullpup Rifle Mk II", "WEAPON_BULLPUPRIFLE", "WEAPON_BULLPUPRIFLE_MK2"),
            new WeaponConversion("Combat MG Mk II", "WEAPON_COMBATMG", "WEAPON_COMBATMG_MK2"),
            new WeaponConversion("Pump Shotgun Mk II", "WEAPON_PUMPSHOTGUN", "WEAPON_PUMPSHOTGUN_MK2"),
            new WeaponConversion("Heavy Sniper Mk II", "WEAPON_HEAVYSNIPER", "WEAPON_HEAVYSNIPER_MK2"),
            new WeaponConversion("Marksman Rifle Mk II", "WEAPON_MARKSMANRIFLE", "WEAPON_MARKSMANRIFLE_MK2")
        };

        private static readonly WeaponComponentHash[] MenuComponents =
            Enum.GetValues(typeof(WeaponComponentHash))
                .Cast<WeaponComponentHash>()
                .Where(IsMenuComponent)
                .ToArray();

        private static readonly WeaponComponentHash[] MaxUpgradeComponents = MenuComponents
            .Where(IsMaxUpgradeComponent)
            .ToArray();

        private static readonly ComponentGroup[] ComponentGroupOrder =
        {
            ComponentGroup.Magazine,
            ComponentGroup.Scope,
            ComponentGroup.Flashlight,
            ComponentGroup.Grip,
            ComponentGroup.Barrel,
            ComponentGroup.Muzzle,
            ComponentGroup.Camo,
            ComponentGroup.SlideCamo,
            ComponentGroup.Variant,
            ComponentGroup.Other
        };

        private static readonly WeaponConfigStore ConfigStore =
            new WeaponConfigStore(ScriptPaths.WeaponConfigsDirectory);

        private readonly WeaponDefinition[] _weaponDefinitions;

        private bool _isMenuVisible;
        private WeaponCategory _category = WeaponCategory.None;
        private readonly Dictionary<WeaponCategory, MenuSelection> _savedSelections =
            new Dictionary<WeaponCategory, MenuSelection>();
        private int _categoryIndex;
        private int _page;
        private int _index;

        public WeaponService(WeaponHash[] weaponHashes)
        {
            _weaponDefinitions = BuildWeaponCatalog(weaponHashes);
        }

        public bool IsMenuVisible
        {
            get { return _isMenuVisible; }
        }

        public void ToggleMenu()
        {
            var character = GetPlayerCharacter();
            if (character == null)
            {
                Notifier.Show("Игрок недоступен");
                return;
            }

            _isMenuVisible = !_isMenuVisible;
            Notifier.Show(_isMenuVisible ? "Меню оружия открыто" : "Меню оружия закрыто");
        }

        public void Draw()
        {
            if (!_isMenuVisible)
            {
                return;
            }

            var character = GetPlayerCharacter();
            if (character == null)
            {
                _isMenuVisible = false;
                return;
            }

            if (_category == WeaponCategory.None)
            {
                DrawCategories(character);
                return;
            }

            var options = BuildOptions(character, _category);
            if (options.Count == 0)
            {
                ReturnToCategories();
                return;
            }

            ClampState(options.Count);

            if (_category == WeaponCategory.Components)
            {
                DrawUnpagedOptions(character, options);
                return;
            }

            var startIndex = _page * ItemsPerPage;
            var endIndex = Math.Min(startIndex + ItemsPerPage, options.Count);
            var pageCount = Math.Max(1, (options.Count + ItemsPerPage - 1) / ItemsPerPage);
            var text = "Оружие: " + GetCategoryName(_category) + "\n";

            for (var i = startIndex; i < endIndex; i++)
            {
                var option = options[i];
                var marker = i == _index ? "> " : "  ";
                text += marker + option.Name + GetOptionValueText(character, option) + "\n";
            }

            text += "\nСтраница " + (_page + 1) + "/" + pageCount;
            text += "\n8/2 - выбор  7/9 - страницы  4/6 - изменить  5 - применить  0 - назад";

            MenuPanelRenderer.Draw(text, new PointF(10, 10), 0.42f);
        }

        private void DrawUnpagedOptions(Ped character, List<WeaponOption> options)
        {
            ClampUnpagedState(options.Count);

            var text = "Оружие: " + GetCategoryName(_category) + "\n";
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var marker = i == _index ? "> " : "  ";
                text += marker + option.Name + GetOptionValueText(character, option) + "\n";
            }

            text += "\n8/2 - выбор  4/6 - изменить  5 - применить  0 - назад";

            MenuPanelRenderer.Draw(text, new PointF(10, 10), 0.42f);
        }

        public void Handle(KeyEventArgs e)
        {
            if (!_isMenuVisible)
            {
                return;
            }

            var character = GetPlayerCharacter();
            if (character == null)
            {
                _isMenuVisible = false;
                return;
            }

            if (_category == WeaponCategory.None)
            {
                HandleCategories(character, e);
                return;
            }

            var options = BuildOptions(character, _category);
            if (options.Count == 0)
            {
                ReturnToCategories();
                return;
            }

            ClampState(options.Count);

            if (_category == WeaponCategory.Components)
            {
                HandleUnpagedOptions(character, options, e);
                return;
            }

            var startIndex = _page * ItemsPerPage;
            var endIndex = Math.Min(startIndex + ItemsPerPage, options.Count);

            switch (e.KeyCode)
            {
                case Keys.L:
                case Keys.X:
                case Keys.Escape:
                    _isMenuVisible = false;
                    Notifier.Show("Меню оружия закрыто");
                    break;
                case Keys.NumPad0:
                case Keys.Back:
                    ReturnToCategories();
                    break;
                case Keys.NumPad8:
                    _index = _index == startIndex ? endIndex - 1 : _index - 1;
                    break;
                case Keys.NumPad2:
                    _index = _index + 1 >= endIndex ? startIndex : _index + 1;
                    break;
                case Keys.NumPad7:
                    if (_page > 0)
                    {
                        _page--;
                        _index = _page * ItemsPerPage;
                    }
                    break;
                case Keys.NumPad9:
                    if (_page < GetMaxPage(options.Count))
                    {
                        _page++;
                        _index = _page * ItemsPerPage;
                    }
                    break;
                case Keys.NumPad4:
                    ChangeOption(character, options[_index], -1);
                    break;
                case Keys.NumPad6:
                    ChangeOption(character, options[_index], 1);
                    break;
                case Keys.NumPad5:
                    ApplyOption(character, options[_index]);
                    break;
            }

            if (_category != WeaponCategory.None)
            {
                ClampState(BuildOptions(character, _category).Count);
            }
        }

        private void HandleUnpagedOptions(Ped character, List<WeaponOption> options, KeyEventArgs e)
        {
            ClampUnpagedState(options.Count);

            switch (e.KeyCode)
            {
                case Keys.L:
                case Keys.X:
                case Keys.Escape:
                    _isMenuVisible = false;
                    Notifier.Show("Меню оружия закрыто");
                    break;
                case Keys.NumPad0:
                case Keys.Back:
                    ReturnToCategories();
                    break;
                case Keys.NumPad8:
                    _index = _index == 0 ? options.Count - 1 : _index - 1;
                    break;
                case Keys.NumPad2:
                    _index = _index + 1 >= options.Count ? 0 : _index + 1;
                    break;
                case Keys.NumPad4:
                    ChangeOption(character, options[_index], -1);
                    break;
                case Keys.NumPad6:
                    ChangeOption(character, options[_index], 1);
                    break;
                case Keys.NumPad5:
                    ApplyOption(character, options[_index]);
                    break;
            }
        }

        public void GiveAllWeapons()
        {
            var character = GetPlayerCharacter();
            if (character == null)
            {
                return;
            }

            var weaponHashes = new HashSet<int>();
            foreach (var weapon in _weaponDefinitions)
            {
                if (!IsWeaponValid(weapon.Hash))
                {
                    continue;
                }

                GiveWeapon(character, weapon.Hash, false);
                weaponHashes.Add(weapon.Hash);
            }

            foreach (var weaponHash in OnlineWeaponHashes)
            {
                if (!IsWeaponValid(weaponHash))
                {
                    continue;
                }

                GiveWeapon(character, weaponHash, false);
                weaponHashes.Add(weaponHash);
            }

            foreach (var weaponHash in weaponHashes)
            {
                if (ConfigStore.HasConfig(weaponHash))
                {
                    ConfigStore.Apply(character, weaponHash, MenuComponents);
                }
                else
                {
                    ApplyMaxUpgrades(character, weaponHash);
                    SetMaxTint(character, weaponHash);
                }

                RefillWeapon(character, weaponHash);
            }

            Notifier.Show("Получил online-оружие (с вашими конфигами/max)");
        }

        private void DrawCategories(Ped character)
        {
            var categories = BuildCategories(character);
            if (categories.Count == 0)
            {
                return;
            }

            ClampCategoryState(categories.Count);

            var text = "Оружие\n";
            for (var i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                var marker = i == _categoryIndex ? "> " : "  ";
                text += marker + category.Name + " [" + category.StatusText + "]\n";
            }

            text += "\n8/2 - выбор  5 - открыть  0 - закрыть";

            MenuPanelRenderer.Draw(text, new PointF(10, 10), 0.42f);
        }

        private void HandleCategories(Ped character, KeyEventArgs e)
        {
            var categories = BuildCategories(character);
            if (categories.Count == 0)
            {
                return;
            }

            ClampCategoryState(categories.Count);

            switch (e.KeyCode)
            {
                case Keys.L:
                case Keys.X:
                case Keys.NumPad0:
                case Keys.Back:
                case Keys.Escape:
                    _isMenuVisible = false;
                    Notifier.Show("Меню оружия закрыто");
                    break;
                case Keys.NumPad8:
                    _categoryIndex = _categoryIndex == 0 ? categories.Count - 1 : _categoryIndex - 1;
                    break;
                case Keys.NumPad2:
                    _categoryIndex = _categoryIndex + 1 >= categories.Count ? 0 : _categoryIndex + 1;
                    break;
                case Keys.NumPad5:
                    if (!categories[_categoryIndex].IsEnabled)
                    {
                        Notifier.Show(GetDisabledCategoryText(character, categories[_categoryIndex].Kind));
                        break;
                    }

                    _category = categories[_categoryIndex].Kind;
                    LoadSelection(_category);
                    break;
            }
        }

        private List<WeaponCategoryDefinition> BuildCategories(Ped character)
        {
            var categories = new List<WeaponCategoryDefinition>();

            AddCategory(character, categories, WeaponCategory.Quick);
            AddCategory(character, categories, WeaponCategory.Weapons);
            AddCategory(character, categories, WeaponCategory.Mk2);
            AddCategory(character, categories, WeaponCategory.Components);
            AddCategory(character, categories, WeaponCategory.Tint);

            return categories;
        }

        private void AddCategory(Ped character, ICollection<WeaponCategoryDefinition> categories, WeaponCategory category)
        {
            var count = BuildOptions(character, category).Count;
            var enabled = count > 0;
            var status = enabled ? count.ToString() : "нет";

            if (category == WeaponCategory.Components || category == WeaponCategory.Tint)
            {
                var currentWeapon = GetCurrentWeapon(character);
                if (!IsUsableWeapon(currentWeapon) || !HasWeapon(character, currentWeapon))
                {
                    status = "нет текущего";
                }
            }

            categories.Add(new WeaponCategoryDefinition(category, GetCategoryName(category), status, enabled));
        }

        private List<WeaponOption> BuildOptions(Ped character, WeaponCategory category)
        {
            var options = new List<WeaponOption>();

            switch (category)
            {
                case WeaponCategory.Quick:
                    options.Add(WeaponOption.CreateCommand("Выдать online + max (или свои)", WeaponCommand.GiveAllMax));

                    var currentWeapon = GetCurrentWeapon(character);
                    if (IsUsableWeapon(currentWeapon) && HasWeapon(character, currentWeapon))
                    {
                        options.Add(WeaponOption.CreateCommand("Сохранить конфиг текущего", WeaponCommand.SaveCurrentConfig));
                        if (ConfigStore.HasConfig(currentWeapon))
                        {
                            options.Add(WeaponOption.CreateCommand("Применить сохраненный конфиг", WeaponCommand.ApplySavedConfig));
                        }

                        options.Add(WeaponOption.CreateCommand("Применить все сохраненные конфиги", WeaponCommand.ApplyAllConfigs));
                        options.Add(WeaponOption.CreateCommand("Макс. апгрейд текущего", WeaponCommand.MaxCurrent));
                        options.Add(WeaponOption.CreateCommand("Патроны текущего", WeaponCommand.RefillCurrent));
                    }
                    else
                    {
                        options.Add(WeaponOption.CreateCommand("Применить все сохраненные конфиги", WeaponCommand.ApplyAllConfigs));
                    }

                    options.Add(WeaponOption.CreateCommand("Убрать все оружие", WeaponCommand.RemoveAll));
                    break;
                case WeaponCategory.Weapons:
                    foreach (var weapon in _weaponDefinitions)
                    {
                        if (IsWeaponValid(weapon.Hash))
                        {
                            options.Add(WeaponOption.Weapon(weapon.Name, weapon.Hash));
                        }
                    }

                    break;
                case WeaponCategory.Mk2:
                    foreach (var conversion in Mk2Conversions)
                    {
                        if (IsWeaponValid(conversion.TargetHash))
                        {
                            options.Add(WeaponOption.Mk2Conversion("Переоборудовать в " + conversion.DisplayName, conversion.SourceHash, conversion.TargetHash));
                        }
                    }

                    break;
                case WeaponCategory.Components:
                    AddComponentOptions(character, options);
                    break;
                case WeaponCategory.Tint:
                    AddTintOptions(character, options);
                    break;
            }

            return options;
        }

        private static void AddComponentOptions(Ped character, ICollection<WeaponOption> options)
        {
            var weaponHash = GetCurrentWeapon(character);
            if (!IsUsableWeapon(weaponHash) || !HasWeapon(character, weaponHash))
            {
                return;
            }

            foreach (var group in ComponentGroupOrder)
            {
                if (GetCompatibleComponents(weaponHash, group).Count == 0)
                {
                    continue;
                }

                options.Add(WeaponOption.Component(GetComponentGroupName(group), weaponHash, group));
            }
        }

        private static void AddTintOptions(Ped character, ICollection<WeaponOption> options)
        {
            var weaponHash = GetCurrentWeapon(character);
            if (!IsUsableWeapon(weaponHash) || !HasWeapon(character, weaponHash))
            {
                return;
            }

            var count = Function.Call<int>(Hash.GET_WEAPON_TINT_COUNT, weaponHash);
            if (count > 0)
            {
                options.Add(WeaponOption.Range("Цвет оружия", WeaponOptionKind.Tint, weaponHash, 0, count - 1));
            }
        }

        private string GetOptionValueText(Ped character, WeaponOption option)
        {
            switch (option.Kind)
            {
                case WeaponOptionKind.Command:
                    return "";
                case WeaponOptionKind.Weapon:
                    if (GetCurrentWeapon(character) == option.WeaponHash)
                    {
                        return " [текущее]";
                    }

                    return " [" + (HasWeapon(character, option.WeaponHash) ? "есть" : "нет") + "]";
                case WeaponOptionKind.Mk2Conversion:
                    if (HasWeapon(character, option.TargetHash))
                    {
                        return " [есть]";
                    }

                    return " [" + (HasWeapon(character, option.WeaponHash) ? "готово" : "нет") + "]";
                case WeaponOptionKind.Component:
                    return " [" + GetComponentGroupValueText(character, option.WeaponHash, option.ComponentGroup) + "]";
                case WeaponOptionKind.Tint:
                    return " [" + Function.Call<int>(Hash.GET_PED_WEAPON_TINT_INDEX, character.Handle, option.WeaponHash) + "/" + option.Max + "]";
                default:
                    return "";
            }
        }

        private void ApplyOption(Ped character, WeaponOption option)
        {
            switch (option.Kind)
            {
                case WeaponOptionKind.Command:
                    RunCommand(character, option.Command);
                    break;
                case WeaponOptionKind.Weapon:
                    if (!HasWeapon(character, option.WeaponHash))
                    {
                        GiveWeapon(character, option.WeaponHash, true);
                        Notifier.Show("Оружие выдано: " + option.Name);
                        break;
                    }

                    SelectWeapon(character, option.WeaponHash);
                    break;
                case WeaponOptionKind.Mk2Conversion:
                    ConvertToMk2(character, option);
                    break;
                case WeaponOptionKind.Component:
                    CycleComponentGroup(character, option.WeaponHash, option.ComponentGroup, 1);
                    break;
                case WeaponOptionKind.Tint:
                    ChangeOption(character, option, 1);
                    break;
            }
        }

        private void ChangeOption(Ped character, WeaponOption option, int direction)
        {
            switch (option.Kind)
            {
                case WeaponOptionKind.Weapon:
                    if (direction < 0)
                    {
                        RemoveWeapon(character, option.WeaponHash);
                        Notifier.Show("Оружие убрано: " + option.Name);
                        break;
                    }

                    GiveWeapon(character, option.WeaponHash, true);
                    Notifier.Show("Оружие выдано: " + option.Name);
                    break;
                case WeaponOptionKind.Mk2Conversion:
                    if (direction < 0)
                    {
                        RemoveWeapon(character, option.TargetHash);
                        break;
                    }

                    ConvertToMk2(character, option);
                    break;
                case WeaponOptionKind.Component:
                    CycleComponentGroup(character, option.WeaponHash, option.ComponentGroup, direction);
                    break;
                case WeaponOptionKind.Tint:
                    var currentTint = Function.Call<int>(Hash.GET_PED_WEAPON_TINT_INDEX, character.Handle, option.WeaponHash);
                    Function.Call(Hash.SET_PED_WEAPON_TINT_INDEX, character.Handle, option.WeaponHash, Wrap(currentTint + direction, option.Min, option.Max));
                    break;
            }
        }

        private void RunCommand(Ped character, WeaponCommand command)
        {
            switch (command)
            {
                case WeaponCommand.GiveAllMax:
                    GiveAllWeapons();
                    break;
                case WeaponCommand.SaveCurrentConfig:
                    var saveWeapon = GetCurrentWeapon(character);
                    if (IsUsableWeapon(saveWeapon) && HasWeapon(character, saveWeapon))
                    {
                        ConfigStore.Save(character, saveWeapon, MenuComponents);
                        Notifier.Show("Конфиг оружия сохранен");
                    }
                    break;
                case WeaponCommand.ApplySavedConfig:
                    var applyWeapon = GetCurrentWeapon(character);
                    if (IsUsableWeapon(applyWeapon) && HasWeapon(character, applyWeapon))
                    {
                        if (ConfigStore.Apply(character, applyWeapon, MenuComponents))
                        {
                            RefillWeapon(character, applyWeapon);
                            Notifier.Show("Сохраненный конфиг применен");
                        }
                        else
                        {
                            Notifier.Show("Нет сохраненного конфига для этого оружия");
                        }
                    }
                    break;
                case WeaponCommand.ApplyAllConfigs:
                    var appliedCount = 0;
                    foreach (var def in _weaponDefinitions)
                    {
                        if (HasWeapon(character, def.Hash) && ConfigStore.Apply(character, def.Hash, MenuComponents))
                        {
                            RefillWeapon(character, def.Hash);
                            appliedCount++;
                        }
                    }
                    foreach (var hash in OnlineWeaponHashes)
                    {
                        if (HasWeapon(character, hash) && ConfigStore.Apply(character, hash, MenuComponents))
                        {
                            RefillWeapon(character, hash);
                            appliedCount++;
                        }
                    }
                    Notifier.Show($"Применено сохраненных конфигов: {appliedCount}");
                    break;
                case WeaponCommand.MaxCurrent:
                    var weaponHash = GetCurrentWeapon(character);
                    if (IsUsableWeapon(weaponHash) && HasWeapon(character, weaponHash))
                    {
                        ApplyMaxUpgrades(character, weaponHash);
                        SetMaxTint(character, weaponHash);
                        RefillWeapon(character, weaponHash);
                        Notifier.Show("Текущее оружие улучшено");
                    }

                    break;
                case WeaponCommand.RefillCurrent:
                    RefillWeapon(character, GetCurrentWeapon(character));
                    Notifier.Show("Патроны пополнены");
                    break;
                case WeaponCommand.RemoveAll:
                    Function.Call(Hash.REMOVE_ALL_PED_WEAPONS, character.Handle, true);
                    Notifier.Show("Оружие убрано");
                    break;
            }
        }

        private static void ConvertToMk2(Ped character, WeaponOption option)
        {
            if (!IsWeaponValid(option.TargetHash))
            {
                Notifier.Show("Mk II недоступен");
                return;
            }

            if (HasWeapon(character, option.WeaponHash))
            {
                RemoveWeapon(character, option.WeaponHash);
            }

            GiveWeapon(character, option.TargetHash, true);
            if (!ConfigStore.HasConfig(option.TargetHash))
            {
                ApplyMaxUpgrades(character, option.TargetHash);
                SetMaxTint(character, option.TargetHash);
            }

            RefillWeapon(character, option.TargetHash);
            Notifier.Show("Mk II готов");
        }

        private static void ApplyMaxUpgrades(Ped character, int weaponHash)
        {
            foreach (var component in MaxUpgradeComponents)
            {
                var componentHash = ToNativeHash(component);
                if (!Function.Call<bool>(Hash.DOES_WEAPON_TAKE_WEAPON_COMPONENT, weaponHash, componentHash))
                {
                    continue;
                }

                Function.Call(Hash.GIVE_WEAPON_COMPONENT_TO_PED, character.Handle, weaponHash, componentHash);
            }
        }

        private static void SetMaxTint(Ped character, int weaponHash)
        {
            var tintCount = Function.Call<int>(Hash.GET_WEAPON_TINT_COUNT, weaponHash);
            if (tintCount <= 0)
            {
                return;
            }

            Function.Call(Hash.SET_PED_WEAPON_TINT_INDEX, character.Handle, weaponHash, tintCount - 1);
        }

        private static void RefillWeapon(Ped character, int weaponHash)
        {
            if (!IsUsableWeapon(weaponHash) || !HasWeapon(character, weaponHash))
            {
                return;
            }

            Function.Call(Hash.SET_PED_AMMO, character.Handle, weaponHash, DefaultAmmo);
            RefillWeaponAmmoType(character, weaponHash);
            RefillWeaponClip(character, weaponHash);
        }

        private static void RefillWeaponAmmoType(Ped character, int weaponHash)
        {
            var ammoType = Function.Call<int>(Hash.GET_PED_AMMO_TYPE_FROM_WEAPON, character.Handle, weaponHash);
            if (ammoType != 0)
            {
                Function.Call(Hash.SET_PED_AMMO_BY_TYPE, character.Handle, ammoType, DefaultAmmo);
            }

            var originalAmmoType = Function.Call<int>(Hash.GET_PED_ORIGINAL_AMMO_TYPE_FROM_WEAPON, character.Handle, weaponHash);
            if (originalAmmoType != 0 && originalAmmoType != ammoType)
            {
                Function.Call(Hash.SET_PED_AMMO_BY_TYPE, character.Handle, originalAmmoType, DefaultAmmo);
            }
        }

        private static void RefillWeaponClip(Ped character, int weaponHash)
        {
            var maxClipAmmo = Function.Call<int>(Hash.GET_MAX_AMMO_IN_CLIP, character.Handle, weaponHash, true);
            if (maxClipAmmo <= 0)
            {
                return;
            }

            Function.Call(Hash.SET_AMMO_IN_CLIP, character.Handle, weaponHash, maxClipAmmo);
        }

        private static bool HasComponent(Ped character, int weaponHash, int componentHash)
        {
            return Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON_COMPONENT, character.Handle, weaponHash, componentHash);
        }

        private static string GetComponentGroupValueText(Ped character, int weaponHash, ComponentGroup group)
        {
            var components = GetCompatibleComponents(weaponHash, group);
            var index = GetCurrentComponentGroupIndex(character, weaponHash, components);
            if (index <= 0)
            {
                return group == ComponentGroup.Flashlight ? "Выкл" : "Сток";
            }

            return FormatComponentValue(components[index - 1], group);
        }

        private static void CycleComponentGroup(Ped character, int weaponHash, ComponentGroup group, int direction)
        {
            var components = GetCompatibleComponents(weaponHash, group);
            if (components.Count == 0)
            {
                return;
            }

            var currentIndex = GetCurrentComponentGroupIndex(character, weaponHash, components);
            var nextIndex = Wrap(currentIndex + direction, 0, components.Count);

            foreach (var component in components)
            {
                var componentHash = ToNativeHash(component);
                if (HasComponent(character, weaponHash, componentHash))
                {
                    Function.Call(Hash.REMOVE_WEAPON_COMPONENT_FROM_PED, character.Handle, weaponHash, componentHash);
                }
            }

            if (nextIndex > 0)
            {
                Function.Call(Hash.GIVE_WEAPON_COMPONENT_TO_PED, character.Handle, weaponHash, ToNativeHash(components[nextIndex - 1]));
            }

            if (group == ComponentGroup.Magazine)
            {
                RefillWeapon(character, weaponHash);
            }
        }

        private static int GetCurrentComponentGroupIndex(Ped character, int weaponHash, IList<WeaponComponentHash> components)
        {
            for (var i = 0; i < components.Count; i++)
            {
                if (HasComponent(character, weaponHash, ToNativeHash(components[i])))
                {
                    return i + 1;
                }
            }

            return 0;
        }

        private static List<WeaponComponentHash> GetCompatibleComponents(int weaponHash, ComponentGroup group)
        {
            return MenuComponents
                .Where(component => GetComponentGroup(component) == group)
                .Where(component => !IsStockComponent(component))
                .Where(component => Function.Call<bool>(Hash.DOES_WEAPON_TAKE_WEAPON_COMPONENT, weaponHash, ToNativeHash(component)))
                .OrderBy(GetComponentSortKey)
                .ThenBy(component => component.ToString())
                .ToList();
        }

        private static bool HasWeapon(Ped character, int weaponHash)
        {
            return Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON, character.Handle, weaponHash, false);
        }

        private static void GiveWeapon(Ped character, int weaponHash, bool equipNow)
        {
            Function.Call(Hash.GIVE_WEAPON_TO_PED, character.Handle, weaponHash, DefaultAmmo, false, equipNow);
            if (ConfigStore.HasConfig(weaponHash))
            {
                ConfigStore.Apply(character, weaponHash, MenuComponents);
                RefillWeapon(character, weaponHash);
            }

            if (equipNow)
            {
                SelectWeapon(character, weaponHash);
            }
        }

        private static void SelectWeapon(Ped character, int weaponHash)
        {
            Function.Call(Hash.SET_CURRENT_PED_WEAPON, character.Handle, weaponHash, true);
        }

        private static void RemoveWeapon(Ped character, int weaponHash)
        {
            if (IsUsableWeapon(weaponHash))
            {
                Function.Call(Hash.REMOVE_WEAPON_FROM_PED, character.Handle, weaponHash);
            }
        }

        private string GetDisabledCategoryText(Ped character, WeaponCategory category)
        {
            if (category == WeaponCategory.Components || category == WeaponCategory.Tint)
            {
                var currentWeapon = GetCurrentWeapon(character);
                if (!IsUsableWeapon(currentWeapon) || !HasWeapon(character, currentWeapon))
                {
                    return "Сначала выбери оружие";
                }
            }

            return "Раздел недоступен";
        }

        private void ReturnToCategories()
        {
            SaveSelection();
            _category = WeaponCategory.None;
        }

        private void ResetMenuState()
        {
            ReturnToCategories();
        }

        private void ClampCategoryState(int itemCount)
        {
            if (itemCount <= 0)
            {
                _categoryIndex = 0;
                return;
            }

            _categoryIndex = Math.Min(_categoryIndex, itemCount - 1);
            _categoryIndex = Math.Max(_categoryIndex, 0);
        }

        private void ClampUnpagedState(int itemCount)
        {
            _page = 0;

            if (itemCount <= 0)
            {
                _index = 0;
                return;
            }

            _index = Math.Min(_index, itemCount - 1);
            _index = Math.Max(_index, 0);
        }

        private void SaveSelection()
        {
            if (_category == WeaponCategory.None)
            {
                return;
            }

            _savedSelections[_category] = new MenuSelection(_page, _index);
        }

        private void LoadSelection(WeaponCategory category)
        {
            MenuSelection selection;
            if (!_savedSelections.TryGetValue(category, out selection))
            {
                _page = 0;
                _index = 0;
                return;
            }

            _page = selection.Page;
            _index = selection.Index;
        }

        private void ClampState(int itemCount)
        {
            if (itemCount <= 0)
            {
                _page = 0;
                _index = 0;
                return;
            }

            _page = Math.Min(_page, GetMaxPage(itemCount));
            _index = Math.Min(_index, itemCount - 1);
            _index = Math.Max(_index, _page * ItemsPerPage);
        }

        private static int GetMaxPage(int itemCount)
        {
            return itemCount == 0
                ? 0
                : (itemCount - 1) / ItemsPerPage;
        }

        private static Ped GetPlayerCharacter()
        {
            var character = Game.Player.Character;
            return character != null && character.Exists()
                ? character
                : null;
        }

        private static int GetCurrentWeapon(Ped character)
        {
            return Function.Call<int>(Hash.GET_SELECTED_PED_WEAPON, character.Handle);
        }

        private static bool IsUsableWeapon(int weaponHash)
        {
            return weaponHash != UnarmedHash && IsWeaponValid(weaponHash);
        }

        private static bool IsWeaponValid(int weaponHash)
        {
            return Function.Call<bool>(Hash.IS_WEAPON_VALID, weaponHash);
        }

        private static bool IsMenuComponent(WeaponComponentHash component)
        {
            return component != WeaponComponentHash.Invalid &&
                   component.ToString() != "GunrunMk2Upgrade";
        }

        private static bool IsMaxUpgradeComponent(WeaponComponentHash component)
        {
            var name = component.ToString();
            if (name.Contains("Camo") ||
                name.EndsWith("Clip01") ||
                name.EndsWith("Barrel01") ||
                IsSpecialAmmoComponent(name))
            {
                return false;
            }

            return true;
        }

        private static bool IsSpecialAmmoComponent(string componentName)
        {
            return componentName.Contains("ClipArmorPiercing") ||
                   componentName.Contains("ClipExplosive") ||
                   componentName.Contains("ClipFMJ") ||
                   componentName.Contains("ClipHollowPoint") ||
                   componentName.Contains("ClipIncendiary") ||
                   componentName.Contains("ClipTracer");
        }

        private static bool IsStockComponent(WeaponComponentHash component)
        {
            var name = component.ToString();
            return name.EndsWith("Clip01") || name.EndsWith("Barrel01");
        }

        private static ComponentGroup GetComponentGroup(WeaponComponentHash component)
        {
            var name = component.ToString();
            if (name.Contains("Camo") && name.EndsWith("Slide"))
            {
                return ComponentGroup.SlideCamo;
            }

            if (name.Contains("Camo"))
            {
                return ComponentGroup.Camo;
            }

            if (name.Contains("Clip"))
            {
                return ComponentGroup.Magazine;
            }

            if (name.Contains("Flsh") || name.Contains("Flashlight"))
            {
                return ComponentGroup.Flashlight;
            }

            if (name.Contains("Grip"))
            {
                return ComponentGroup.Grip;
            }

            if (name.Contains("Scope") || name.Contains("Sights") || name.Contains("Sight") || name.Contains("Rail"))
            {
                return ComponentGroup.Scope;
            }

            if (name.Contains("Barrel"))
            {
                return ComponentGroup.Barrel;
            }

            if (name.Contains("Supp") || name.Contains("Muzzle") || name.Contains("Comp"))
            {
                return ComponentGroup.Muzzle;
            }

            if (name.Contains("Varmod") || name.StartsWith("Knuckle") || name.StartsWith("Switchblade"))
            {
                return ComponentGroup.Variant;
            }

            return ComponentGroup.Other;
        }

        private static int GetComponentSortKey(WeaponComponentHash component)
        {
            var name = component.ToString();
            if (name.Contains("Clip02")) return 10;
            if (name.Contains("Clip03")) return 20;
            if (name.Contains("ClipTracer")) return 30;
            if (name.Contains("ClipIncendiary")) return 40;
            if (name.Contains("ClipArmorPiercing")) return 50;
            if (name.Contains("ClipHollowPoint")) return 60;
            if (name.Contains("ClipFMJ")) return 70;
            if (name.Contains("ClipExplosive")) return 80;
            if (name.Contains("Sights")) return 10;
            if (name.Contains("ScopeSmall")) return 20;
            if (name.Contains("ScopeMacro")) return 30;
            if (name.Contains("ScopeMedium")) return 40;
            if (name.Contains("ScopeLarge")) return 50;
            if (name.Contains("ScopeMax")) return 60;
            if (name.Contains("ScopeNV")) return 70;
            if (name.Contains("ScopeThermal")) return 80;
            if (name.Contains("Barrel02")) return 10;
            if (name.Contains("Supp")) return 10;
            if (name.Contains("Comp")) return 20;
            if (name.Contains("Muzzle01")) return 30;
            if (name.Contains("Muzzle02")) return 40;
            if (name.Contains("Muzzle03")) return 50;
            if (name.Contains("Muzzle04")) return 60;
            if (name.Contains("Muzzle05")) return 70;
            if (name.Contains("Muzzle06")) return 80;
            if (name.Contains("Muzzle07")) return 90;
            if (name.Contains("Muzzle08")) return 100;
            if (name.Contains("Muzzle09")) return 110;
            if (name.Contains("Independence01")) return 110;

            for (var i = 2; i <= 10; i++)
            {
                if (name.Contains("Camo" + i.ToString("00")))
                {
                    return i * 10;
                }
            }

            if (name.Contains("Camo")) return 10;

            return 500;
        }

        private static WeaponDefinition[] BuildWeaponCatalog(IEnumerable<WeaponHash> weaponHashes)
        {
            var definitions = new List<WeaponDefinition>();
            var usedHashes = new HashSet<int>();

            foreach (var weaponHash in weaponHashes)
            {
                var hash = ToNativeHash(weaponHash);
                if (hash == UnarmedHash || !usedHashes.Add(hash))
                {
                    continue;
                }

                definitions.Add(new WeaponDefinition(hash, FormatWeaponName(weaponHash.ToString())));
            }

            foreach (var weaponName in OnlineWeaponNames)
            {
                var hash = GetHashKey(weaponName);
                if (!usedHashes.Add(hash))
                {
                    continue;
                }

                definitions.Add(new WeaponDefinition(hash, FormatWeaponName(weaponName)));
            }

            return definitions
                .OrderBy(weapon => weapon.Name)
                .ToArray();
        }

        private static string FormatWeaponName(string value)
        {
            var name = value.StartsWith("WEAPON_", StringComparison.Ordinal)
                ? value.Substring("WEAPON_".Length).Replace('_', ' ')
                : value;

            return SplitPascalCase(name)
                .Replace(" Mk2", " Mk II")
                .Replace("M K2", "Mk II")
                .Replace("SMG", "SMG")
                .Trim();
        }

        private static string GetComponentGroupName(ComponentGroup group)
        {
            switch (group)
            {
                case ComponentGroup.Magazine: return "Магазин / патроны";
                case ComponentGroup.Scope: return "Прицел";
                case ComponentGroup.Flashlight: return "Фонарик";
                case ComponentGroup.Grip: return "Рукоять";
                case ComponentGroup.Barrel: return "Ствол";
                case ComponentGroup.Muzzle: return "Дуло";
                case ComponentGroup.Camo: return "Камуфляж";
                case ComponentGroup.SlideCamo: return "Камуфляж затвора";
                case ComponentGroup.Variant: return "Оформление";
                default: return "Другое";
            }
        }

        private static string FormatComponentValue(WeaponComponentHash component, ComponentGroup group)
        {
            var name = component.ToString();
            switch (group)
            {
                case ComponentGroup.Magazine:
                    if (name.Contains("Clip02")) return "Расширенный";
                    if (name.Contains("Clip03")) return "Барабан";
                    if (name.Contains("ClipTracer")) return "Трассеры";
                    if (name.Contains("ClipIncendiary")) return "Зажигательные";
                    if (name.Contains("ClipArmorPiercing")) return "Бронебойные";
                    if (name.Contains("ClipHollowPoint")) return "Экспансивные";
                    if (name.Contains("ClipFMJ")) return "FMJ";
                    if (name.Contains("ClipExplosive")) return "Разрывные";
                    return "Спецмагазин";
                case ComponentGroup.Scope:
                    if (name.Contains("Sights")) return "Коллиматор";
                    if (name.Contains("ScopeMacro")) return "Малый";
                    if (name.Contains("ScopeSmall")) return "Компактный";
                    if (name.Contains("ScopeMedium")) return "Средний";
                    if (name.Contains("ScopeLarge")) return "Большой";
                    if (name.Contains("ScopeMax")) return "Снайперский";
                    if (name.Contains("ScopeNV")) return "Ночное видение";
                    if (name.Contains("ScopeThermal")) return "Тепловизор";
                    if (name.Contains("Rail")) return "Планка";
                    if (name.Contains("Sight")) return "Прицел";
                    return "Прицел";
                case ComponentGroup.Flashlight:
                    return "Вкл";
                case ComponentGroup.Grip:
                    return "Рукоять";
                case ComponentGroup.Barrel:
                    if (name.Contains("Barrel02")) return "Тяжелый";
                    return "Улучшенный";
                case ComponentGroup.Muzzle:
                    if (name.Contains("Supp")) return "Глушитель";
                    if (name.Contains("Comp")) return "Компенсатор";
                    if (name.Contains("Muzzle01")) return "Дульный тормоз 1";
                    if (name.Contains("Muzzle02")) return "Дульный тормоз 2";
                    if (name.Contains("Muzzle03")) return "Дульный тормоз 3";
                    if (name.Contains("Muzzle04")) return "Дульный тормоз 4";
                    if (name.Contains("Muzzle05")) return "Дульный тормоз 5";
                    if (name.Contains("Muzzle06")) return "Дульный тормоз 6";
                    if (name.Contains("Muzzle07")) return "Дульный тормоз 7";
                    if (name.Contains("Muzzle08")) return "Дульный тормоз 8";
                    if (name.Contains("Muzzle09")) return "Дульный тормоз 9";
                    return "Дульник";
                case ComponentGroup.Camo:
                case ComponentGroup.SlideCamo:
                    return FormatCamoValue(name);
                case ComponentGroup.Variant:
                    if (name.Contains("Luxe")) return "Люкс";
                    if (name.Contains("Lowrider")) return "Лоурайдер";
                    if (name.Contains("Boss")) return "Босс";
                    if (name.Contains("Goon")) return "Гангстер";
                    if (name.Contains("Ballas")) return "Ballas";
                    if (name.Contains("Diamond")) return "Diamond";
                    if (name.Contains("Dollar")) return "Dollar";
                    if (name.Contains("Hate")) return "Hate";
                    if (name.Contains("King")) return "King";
                    if (name.Contains("Love")) return "Love";
                    if (name.Contains("Pimp")) return "Pimp";
                    if (name.Contains("Player")) return "Player";
                    if (name.Contains("Vagos")) return "Vagos";
                    if (name.Contains("Var1")) return "Вариант 1";
                    if (name.Contains("Var2")) return "Вариант 2";
                    return "Вариант";
                default:
                    return SplitPascalCase(name)
                        .Replace(" Mk2", " Mk II")
                        .Trim();
            }
        }

        private static string FormatCamoValue(string componentName)
        {
            if (componentName.Contains("Independence01")) return "Патриот";
            if (componentName.Contains("Camo02")) return "Кисть";
            if (componentName.Contains("Camo03")) return "Лесной";
            if (componentName.Contains("Camo04")) return "Череп";
            if (componentName.Contains("Camo05")) return "Sessanta Nove";
            if (componentName.Contains("Camo06")) return "Perseus";
            if (componentName.Contains("Camo07")) return "Леопард";
            if (componentName.Contains("Camo08")) return "Зебра";
            if (componentName.Contains("Camo09")) return "Геометрия";
            if (componentName.Contains("Camo10")) return "Бум";
            return "Цифровой";
        }

        private static string SplitPascalCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var text = value[0].ToString();
            for (var i = 1; i < value.Length; i++)
            {
                var current = value[i];
                var previous = value[i - 1];
                if (char.IsUpper(current) && (char.IsLower(previous) || char.IsDigit(previous)))
                {
                    text += " ";
                }

                text += current;
            }

            return text;
        }

        private static string GetCategoryName(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Quick: return "Быстро";
                case WeaponCategory.Weapons: return "Оружие";
                case WeaponCategory.Mk2: return "Mk II";
                case WeaponCategory.Components: return "Обвесы";
                case WeaponCategory.Tint: return "Цвет";
                default: return "Оружие";
            }
        }

        private static int Wrap(int value, int min, int max)
        {
            if (max < min)
            {
                return min;
            }

            if (value < min)
            {
                return max;
            }

            return value > max ? min : value;
        }

        private static int ToNativeHash(WeaponHash weaponHash)
        {
            return unchecked((int)(uint)weaponHash);
        }

        private static int ToNativeHash(WeaponComponentHash componentHash)
        {
            return unchecked((int)(uint)componentHash);
        }

        private static int GetHashKey(string value)
        {
            uint hash = 0;
            for (var i = 0; i < value.Length; i++)
            {
                hash += char.ToLowerInvariant(value[i]);
                hash += hash << 10;
                hash ^= hash >> 6;
            }

            hash += hash << 3;
            hash ^= hash >> 11;
            hash += hash << 15;

            return unchecked((int)hash);
        }

        private enum WeaponCategory
        {
            None,
            Quick,
            Weapons,
            Mk2,
            Components,
            Tint
        }

        private enum WeaponOptionKind
        {
            Command,
            Weapon,
            Mk2Conversion,
            Component,
            Tint
        }

        private enum ComponentGroup
        {
            None,
            Magazine,
            Scope,
            Flashlight,
            Grip,
            Barrel,
            Muzzle,
            Camo,
            SlideCamo,
            Variant,
            Other
        }

        private enum WeaponCommand
        {
            GiveAllMax,
            SaveCurrentConfig,
            ApplySavedConfig,
            ApplyAllConfigs,
            MaxCurrent,
            RefillCurrent,
            RemoveAll
        }

        private struct WeaponCategoryDefinition
        {
            public readonly WeaponCategory Kind;
            public readonly string Name;
            public readonly string StatusText;
            public readonly bool IsEnabled;

            public WeaponCategoryDefinition(WeaponCategory kind, string name, string statusText, bool isEnabled)
            {
                Kind = kind;
                Name = name;
                StatusText = statusText;
                IsEnabled = isEnabled;
            }
        }

        private struct WeaponOption
        {
            public readonly string Name;
            public readonly WeaponOptionKind Kind;
            public readonly WeaponCommand Command;
            public readonly int WeaponHash;
            public readonly int TargetHash;
            public readonly int ComponentHash;
            public readonly ComponentGroup ComponentGroup;
            public readonly int Min;
            public readonly int Max;

            private WeaponOption(
                string name,
                WeaponOptionKind kind,
                WeaponCommand command,
                int weaponHash,
                int targetHash,
                int componentHash,
                ComponentGroup componentGroup,
                int min,
                int max)
            {
                Name = name;
                Kind = kind;
                Command = command;
                WeaponHash = weaponHash;
                TargetHash = targetHash;
                ComponentHash = componentHash;
                ComponentGroup = componentGroup;
                Min = min;
                Max = max;
            }

            public static WeaponOption CreateCommand(string name, WeaponCommand command)
            {
                return new WeaponOption(name, WeaponOptionKind.Command, command, 0, 0, 0, ComponentGroup.None, 0, 0);
            }

            public static WeaponOption Weapon(string name, int weaponHash)
            {
                return new WeaponOption(name, WeaponOptionKind.Weapon, WeaponCommand.GiveAllMax, weaponHash, 0, 0, ComponentGroup.None, 0, 0);
            }

            public static WeaponOption Mk2Conversion(string name, int sourceHash, int targetHash)
            {
                return new WeaponOption(name, WeaponOptionKind.Mk2Conversion, WeaponCommand.GiveAllMax, sourceHash, targetHash, 0, ComponentGroup.None, 0, 0);
            }

            public static WeaponOption Component(string name, int weaponHash, ComponentGroup group)
            {
                return new WeaponOption(name, WeaponOptionKind.Component, WeaponCommand.GiveAllMax, weaponHash, 0, 0, group, 0, 0);
            }

            public static WeaponOption Range(string name, WeaponOptionKind kind, int weaponHash, int min, int max)
            {
                return new WeaponOption(name, kind, WeaponCommand.GiveAllMax, weaponHash, 0, 0, ComponentGroup.None, min, max);
            }
        }

        private struct WeaponDefinition
        {
            public readonly int Hash;
            public readonly string Name;

            public WeaponDefinition(int hash, string name)
            {
                Hash = hash;
                Name = name;
            }
        }

        private sealed class WeaponConversion
        {
            public readonly string DisplayName;
            public readonly int SourceHash;
            public readonly int TargetHash;

            public WeaponConversion(string displayName, string sourceName, string targetName)
            {
                DisplayName = displayName;
                SourceHash = GetHashKey(sourceName);
                TargetHash = GetHashKey(targetName);
            }
        }

        private struct MenuSelection
        {
            public readonly int Page;
            public readonly int Index;

            public MenuSelection(int page, int index)
            {
                Page = page;
                Index = index;
            }
        }
    }
}
