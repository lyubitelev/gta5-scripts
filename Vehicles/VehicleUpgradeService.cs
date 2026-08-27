using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;

namespace gta.Vehicles
{
    internal sealed class VehicleUpgradeService
    {
        private const int ItemsPerPage = 10;
        private const int MaxExtraId = 20;
        private const int MaxXenonColor = 12;
        private const int MaxVehicleColor = 159;
        private const int MaxWheelType = 12;

        private static readonly VehicleTuningConfigStore ConfigStore =
            new VehicleTuningConfigStore(ScriptPaths.VehicleConfigsDirectory);

        private static readonly int[] PerformanceModTypes =
        {
            11,
            12,
            13,
            15,
            16
        };

        private static readonly int[] BodyModTypes =
        {
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            9,
            10
        };

        private static readonly int[] PlateModTypes =
        {
            25,
            26
        };

        private static readonly int[] WheelModTypes =
        {
            23,
            24
        };

        private static readonly int[] InteriorModTypes =
        {
            27,
            28,
            29,
            30,
            31,
            32,
            33,
            34,
            35,
            36,
            37
        };

        private static readonly int[] EngineBayModTypes =
        {
            38,
            39,
            40,
            41,
            42,
            43,
            44,
            45,
            46
        };

        private static readonly int[] LiveryModTypes =
        {
            47,
            48
        };

        private static readonly int[] LightModTypes =
        {
            49
        };

        private static readonly int[] ToggleModTypes =
        {
            18,
            20,
            22
        };

        private static readonly BennyConversion[] BennyConversions =
        {
            new BennyConversion("banshee", "banshee2", "Banshee 900R"),
            new BennyConversion("sultan", "sultanrs", "Sultan RS"),
            new BennyConversion("elegy2", "elegy", "Elegy Retro Custom"),
            new BennyConversion("comet2", "comet3", "Comet Retro Custom"),
            new BennyConversion("fcr", "fcr2", "FCR 1000 Custom"),
            new BennyConversion("diablous", "diablous2", "Diabolus Custom"),
            new BennyConversion("italigtb", "italigtb2", "Itali GTB Custom"),
            new BennyConversion("nero", "nero2", "Nero Custom"),
            new BennyConversion("specter", "specter2", "Specter Custom"),
            new BennyConversion("buccaneer", "buccaneer2", "Buccaneer Custom"),
            new BennyConversion("chino", "chino2", "Chino Custom"),
            new BennyConversion("faction", "faction2", "Faction Custom"),
            new BennyConversion("faction", "faction3", "Faction Custom Donk"),
            new BennyConversion("faction2", "faction3", "Faction Custom Donk"),
            new BennyConversion("moonbeam", "moonbeam2", "Moonbeam Custom"),
            new BennyConversion("primo", "primo2", "Primo Custom"),
            new BennyConversion("voodoo2", "voodoo", "Voodoo Custom"),
            new BennyConversion("minivan", "minivan2", "Minivan Custom"),
            new BennyConversion("sabregt", "sabregt2", "Sabre Turbo Custom"),
            new BennyConversion("slamvan", "slamvan3", "Slamvan Custom"),
            new BennyConversion("slamvan2", "slamvan3", "Slamvan Custom"),
            new BennyConversion("tornado", "tornado5", "Tornado Custom"),
            new BennyConversion("tornado2", "tornado5", "Tornado Custom"),
            new BennyConversion("tornado3", "tornado5", "Tornado Custom"),
            new BennyConversion("tornado4", "tornado5", "Tornado Custom"),
            new BennyConversion("virgo", "virgo2", "Virgo Classic Custom"),
            new BennyConversion("virgo3", "virgo2", "Virgo Classic Custom"),
            new BennyConversion("youga2", "youga3", "Youga Classic 4x4"),
            new BennyConversion("yosemite", "yosemite3", "Yosemite Rancher"),
            new BennyConversion("peyote", "peyote3", "Peyote Custom"),
            new BennyConversion("manana", "manana2", "Manana Custom"),
            new BennyConversion("glendale", "glendale2", "Glendale Custom"),
            new BennyConversion("gauntlet3", "gauntlet5", "Gauntlet Classic Custom"),
            new BennyConversion("weevil", "weevil2", "Weevil Custom"),
            new BennyConversion("brioso2", "brioso3", "Brioso 300 Widebody"),
            new BennyConversion("sentinel3", "sentinel4", "Sentinel Classic Widebody"),
            new BennyConversion("tenf", "tenf2", "10F Widebody")
        };

        private static readonly ColorPreset[] LightColorPresets =
        {
            new ColorPreset("Белый", 255, 255, 255),
            new ColorPreset("Синий", 0, 0, 255),
            new ColorPreset("Голубой", 0, 150, 255),
            new ColorPreset("Мятный", 50, 255, 155),
            new ColorPreset("Лайм", 0, 255, 0),
            new ColorPreset("Желтый", 255, 255, 0),
            new ColorPreset("Золотой", 255, 180, 0),
            new ColorPreset("Оранжевый", 255, 90, 0),
            new ColorPreset("Красный", 255, 0, 0),
            new ColorPreset("Розовый", 255, 80, 150),
            new ColorPreset("Ярко-розовый", 255, 0, 255),
            new ColorPreset("Фиолетовый", 150, 0, 255),
            new ColorPreset("УФ", 50, 0, 255)
        };

        private static readonly int[] GroupedVehicleModTypes = BodyModTypes
            .Concat(PerformanceModTypes)
            .Concat(PlateModTypes)
            .Concat(WheelModTypes)
            .Concat(InteriorModTypes)
            .Concat(EngineBayModTypes)
            .Concat(LiveryModTypes)
            .Concat(LightModTypes)
            .ToArray();

        private static readonly string[] WheelTypeNames =
        {
            "Спорт",
            "Маслкары",
            "Лоурайдер",
            "Внедорожник",
            "Бездорожье",
            "Тюнер",
            "Мото",
            "Премиум",
            "Benny оригинал",
            "Benny особые",
            "Открытые колеса",
            "Уличные",
            "Трек"
        };

        private bool _isMenuVisible;
        private bool _isFobMode;
        private TuningCategory _category = TuningCategory.None;
        private readonly Dictionary<TuningCategory, MenuSelection> _savedSelections =
            new Dictionary<TuningCategory, MenuSelection>();
        private int _categoryIndex;
        private int _page;
        private int _index;
        private static readonly Dictionary<int, bool> VehicleKeepEngineRunning = new Dictionary<int, bool>();
        private static readonly Dictionary<int, bool> VehicleWindowsDown = new Dictionary<int, bool>();
        private static readonly Dictionary<int, bool> VehicleForcedBrakeLights = new Dictionary<int, bool>();
        private static readonly HashSet<int> HandbrakedVehicles = new HashSet<int>();
        private static bool _isInteriorLightOn = false;

        public static bool IsVehicleHandbraked(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return false;
            return HandbrakedVehicles.Contains(vehicle.Handle);
        }

        public static void ToggleHandbrake(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return;
            bool isHandbraked = HandbrakedVehicles.Contains(vehicle.Handle);
            if (isHandbraked)
            {
                HandbrakedVehicles.Remove(vehicle.Handle);
                Function.Call(Hash.SET_VEHICLE_HANDBRAKE, vehicle.Handle, false);
                Notifier.Show("Ручной тормоз: ~g~Снят (ВЫКЛ)~s~");
            }
            else
            {
                HandbrakedVehicles.Add(vehicle.Handle);
                Function.Call(Hash.SET_VEHICLE_HANDBRAKE, vehicle.Handle, true);
                Notifier.Show("Ручной тормоз: ~r~Затянут (ВКЛ)~s~");
            }
        }

        public bool IsMenuVisible
        {
            get { return _isMenuVisible; }
        }

        public void ToggleMenu()
        {
            var currentVehicle = GetCurrentVehicle();
            if (currentVehicle == null)
            {
                Notifier.Show("Поблизости нет транспорта");
                return;
            }

            var player = Game.Player.Character;
            bool isOnFoot = player != null && player.Exists() && !player.IsInVehicle();

            _isMenuVisible = !_isMenuVisible;

            if (_isMenuVisible)
            {
                if (isOnFoot)
                {
                    _isFobMode = true;
                    _category = TuningCategory.Doors;
                    LoadSelection(_category);
                    PlayKeyFobBeep(currentVehicle);
                    Notifier.Show("Брелок авто: Двери и кузов");
                }
                else
                {
                    _isFobMode = false;
                    _category = TuningCategory.None;
                    EnsureModKitSelected(currentVehicle);
                    Notifier.Show("Меню тюнинга открыто");
                }
            }
            else
            {
                _isFobMode = false;
                Notifier.Show("Меню закрыто");
            }
        }

        public void Draw()
        {
            if (!_isMenuVisible)
            {
                return;
            }

            var vehicle = GetCurrentVehicle();
            if (vehicle == null)
            {
                _isMenuVisible = false;
                return;
            }

            if (_category == TuningCategory.None)
            {
                DrawCategories(vehicle);
                return;
            }

            var options = BuildOptions(vehicle, _category);
            if (options.Count == 0)
            {
                ReturnToCategories();
                return;
            }

            ClampState(options.Count);

            var startIndex = _page * ItemsPerPage;
            var endIndex = Math.Min(startIndex + ItemsPerPage, options.Count);
            var pageCount = Math.Max(1, (options.Count + ItemsPerPage - 1) / ItemsPerPage);
            var text = _isFobMode
                ? "Брелок авто: Двери и кузов\n"
                : "Тюнинг: " + GetCategoryName(_category) + "\n";

            for (var i = startIndex; i < endIndex; i++)
            {
                var option = options[i];
                var marker = i == _index ? "> " : "  ";
                var suffix = GetOptionValueText(vehicle, option);
                text += marker + option.Name + suffix + "\n";
            }

            text += "\nСтраница " + (_page + 1) + "/" + pageCount;
            text += "\n8/2 - выбор  7/9 - страницы  4/6 - изменить  5 - применить  0 - назад";

            MenuPanelRenderer.Draw(text, new PointF(10, 10), 0.42f);
        }

        public void Handle(KeyEventArgs e)
        {
            if (!_isMenuVisible)
            {
                return;
            }

            var vehicle = GetCurrentVehicle();
            if (vehicle == null)
            {
                _isMenuVisible = false;
                return;
            }

            if (_category == TuningCategory.None)
            {
                HandleCategories(vehicle, e);
                return;
            }

            var options = BuildOptions(vehicle, _category);
            if (options.Count == 0)
            {
                ReturnToCategories();
                return;
            }

            ClampState(options.Count);

            var startIndex = _page * ItemsPerPage;
            var endIndex = Math.Min(startIndex + ItemsPerPage, options.Count);

            switch (e.KeyCode)
            {
                case Keys.X:
                case Keys.Escape:
                    _isMenuVisible = false;
                    Notifier.Show("Меню тюнинга закрыто");
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
                    ChangeOption(vehicle, options[_index], -1);
                    break;
                case Keys.NumPad6:
                    ChangeOption(vehicle, options[_index], 1);
                    break;
                case Keys.NumPad5:
                    if (options[_index].Kind == TuningOptionKind.BennyConversion)
                    {
                        ApplyOption(vehicle, options[_index]);
                        ReturnToCategories();
                        return;
                    }

                    ApplyOption(vehicle, options[_index]);
                    break;
            }

            if (_category != TuningCategory.None)
            {
                ClampState(BuildOptions(vehicle, _category).Count);
            }
        }

        private void DrawCategories(Vehicle vehicle)
        {
            var categories = BuildCategories(vehicle);
            if (categories.Count == 0)
            {
                return;
            }

            ClampCategoryState(categories.Count);

            var text = "Тюнинг\n";

            for (var i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                var marker = i == _categoryIndex ? "> " : "  ";
                text += marker + category.Name + " [" + category.StatusText + "]\n";
            }

            text += "\n8/2 - выбор  5 - открыть  0 - закрыть";

            MenuPanelRenderer.Draw(text, new PointF(10, 10), 0.42f);
        }

        private void HandleCategories(Vehicle vehicle, KeyEventArgs e)
        {
            var categories = BuildCategories(vehicle);
            if (categories.Count == 0)
            {
                return;
            }

            ClampCategoryState(categories.Count);

            switch (e.KeyCode)
            {
                case Keys.X:
                case Keys.NumPad0:
                case Keys.Back:
                case Keys.Escape:
                    _isMenuVisible = false;
                    Notifier.Show("Меню тюнинга закрыто");
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
                        Notifier.Show(GetDisabledCategoryText(vehicle, categories[_categoryIndex].Kind));
                        break;
                    }

                    _category = categories[_categoryIndex].Kind;
                    LoadSelection(_category);
                    break;
            }
        }

        private void ReturnToCategories()
        {
            SaveSelection();
            if (_isFobMode)
            {
                _isMenuVisible = false;
                _isFobMode = false;
                return;
            }

            _category = TuningCategory.None;
        }

        private void ResetMenuState()
        {
            ReturnToCategories();
        }

        private void SaveSelection()
        {
            if (_category == TuningCategory.None)
            {
                return;
            }

            _savedSelections[_category] = new MenuSelection(_page, _index);
        }

        private void LoadSelection(TuningCategory category)
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

        public void MaximizePerformanceModsForCurrentVehicle()
        {
            var currentVehicle = GetCurrentVehicle();
            if (currentVehicle == null)
            {
                return;
            }

            MaximizePerformanceMods(currentVehicle);
        }

        public void MaximizeModsForCurrentVehicle()
        {
            var currentVehicle = GetCurrentVehicle();
            if (currentVehicle == null)
            {
                return;
            }

            MaximizeMods(currentVehicle);
        }

        private static List<TuningCategoryDefinition> BuildCategories(Vehicle vehicle)
        {
            var categories = new List<TuningCategoryDefinition>();

            AddCategory(vehicle, categories, TuningCategory.Quick);
            AddCategory(vehicle, categories, TuningCategory.Doors);
            AddCategory(vehicle, categories, TuningCategory.Nitro);
            AddCategory(vehicle, categories, TuningCategory.ModKits);
            AddCategory(vehicle, categories, TuningCategory.Performance);
            AddCategory(vehicle, categories, TuningCategory.Body);
            AddCategory(vehicle, categories, TuningCategory.Paint);
            AddCategory(vehicle, categories, TuningCategory.Plates);
            AddCategory(vehicle, categories, TuningCategory.Wheels);
            AddCategory(vehicle, categories, TuningCategory.Lights);
            AddCategory(vehicle, categories, TuningCategory.Interior);
            AddCategory(vehicle, categories, TuningCategory.EngineBay);
            AddCategory(vehicle, categories, TuningCategory.Liveries);
            AddCategory(vehicle, categories, TuningCategory.Extras);
            AddCategory(vehicle, categories, TuningCategory.Misc);
            categories.Add(CreateBennyCategory(vehicle));

            return categories;
        }

        private static void AddCategory(Vehicle vehicle, ICollection<TuningCategoryDefinition> categories, TuningCategory category)
        {
            var count = BuildOptions(vehicle, category).Count;
            if (count <= 0)
            {
                return;
            }

            categories.Add(new TuningCategoryDefinition(category, GetCategoryName(category), count, count.ToString(), true));
        }

        private static TuningCategoryDefinition CreateBennyCategory(Vehicle vehicle)
        {
            var count = BuildOptions(vehicle, TuningCategory.Benny).Count;
            if (count > 0)
            {
                return new TuningCategoryDefinition(TuningCategory.Benny, GetCategoryName(TuningCategory.Benny), count, "доступно " + count, true);
            }

            return new TuningCategoryDefinition(
                TuningCategory.Benny,
                GetCategoryName(TuningCategory.Benny),
                0,
                IsBennyCustomVehicle(vehicle) ? "уже custom" : "нет",
                false);
        }

        private static string GetDisabledCategoryText(Vehicle vehicle, TuningCategory category)
        {
            if (category == TuningCategory.Benny)
            {
                return IsBennyCustomVehicle(vehicle)
                    ? "Это уже Benny/custom-версия"
                    : "Эта машина не поддерживает Benny-конверсию";
            }

            return "Вкладка недоступна";
        }

        private static string GetCategoryName(TuningCategory category)
        {
            switch (category)
            {
                case TuningCategory.Quick: return "Быстро";
                case TuningCategory.Doors: return "Двери и кузов";
                case TuningCategory.Nitro: return "Нитро";
                case TuningCategory.Benny: return "Бенни";
                case TuningCategory.ModKits: return "Модкиты";
                case TuningCategory.Performance: return "Производительность";
                case TuningCategory.Body: return "Кузов";
                case TuningCategory.Paint: return "Покраска";
                case TuningCategory.Plates: return "Номера";
                case TuningCategory.Wheels: return "Колеса";
                case TuningCategory.Lights: return "Свет";
                case TuningCategory.Interior: return "Салон";
                case TuningCategory.EngineBay: return "Под капотом";
                case TuningCategory.Liveries: return "Ливреи";
                case TuningCategory.Extras: return "Extras";
                case TuningCategory.Misc: return "Разное";
                default: return "Тюнинг";
            }
        }

        private static List<TuningOption> BuildOptions(Vehicle vehicle, TuningCategory category)
        {
            var options = new List<TuningOption>();

            switch (category)
            {
                case TuningCategory.Quick:
                    options.Add(TuningOption.CreateCommand("Сохранить конфиг", TuningCommand.SaveConfig));
                    if (ConfigStore.HasConfig(vehicle))
                    {
                        options.Add(TuningOption.CreateCommand("Применить сохраненный", TuningCommand.ApplySavedConfig));
                    }

                    options.Add(TuningOption.CreateCommand("Починить", TuningCommand.Repair));
                    options.Add(TuningOption.CreateCommand("Помыть машину", TuningCommand.Clean));
                    options.Add(TuningOption.CreateCommand("Макс. производительность", TuningCommand.MaxPerformance));
                    options.Add(TuningOption.CreateCommand("Макс. тюнинг", TuningCommand.MaxAll));
                    break;
                case TuningCategory.Doors:
                    options.Add(TuningOption.SpecialToggle("Центральный замок", TuningOptionKind.DoorLockStatus));
                    options.Add(TuningOption.SpecialToggle("Открыть/Закрыть все двери", TuningOptionKind.DoorAll));
                    options.Add(TuningOption.SpecialToggle("Капот", TuningOptionKind.DoorHood));
                    options.Add(TuningOption.SpecialToggle("Багажник", TuningOptionKind.DoorTrunk));
                    options.Add(TuningOption.SpecialToggle("Водительская дверь", TuningOptionKind.DoorFrontLeft));
                    options.Add(TuningOption.SpecialToggle("Пассажирская дверь", TuningOptionKind.DoorFrontRight));
                    if (Function.Call<bool>(Hash.GET_IS_DOOR_VALID, vehicle.Handle, 2))
                    {
                        options.Add(TuningOption.SpecialToggle("Задняя левая дверь", TuningOptionKind.DoorBackLeft));
                    }
                    if (Function.Call<bool>(Hash.GET_IS_DOOR_VALID, vehicle.Handle, 3))
                    {
                        options.Add(TuningOption.SpecialToggle("Задняя правая дверь", TuningOptionKind.DoorBackRight));
                    }
                    options.Add(TuningOption.SpecialToggle("Все стекла", TuningOptionKind.WindowsAll));
                    if (Function.Call<bool>(Hash.IS_VEHICLE_A_CONVERTIBLE, vehicle.Handle, false))
                    {
                        options.Add(TuningOption.SpecialToggle("Крыша кабриолета", TuningOptionKind.ConvertibleRoof));
                    }
                    options.Add(TuningOption.SpecialToggle("Двигатель (Автозапуск)", TuningOptionKind.EngineToggle));
                    options.Add(TuningOption.SpecialToggle("Свет в салоне", TuningOptionKind.InteriorLight));
                    options.Add(TuningOption.SpecialToggle("Тормозные огни (Стоп-сигналы)", TuningOptionKind.BrakeLightsToggle));
                    options.Add(TuningOption.SpecialToggle("Не глушить при выходе", TuningOptionKind.KeepEngineRunning));
                    options.Add(TuningOption.CreateCommand("Поиск на парковке (Сигнал)", TuningCommand.PanicAlarm));
                    break;
                case TuningCategory.Nitro:
                    options.Add(TuningOption.SpecialToggle("Реактивное нитро (Shift)", TuningOptionKind.NitroBoostToggle));
                    options.Add(TuningOption.Range("Пламя выхлопа", TuningOptionKind.NitroFlameMode, 0, 2));
                    break;
                case TuningCategory.Benny:
                    AddBennyConversionOptions(vehicle, options);
                    break;
                case TuningCategory.ModKits:
                    AddModKitOptions(vehicle, options);
                    break;
                case TuningCategory.Performance:
                    AddVehicleModOptions(vehicle, options, PerformanceModTypes);
                    options.Add(TuningOption.Toggle("Турбо", 18));
                    break;
                case TuningCategory.Body:
                    AddVehicleModOptions(vehicle, options, BodyModTypes);
                    break;
                case TuningCategory.Paint:
                    options.Add(TuningOption.Range("Тонировка", TuningOptionKind.WindowTint, 0, GetWindowTintCount(vehicle) - 1));
                    options.Add(TuningOption.Range("Основной цвет", TuningOptionKind.PrimaryColor, 0, GetVehicleColorCount() - 1));
                    options.Add(TuningOption.Range("Доп. цвет", TuningOptionKind.SecondaryColor, 0, GetVehicleColorCount() - 1));
                    options.Add(TuningOption.Range("Перламутр", TuningOptionKind.PearlescentColor, 0, MaxVehicleColor));
                    break;
                case TuningCategory.Plates:
                    options.Add(TuningOption.Range("Тип номера", TuningOptionKind.PlateType, 0, GetNumberPlateCount() - 1));
                    AddVehicleModOptions(vehicle, options, PlateModTypes);
                    break;
                case TuningCategory.Wheels:
                    options.Add(TuningOption.Range("Цвет дисков", TuningOptionKind.WheelColor, 0, MaxVehicleColor));
                    options.Add(TuningOption.Range("Тип дисков", TuningOptionKind.WheelType, 0, MaxWheelType));
                    options.Add(TuningOption.SpecialToggle("Пуленепробиваемые шины", TuningOptionKind.BulletproofTires));
                    options.Add(TuningOption.Toggle("Дым от шин", 20));
                    options.Add(TuningOption.Range("Цвет дыма", TuningOptionKind.TireSmokeColor, 0, LightColorPresets.Length - 1));
                    AddVehicleModOptions(vehicle, options, WheelModTypes);
                    break;
                case TuningCategory.Lights:
                    options.Add(TuningOption.Toggle("Ксенон", 22));
                    options.Add(TuningOption.Range("Цвет ксенона", TuningOptionKind.XenonColor, -1, MaxXenonColor));
                    options.Add(TuningOption.SpecialToggle("Неон", TuningOptionKind.NeonLights));
                    options.Add(TuningOption.Range("Цвет неона", TuningOptionKind.NeonColor, 0, LightColorPresets.Length - 1));
                    AddVehicleModOptions(vehicle, options, LightModTypes);
                    break;
                case TuningCategory.Interior:
                    AddVehicleModOptions(vehicle, options, InteriorModTypes);
                    break;
                case TuningCategory.EngineBay:
                    AddVehicleModOptions(vehicle, options, EngineBayModTypes);
                    break;
                case TuningCategory.Liveries:
                    AddLiveryOptions(vehicle, options);
                    AddVehicleModOptions(vehicle, options, LiveryModTypes);
                    break;
                case TuningCategory.Extras:
                    AddExtraOptions(vehicle, options);
                    break;
                case TuningCategory.Misc:
                    options.Add(TuningOption.SpecialToggle("Ручной тормоз (Ручник)", TuningOptionKind.HandbrakeToggle));
                    AddMiscOptions(vehicle, options);
                    break;
            }

            return options.Where(option => option.IsAvailable).ToList();
        }

        private static void AddBennyConversionOptions(Vehicle vehicle, ICollection<TuningOption> options)
        {
            foreach (var conversion in BennyConversions)
            {
                if (vehicle.Model.Hash != conversion.SourceHash)
                {
                    continue;
                }

                options.Add(TuningOption.BennyConversion(
                    "Переоборудовать в " + conversion.DisplayName,
                    conversion.TargetModel));
            }
        }

        private static bool IsBennyCustomVehicle(Vehicle vehicle)
        {
            return BennyConversions.Any(conversion => vehicle.Model.Hash == conversion.TargetHash);
        }

        private static void AddLiveryOptions(Vehicle vehicle, ICollection<TuningOption> options)
        {
            var liveryCount = Function.Call<int>(Hash.GET_VEHICLE_LIVERY_COUNT, vehicle.Handle);
            if (liveryCount > 0)
            {
                options.Add(TuningOption.Range("Ливрея", TuningOptionKind.Livery, -1, liveryCount - 1));
            }

            var livery2Count = Function.Call<int>(Hash.GET_VEHICLE_LIVERY2_COUNT, vehicle.Handle);
            if (livery2Count > 0)
            {
                options.Add(TuningOption.Range("Ливрея 2", TuningOptionKind.Livery2, -1, livery2Count - 1));
            }
        }

        private static void AddModKitOptions(Vehicle vehicle, ICollection<TuningOption> options)
        {
            var count = GetModKitCount(vehicle);
            if (count > 1)
            {
                options.Add(TuningOption.Range("Модкит", TuningOptionKind.ModKit, 0, count - 1));
            }
        }

        private static void AddExtraOptions(Vehicle vehicle, ICollection<TuningOption> options)
        {
            for (var extraId = 1; extraId <= MaxExtraId; extraId++)
            {
                if (!Function.Call<bool>(Hash.DOES_EXTRA_EXIST, vehicle.Handle, extraId))
                {
                    continue;
                }

                options.Add(TuningOption.Extra("Extra " + extraId, extraId));
            }
        }

        private static void AddVehicleModOptions(Vehicle vehicle, ICollection<TuningOption> options, IEnumerable<int> modTypes)
        {
            foreach (var modType in modTypes)
            {
                AddVehicleModOption(vehicle, options, modType);
            }
        }

        private static void AddMiscOptions(Vehicle vehicle, ICollection<TuningOption> options)
        {
            for (var modType = 0; modType <= ModSettings.MaxVehicleModType; modType++)
            {
                if (ToggleModTypes.Contains(modType) || GroupedVehicleModTypes.Contains(modType))
                {
                    continue;
                }

                AddVehicleModOption(vehicle, options, modType);
            }
        }

        private static void AddVehicleModOption(Vehicle vehicle, ICollection<TuningOption> options, int modType)
        {
            if (ToggleModTypes.Contains(modType))
            {
                return;
            }

            var numMods = Function.Call<int>(Hash.GET_NUM_VEHICLE_MODS, vehicle.Handle, modType);
            if (numMods <= 0)
            {
                return;
            }

            options.Add(TuningOption.Mod(GetModTypeName(modType), modType, numMods));
        }

        private static string GetOptionValueText(Vehicle vehicle, TuningOption option)
        {
            switch (option.Kind)
            {
                case TuningOptionKind.Command:
                    return "";
                case TuningOptionKind.BennyConversion:
                    return " [" + option.TargetModel + "]";
                case TuningOptionKind.Mod:
                    return " [" + FormatModValue(GetVehicleMod(vehicle, option.ModType), option.Max) + "]";
                case TuningOptionKind.ModKit:
                    return " [ID " + Function.Call<int>(Hash.GET_VEHICLE_MOD_KIT, vehicle.Handle) + ", всего " + (option.Max + 1) + "]";
                case TuningOptionKind.Extra:
                    return " [" + (IsExtraOn(vehicle, option.ModType) ? "Вкл" : "Выкл") + "]";
                case TuningOptionKind.ToggleMod:
                    return " [" + (IsToggleModOn(vehicle, option.ModType) ? "Вкл" : "Выкл") + "]";
                case TuningOptionKind.WindowTint:
                    return " [" + Function.Call<int>(Hash.GET_VEHICLE_WINDOW_TINT, vehicle.Handle) + "/" + option.Max + "]";
                case TuningOptionKind.PlateType:
                    return " [" + Function.Call<int>(Hash.GET_VEHICLE_NUMBER_PLATE_TEXT_INDEX, vehicle.Handle) + "/" + option.Max + "]";
                case TuningOptionKind.PrimaryColor:
                    return " [" + GetVehicleColors(vehicle).Primary + "]";
                case TuningOptionKind.SecondaryColor:
                    return " [" + GetVehicleColors(vehicle).Secondary + "]";
                case TuningOptionKind.PearlescentColor:
                    return " [" + GetExtraColors(vehicle).Pearlescent + "]";
                case TuningOptionKind.WheelColor:
                    return " [" + GetExtraColors(vehicle).Wheel + "]";
                case TuningOptionKind.WheelType:
                    return " [" + GetWheelTypeText(Function.Call<int>(Hash.GET_VEHICLE_WHEEL_TYPE, vehicle.Handle)) + "]";
                case TuningOptionKind.XenonColor:
                    return " [" + GetXenonColorText(Function.Call<int>(Hash.GET_VEHICLE_XENON_LIGHT_COLOR_INDEX, vehicle.Handle)) + "]";
                case TuningOptionKind.NeonColor:
                    return " [" + LightColorPresets[GetNeonColorIndex(vehicle)].Name + "]";
                case TuningOptionKind.TireSmokeColor:
                    return " [" + LightColorPresets[GetTireSmokeColorIndex(vehicle)].Name + "]";
                case TuningOptionKind.Livery:
                    return " [" + FormatModValue(Function.Call<int>(Hash.GET_VEHICLE_LIVERY, vehicle.Handle), option.Max) + "]";
                case TuningOptionKind.Livery2:
                    return " [" + FormatModValue(Function.Call<int>(Hash.GET_VEHICLE_LIVERY2, vehicle.Handle), option.Max) + "]";
                case TuningOptionKind.BulletproofTires:
                    return " [" + (!Function.Call<bool>(Hash.GET_VEHICLE_TYRES_CAN_BURST, vehicle.Handle) ? "Вкл" : "Выкл") + "]";
                case TuningOptionKind.NeonLights:
                    return " [" + (AreNeonsOn(vehicle) ? "Вкл" : "Выкл") + "]";
                case TuningOptionKind.NitroBoostToggle:
                    return " [" + (VehicleNitroService.GetNitroEnabledForVehicle(vehicle) ? "Вкл" : "Выкл") + "]";
                case TuningOptionKind.NitroFlameMode:
                    switch (VehicleNitroService.GetFlameModeForVehicle(vehicle))
                    {
                        case NitroFlameMode.DuringBoost: return " [При нитро]";
                        case NitroFlameMode.AlwaysOn: return " [Всегда вкл]";
                        case NitroFlameMode.Disabled: return " [Выкл]";
                        default: return " [При нитро]";
                    }
                case TuningOptionKind.DoorLockStatus:
                    int lockState = Function.Call<int>(Hash.GET_VEHICLE_DOOR_LOCK_STATUS, vehicle.Handle);
                    return " [" + (lockState == 2 || lockState == 3 ? "Заперт" : "Открыт") + "]";
                case TuningOptionKind.DoorAll:
                    return " [" + (IsAnyDoorOpen(vehicle) ? "Открыты" : "Закрыты") + "]";
                case TuningOptionKind.DoorHood:
                    return " [" + (IsDoorOpen(vehicle, 4) ? "Открыт" : "Закрыт") + "]";
                case TuningOptionKind.DoorTrunk:
                    return " [" + (IsDoorOpen(vehicle, 5) ? "Открыт" : "Закрыт") + "]";
                case TuningOptionKind.DoorFrontLeft:
                    return " [" + (IsDoorOpen(vehicle, 0) ? "Открыта" : "Закрыта") + "]";
                case TuningOptionKind.DoorFrontRight:
                    return " [" + (IsDoorOpen(vehicle, 1) ? "Открыта" : "Закрыта") + "]";
                case TuningOptionKind.DoorBackLeft:
                    return " [" + (IsDoorOpen(vehicle, 2) ? "Открыта" : "Закрыта") + "]";
                case TuningOptionKind.DoorBackRight:
                    return " [" + (IsDoorOpen(vehicle, 3) ? "Открыта" : "Закрыта") + "]";
                case TuningOptionKind.WindowsAll:
                    return " [" + (GetWindowsDown(vehicle) ? "Опущены" : "Подняты") + "]";
                case TuningOptionKind.ConvertibleRoof:
                    int roofState = Function.Call<int>(Hash.GET_CONVERTIBLE_ROOF_STATE, vehicle.Handle);
                    return " [" + (roofState == 2 ? "Сложена" : "Поднята") + "]";
                case TuningOptionKind.EngineToggle:
                    return " [" + (Function.Call<bool>(Hash.GET_IS_VEHICLE_ENGINE_RUNNING, vehicle.Handle) ? "Заведен" : "Заглушен") + "]";
                case TuningOptionKind.InteriorLight:
                    return " [" + (_isInteriorLightOn ? "Вкл" : "Выкл") + "]";
                case TuningOptionKind.BrakeLightsToggle:
                    return " [" + (GetForcedBrakeLights(vehicle) ? "Вкл" : "Выкл") + "]";
                case TuningOptionKind.KeepEngineRunning:
                    return " [" + (GetKeepEngineRunning(vehicle) ? "Вкл" : "Выкл") + "]";
                case TuningOptionKind.HandbrakeToggle:
                    return " [" + (IsVehicleHandbraked(vehicle) ? "Вкл" : "Выкл") + "]";
                default:
                    return "";
            }
        }

        private static void ApplyOption(Vehicle vehicle, TuningOption option)
        {
            if (option.Kind == TuningOptionKind.Command)
            {
                RunCommand(vehicle, option.Command);
                return;
            }

            if (option.Kind == TuningOptionKind.BennyConversion)
            {
                ConvertToBennyVehicle(vehicle, option.TargetModel);
                return;
            }

            if (option.Kind == TuningOptionKind.NitroBoostToggle)
            {
                bool curEnabled = VehicleNitroService.GetNitroEnabledForVehicle(vehicle);
                var curMode = VehicleNitroService.GetFlameModeForVehicle(vehicle);
                VehicleNitroService.SetNitroConfigForVehicle(vehicle, !curEnabled, curMode);
                Notifier.Show(!curEnabled ? "Нитро: Включено" : "Нитро: Выключено");
                return;
            }

            if (option.Kind == TuningOptionKind.NitroFlameMode)
            {
                ChangeOption(vehicle, option, 1);
                return;
            }

            if (option.Kind == TuningOptionKind.ToggleMod ||
                option.Kind == TuningOptionKind.BulletproofTires ||
                option.Kind == TuningOptionKind.NeonLights ||
                option.Kind == TuningOptionKind.Extra ||
                option.Kind == TuningOptionKind.HandbrakeToggle)
            {
                ToggleOption(vehicle, option);
                return;
            }

            ChangeOption(vehicle, option, 1);
        }

        private static void ChangeOption(Vehicle vehicle, TuningOption option, int direction)
        {
            switch (option.Kind)
            {
                case TuningOptionKind.BennyConversion:
                    break;
                case TuningOptionKind.Mod:
                    SetVehicleMod(vehicle, option.ModType, Wrap(GetVehicleMod(vehicle, option.ModType) + direction, -1, option.Max));
                    break;
                case TuningOptionKind.ModKit:
                    Function.Call(Hash.SET_VEHICLE_MOD_KIT, vehicle.Handle, GetNextModKit(vehicle, direction, option.Min, option.Max));
                    break;
                case TuningOptionKind.ToggleMod:
                case TuningOptionKind.BulletproofTires:
                case TuningOptionKind.NeonLights:
                case TuningOptionKind.Extra:
                    ToggleOption(vehicle, option);
                    break;
                case TuningOptionKind.WindowTint:
                    Function.Call(Hash.SET_VEHICLE_WINDOW_TINT, vehicle.Handle, Wrap(Function.Call<int>(Hash.GET_VEHICLE_WINDOW_TINT, vehicle.Handle) + direction, option.Min, option.Max));
                    break;
                case TuningOptionKind.PlateType:
                    Function.Call(Hash.SET_VEHICLE_NUMBER_PLATE_TEXT_INDEX, vehicle.Handle, Wrap(Function.Call<int>(Hash.GET_VEHICLE_NUMBER_PLATE_TEXT_INDEX, vehicle.Handle) + direction, option.Min, option.Max));
                    break;
                case TuningOptionKind.PrimaryColor:
                    SetVehicleColors(vehicle, Wrap(GetVehicleColors(vehicle).Primary + direction, option.Min, option.Max), GetVehicleColors(vehicle).Secondary);
                    break;
                case TuningOptionKind.SecondaryColor:
                    SetVehicleColors(vehicle, GetVehicleColors(vehicle).Primary, Wrap(GetVehicleColors(vehicle).Secondary + direction, option.Min, option.Max));
                    break;
                case TuningOptionKind.PearlescentColor:
                    SetExtraColors(vehicle, Wrap(GetExtraColors(vehicle).Pearlescent + direction, option.Min, option.Max), GetExtraColors(vehicle).Wheel);
                    break;
                case TuningOptionKind.WheelColor:
                    SetExtraColors(vehicle, GetExtraColors(vehicle).Pearlescent, Wrap(GetExtraColors(vehicle).Wheel + direction, option.Min, option.Max));
                    break;
                case TuningOptionKind.WheelType:
                    Function.Call(Hash.SET_VEHICLE_WHEEL_TYPE, vehicle.Handle, Wrap(Function.Call<int>(Hash.GET_VEHICLE_WHEEL_TYPE, vehicle.Handle) + direction, option.Min, option.Max));
                    break;
                case TuningOptionKind.XenonColor:
                    SetXenonColor(vehicle, Wrap(Function.Call<int>(Hash.GET_VEHICLE_XENON_LIGHT_COLOR_INDEX, vehicle.Handle) + direction, option.Min, option.Max));
                    break;
                case TuningOptionKind.NeonColor:
                    SetNeonColor(vehicle, Wrap(GetNeonColorIndex(vehicle) + direction, option.Min, option.Max));
                    break;
                case TuningOptionKind.TireSmokeColor:
                    SetTireSmokeColor(vehicle, Wrap(GetTireSmokeColorIndex(vehicle) + direction, option.Min, option.Max));
                    break;
                case TuningOptionKind.Livery:
                    Function.Call(Hash.SET_VEHICLE_LIVERY, vehicle.Handle, Wrap(Function.Call<int>(Hash.GET_VEHICLE_LIVERY, vehicle.Handle) + direction, option.Min, option.Max));
                    break;
                case TuningOptionKind.Livery2:
                    Function.Call(Hash.SET_VEHICLE_LIVERY2, vehicle.Handle, Wrap(Function.Call<int>(Hash.GET_VEHICLE_LIVERY2, vehicle.Handle) + direction, option.Min, option.Max));
                    break;
                case TuningOptionKind.NitroBoostToggle:
                    bool cur = VehicleNitroService.GetNitroEnabledForVehicle(vehicle);
                    var mode = VehicleNitroService.GetFlameModeForVehicle(vehicle);
                    VehicleNitroService.SetNitroConfigForVehicle(vehicle, !cur, mode);
                    Notifier.Show(!cur ? "Нитро: Включено" : "Нитро: Выключено");
                    break;
                case TuningOptionKind.NitroFlameMode:
                    bool nitroState = VehicleNitroService.GetNitroEnabledForVehicle(vehicle);
                    int nextMode = Wrap((int)VehicleNitroService.GetFlameModeForVehicle(vehicle) + direction, 0, 2);
                    VehicleNitroService.SetNitroConfigForVehicle(vehicle, nitroState, (NitroFlameMode)nextMode);
                    string modeName = (NitroFlameMode)nextMode == NitroFlameMode.DuringBoost ? "При нитро"
                                    : (NitroFlameMode)nextMode == NitroFlameMode.AlwaysOn ? "Всегда вкл" : "Выкл";
                    Notifier.Show("Пламя выхлопа: " + modeName);
                    break;
                case TuningOptionKind.DoorLockStatus:
                    int currentLock = Function.Call<int>(Hash.GET_VEHICLE_DOOR_LOCK_STATUS, vehicle.Handle);
                    bool isLocked = currentLock == 2 || currentLock == 3;
                    Function.Call(Hash.SET_VEHICLE_DOORS_LOCKED, vehicle.Handle, isLocked ? 1 : 2);
                    PlayKeyFobBeep(vehicle);
                    Notifier.Show(isLocked ? "Замки открыты" : "Замки заперты");
                    break;
                case TuningOptionKind.DoorAll:
                    ToggleAllDoors(vehicle);
                    break;
                case TuningOptionKind.DoorHood:
                    ToggleDoor(vehicle, 4);
                    break;
                case TuningOptionKind.DoorTrunk:
                    ToggleDoor(vehicle, 5);
                    break;
                case TuningOptionKind.DoorFrontLeft:
                    ToggleDoor(vehicle, 0);
                    break;
                case TuningOptionKind.DoorFrontRight:
                    ToggleDoor(vehicle, 1);
                    break;
                case TuningOptionKind.DoorBackLeft:
                    ToggleDoor(vehicle, 2);
                    break;
                case TuningOptionKind.DoorBackRight:
                    ToggleDoor(vehicle, 3);
                    break;
                case TuningOptionKind.WindowsAll:
                    bool windowsDown = !GetWindowsDown(vehicle);
                    SetWindowsDown(vehicle, windowsDown);
                    if (windowsDown)
                    {
                        Function.Call(Hash.ROLL_DOWN_WINDOWS, vehicle.Handle);
                    }
                    else
                    {
                        for (int w = 0; w < 4; w++) Function.Call(Hash.ROLL_UP_WINDOW, vehicle.Handle, w);
                    }
                    break;
                case TuningOptionKind.ConvertibleRoof:
                    int currentRoof = Function.Call<int>(Hash.GET_CONVERTIBLE_ROOF_STATE, vehicle.Handle);
                    if (currentRoof == 0 || currentRoof == 1 || currentRoof == 3)
                    {
                        Function.Call(Hash.LOWER_CONVERTIBLE_ROOF, vehicle.Handle, false);
                    }
                    else
                    {
                        Function.Call(Hash.RAISE_CONVERTIBLE_ROOF, vehicle.Handle, false);
                    }
                    break;
                case TuningOptionKind.EngineToggle:
                    bool engRunning = Function.Call<bool>(Hash.GET_IS_VEHICLE_ENGINE_RUNNING, vehicle.Handle);
                    Function.Call(Hash.SET_VEHICLE_ENGINE_ON, vehicle.Handle, !engRunning, false, true);
                    break;
                case TuningOptionKind.InteriorLight:
                    _isInteriorLightOn = !_isInteriorLightOn;
                    Function.Call(Hash.SET_VEHICLE_INTERIORLIGHT, vehicle.Handle, _isInteriorLightOn);
                    break;
                case TuningOptionKind.BrakeLightsToggle:
                    bool brakeState = !GetForcedBrakeLights(vehicle);
                    SetForcedBrakeLights(vehicle, brakeState);
                    if (brakeState)
                    {
                        Function.Call(Hash.SET_VEHICLE_BRAKE_LIGHTS, vehicle.Handle, true);
                    }
                    Notifier.Show(brakeState ? "Стоп-сигналы: Принудительно включены (Boost при торможении)" : "Стоп-сигналы: Автоматический режим");
                    break;
                case TuningOptionKind.KeepEngineRunning:
                    bool keepEng = !GetKeepEngineRunning(vehicle);
                    SetKeepEngineRunning(vehicle, keepEng);
                    Notifier.Show(keepEng ? "Не глушить при выходе: Вкл" : "Не глушить при выходе: Выкл");
                    break;
                case TuningOptionKind.HandbrakeToggle:
                    ToggleHandbrake(vehicle);
                    break;
            }
        }

        private static void ConvertToBennyVehicle(Vehicle vehicle, string targetModelName)
        {
            var targetModel = new Model(targetModelName);
            if (!targetModel.IsInCdImage || !targetModel.IsVehicle)
            {
                Notifier.Show("Модель Бенни недоступна: " + targetModelName);
                return;
            }

            if (!targetModel.Request(1000))
            {
                Notifier.Show("Модель Бенни не загрузилась: " + targetModelName);
                return;
            }

            var player = Game.Player.Character;
            var snapshot = CaptureVehicleSnapshot(vehicle);
            var newVehicle = GTA.World.CreateVehicle(targetModel, snapshot.Position, snapshot.Heading);
            if (newVehicle == null || !newVehicle.Exists())
            {
                targetModel.MarkAsNoLongerNeeded();
                Notifier.Show("Не удалось создать " + targetModelName);
                return;
            }

            ApplyVehicleSnapshot(newVehicle, snapshot);
            Function.Call(Hash.SET_PED_INTO_VEHICLE, player.Handle, newVehicle.Handle, -1);
            Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, newVehicle.Handle);

            if (vehicle.Exists())
            {
                vehicle.MarkAsNoLongerNeeded();
                vehicle.Delete();
            }

            targetModel.MarkAsNoLongerNeeded();
            Notifier.Show("Бенни-конверсия: " + targetModelName);
        }

        private static VehicleSnapshot CaptureVehicleSnapshot(Vehicle vehicle)
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

            var snapshot = new VehicleSnapshot
            {
                Position = vehicle.Position,
                Heading = vehicle.Heading,
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
                Neons = new bool[4],
                Mods = new List<VehicleModSnapshot>(),
                Extras = new List<VehicleExtraSnapshot>()
            };

            for (var i = 0; i < snapshot.Neons.Length; i++)
            {
                snapshot.Neons[i] = Function.Call<bool>(Hash.GET_VEHICLE_NEON_ENABLED, vehicle.Handle, i);
            }

            for (var modType = 0; modType <= ModSettings.MaxVehicleModType; modType++)
            {
                snapshot.Mods.Add(new VehicleModSnapshot
                {
                    Type = modType,
                    Value = Function.Call<int>(Hash.GET_VEHICLE_MOD, vehicle.Handle, modType),
                    Variation = Function.Call<bool>(Hash.GET_VEHICLE_MOD_VARIATION, vehicle.Handle, modType),
                    IsToggle = ToggleModTypes.Contains(modType),
                    ToggleValue = ToggleModTypes.Contains(modType) &&
                                  Function.Call<bool>(Hash.IS_TOGGLE_MOD_ON, vehicle.Handle, modType)
                });
            }

            for (var extraId = 1; extraId <= MaxExtraId; extraId++)
            {
                if (!Function.Call<bool>(Hash.DOES_EXTRA_EXIST, vehicle.Handle, extraId))
                {
                    continue;
                }

                snapshot.Extras.Add(new VehicleExtraSnapshot
                {
                    Id = extraId,
                    Enabled = Function.Call<bool>(Hash.IS_VEHICLE_EXTRA_TURNED_ON, vehicle.Handle, extraId)
                });
            }

            return snapshot;
        }

        private static void ApplyVehicleSnapshot(Vehicle vehicle, VehicleSnapshot snapshot)
        {
            EnsureModKitSelected(vehicle);
            Function.Call(Hash.SET_VEHICLE_WHEEL_TYPE, vehicle.Handle, snapshot.WheelType);
            Function.Call(Hash.SET_VEHICLE_WINDOW_TINT, vehicle.Handle, snapshot.WindowTint);
            Function.Call(Hash.SET_VEHICLE_NUMBER_PLATE_TEXT_INDEX, vehicle.Handle, snapshot.PlateType);

            if (!string.IsNullOrWhiteSpace(snapshot.PlateText))
            {
                Function.Call(Hash.SET_VEHICLE_NUMBER_PLATE_TEXT, vehicle.Handle, snapshot.PlateText);
            }

            Function.Call(Hash.SET_VEHICLE_COLOURS, vehicle.Handle, snapshot.PrimaryColor, snapshot.SecondaryColor);
            Function.Call(Hash.SET_VEHICLE_EXTRA_COLOURS, vehicle.Handle, snapshot.PearlescentColor, snapshot.WheelColor);

            foreach (var mod in snapshot.Mods)
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

            foreach (var extra in snapshot.Extras)
            {
                if (Function.Call<bool>(Hash.DOES_EXTRA_EXIST, vehicle.Handle, extra.Id))
                {
                    Function.Call(Hash.SET_VEHICLE_EXTRA, vehicle.Handle, extra.Id, !extra.Enabled);
                }
            }

            if (Function.Call<int>(Hash.GET_VEHICLE_LIVERY_COUNT, vehicle.Handle) > 0)
            {
                Function.Call(Hash.SET_VEHICLE_LIVERY, vehicle.Handle, snapshot.Livery);
            }

            if (Function.Call<int>(Hash.GET_VEHICLE_LIVERY2_COUNT, vehicle.Handle) > 0)
            {
                Function.Call(Hash.SET_VEHICLE_LIVERY2, vehicle.Handle, snapshot.Livery2);
            }

            Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, vehicle.Handle, !snapshot.BulletproofTires);
            Function.Call(Hash.SET_VEHICLE_XENON_LIGHT_COLOR_INDEX, vehicle.Handle, snapshot.XenonColor);
            Function.Call(Hash.SET_VEHICLE_TYRE_SMOKE_COLOR, vehicle.Handle, snapshot.TireSmokeRed, snapshot.TireSmokeGreen, snapshot.TireSmokeBlue);
            Function.Call(Hash.SET_VEHICLE_NEON_COLOUR, vehicle.Handle, snapshot.NeonRed, snapshot.NeonGreen, snapshot.NeonBlue);

            for (var i = 0; i < snapshot.Neons.Length; i++)
            {
                Function.Call(Hash.SET_VEHICLE_NEON_ENABLED, vehicle.Handle, i, snapshot.Neons[i]);
            }
        }

        private static void ToggleOption(Vehicle vehicle, TuningOption option)
        {
            switch (option.Kind)
            {
                case TuningOptionKind.ToggleMod:
                    Function.Call(Hash.TOGGLE_VEHICLE_MOD, vehicle.Handle, option.ModType, !IsToggleModOn(vehicle, option.ModType));
                    break;
                case TuningOptionKind.BulletproofTires:
                    Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, vehicle.Handle, !Function.Call<bool>(Hash.GET_VEHICLE_TYRES_CAN_BURST, vehicle.Handle));
                    break;
                case TuningOptionKind.NeonLights:
                    SetAllNeons(vehicle, !AreNeonsOn(vehicle));
                    break;
                case TuningOptionKind.Extra:
                    SetExtraOn(vehicle, option.ModType, !IsExtraOn(vehicle, option.ModType));
                    break;
            }
        }

        private static void RunCommand(Vehicle vehicle, TuningCommand command)
        {
            switch (command)
            {
                case TuningCommand.SaveConfig:
                    ConfigStore.Save(vehicle);
                    Notifier.Show("Конфиг машины сохранен");
                    break;
                case TuningCommand.ApplySavedConfig:
                    Notifier.Show(ConfigStore.Apply(vehicle)
                        ? "Сохраненный конфиг применен"
                        : "Для этой машины нет конфига");
                    break;
                case TuningCommand.Repair:
                    vehicle.Repair();
                    Notifier.Show("Транспорт починен");
                    break;
                case TuningCommand.Clean:
                    CleanVehicle(vehicle);
                    Notifier.Show("Машина помыта");
                    break;
                case TuningCommand.MaxPerformance:
                    MaximizePerformanceMods(vehicle);
                    Notifier.Show("Производительность на максимум");
                    break;
                case TuningCommand.MaxAll:
                    MaximizeMods(vehicle);
                    MaximizePerformanceMods(vehicle);
                    Notifier.Show("Тюнинг на максимум");
                    break;
                case TuningCommand.PanicAlarm:
                    Function.Call(Hash.SET_VEHICLE_ALARM, vehicle.Handle, true);
                    Function.Call(Hash.START_VEHICLE_ALARM, vehicle.Handle);
                    Function.Call(Hash.SET_VEHICLE_LIGHTS, vehicle.Handle, 2);
                    Notifier.Show("Поиск на парковке активирован");
                    break;
            }
        }

        private static void MaximizePerformanceMods(Vehicle vehicle)
        {
            EnsureModKitSelected(vehicle);

            foreach (var modType in PerformanceModTypes)
            {
                SetHighestVehicleMod(vehicle, modType);
            }

            Function.Call(Hash.TOGGLE_VEHICLE_MOD, vehicle.Handle, 18, true);
        }

        private static void MaximizeMods(Vehicle vehicle)
        {
            EnsureModKitSelected(vehicle);
            Function.Call(Hash.SET_VEHICLE_WINDOW_TINT, vehicle.Handle, 1);

            for (var modType = 0; modType <= ModSettings.MaxVehicleModType; modType++)
            {
                if (ShouldSkipModType(modType))
                {
                    continue;
                }

                SetHighestVehicleMod(vehicle, modType);
            }
        }

        private static bool ShouldSkipModType(int modType)
        {
            return modType == 23 || modType == 24;
        }

        private static void SetHighestVehicleMod(Vehicle vehicle, int modType)
        {
            var numMods = Function.Call<int>(Hash.GET_NUM_VEHICLE_MODS, vehicle.Handle, modType);
            if (numMods > 0)
            {
                Function.Call(Hash.SET_VEHICLE_MOD, vehicle.Handle, modType, numMods - 1, false);
            }
        }

        private static void CleanVehicle(Vehicle vehicle)
        {
            Function.Call(Hash.SET_VEHICLE_DIRT_LEVEL, vehicle.Handle, 0f);
            Function.Call(Hash.WASH_DECALS_FROM_VEHICLE, vehicle.Handle, 1f);
        }

        private static void EnsureModKitSelected(Vehicle vehicle)
        {
            var count = GetModKitCount(vehicle);
            if (count <= 0)
            {
                return;
            }

            var current = Function.Call<int>(Hash.GET_VEHICLE_MOD_KIT, vehicle.Handle);
            if (current >= 0 && current < count)
            {
                return;
            }

            Function.Call(Hash.SET_VEHICLE_MOD_KIT, vehicle.Handle, 0);
        }

        private static int GetModKitCount(Vehicle vehicle)
        {
            return Function.Call<int>(Hash.GET_NUM_MOD_KITS, vehicle.Handle);
        }

        private static int GetCurrentModKit(Vehicle vehicle)
        {
            var modKit = Function.Call<int>(Hash.GET_VEHICLE_MOD_KIT, vehicle.Handle);
            return modKit < 0 ? 0 : modKit;
        }

        private static int GetNextModKit(Vehicle vehicle, int direction, int min, int max)
        {
            var current = Function.Call<int>(Hash.GET_VEHICLE_MOD_KIT, vehicle.Handle);
            if (current < min || current > max)
            {
                return direction >= 0 ? min : max;
            }

            return Wrap(current + direction, min, max);
        }

        private static Vehicle GetCurrentVehicle()
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                return null;
            }

            if (player.IsInVehicle())
            {
                var vehicle = player.CurrentVehicle;
                return vehicle != null && vehicle.Exists() && !vehicle.IsDead ? vehicle : null;
            }

            var lastVeh = player.LastVehicle;
            if (lastVeh != null && lastVeh.Exists() && !lastVeh.IsDead)
            {
                if (player.Position.DistanceTo(lastVeh.Position) <= 25.0f)
                {
                    return lastVeh;
                }
            }

            var nearbyVeh = GTA.World.GetClosestVehicle(player.Position, 18.0f);
            return nearbyVeh != null && nearbyVeh.Exists() && !nearbyVeh.IsDead ? nearbyVeh : null;
        }

        private static void PlayKeyFobBeep(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
            {
                return;
            }

            Function.Call(Hash.PLAY_SOUND_FROM_ENTITY, -1, "Remote_Click", "PI_MENU_SOUNDS", vehicle.Handle, 0, 0, 0);
            Function.Call(Hash.SET_VEHICLE_LIGHTS, vehicle.Handle, 2);
        }

        private static int GetVehicleMod(Vehicle vehicle, int modType)
        {
            return Function.Call<int>(Hash.GET_VEHICLE_MOD, vehicle.Handle, modType);
        }

        private static void SetVehicleMod(Vehicle vehicle, int modType, int value)
        {
            if (value < 0)
            {
                Function.Call(Hash.REMOVE_VEHICLE_MOD, vehicle.Handle, modType);
                return;
            }

            Function.Call(Hash.SET_VEHICLE_MOD, vehicle.Handle, modType, value, false);
        }

        private static bool IsToggleModOn(Vehicle vehicle, int modType)
        {
            return Function.Call<bool>(Hash.IS_TOGGLE_MOD_ON, vehicle.Handle, modType);
        }

        private static VehicleColors GetVehicleColors(Vehicle vehicle)
        {
            var primary = new OutputArgument();
            var secondary = new OutputArgument();
            Function.Call(Hash.GET_VEHICLE_COLOURS, vehicle.Handle, primary, secondary);
            return new VehicleColors(primary.GetResult<int>(), secondary.GetResult<int>());
        }

        private static void SetVehicleColors(Vehicle vehicle, int primary, int secondary)
        {
            Function.Call(Hash.SET_VEHICLE_COLOURS, vehicle.Handle, primary, secondary);
        }

        private static ExtraColors GetExtraColors(Vehicle vehicle)
        {
            var pearlescent = new OutputArgument();
            var wheel = new OutputArgument();
            Function.Call(Hash.GET_VEHICLE_EXTRA_COLOURS, vehicle.Handle, pearlescent, wheel);
            return new ExtraColors(pearlescent.GetResult<int>(), wheel.GetResult<int>());
        }

        private static void SetExtraColors(Vehicle vehicle, int pearlescent, int wheel)
        {
            Function.Call(Hash.SET_VEHICLE_EXTRA_COLOURS, vehicle.Handle, pearlescent, wheel);
        }

        private static int GetWindowTintCount(Vehicle vehicle)
        {
            var count = Function.Call<int>(Hash.GET_NUM_VEHICLE_WINDOW_TINTS, vehicle.Handle);
            return count > 0 ? count : 1;
        }

        private static int GetNumberPlateCount()
        {
            var count = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_NUMBER_PLATES);
            return count > 0 ? count : 1;
        }

        private static int GetVehicleColorCount()
        {
            var count = Function.Call<int>(Hash.GET_NUMBER_OF_VEHICLE_COLOURS);
            return count > 0 ? count : MaxVehicleColor + 1;
        }

        private static bool AreNeonsOn(Vehicle vehicle)
        {
            for (var i = 0; i < 4; i++)
            {
                if (Function.Call<bool>(Hash.GET_VEHICLE_NEON_ENABLED, vehicle.Handle, i))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetAllNeons(Vehicle vehicle, bool enabled)
        {
            for (var i = 0; i < 4; i++)
            {
                Function.Call(Hash.SET_VEHICLE_NEON_ENABLED, vehicle.Handle, i, enabled);
            }
        }

        private static bool IsExtraOn(Vehicle vehicle, int extraId)
        {
            return Function.Call<bool>(Hash.IS_VEHICLE_EXTRA_TURNED_ON, vehicle.Handle, extraId);
        }

        private static void SetExtraOn(Vehicle vehicle, int extraId, bool enabled)
        {
            Function.Call(Hash.SET_VEHICLE_EXTRA, vehicle.Handle, extraId, !enabled);
        }

        private static string GetXenonColorText(int colorIndex)
        {
            if (colorIndex < 0)
            {
                return "Стандарт";
            }

            return colorIndex < LightColorPresets.Length
                ? LightColorPresets[colorIndex].Name
                : colorIndex.ToString();
        }

        private static void SetXenonColor(Vehicle vehicle, int colorIndex)
        {
            Function.Call(Hash.TOGGLE_VEHICLE_MOD, vehicle.Handle, 22, true);
            Function.Call(Hash.SET_VEHICLE_XENON_LIGHT_COLOR_INDEX, vehicle.Handle, colorIndex);
        }

        private static int GetNeonColorIndex(Vehicle vehicle)
        {
            var red = new OutputArgument();
            var green = new OutputArgument();
            var blue = new OutputArgument();
            Function.Call(Hash.GET_VEHICLE_NEON_COLOUR, vehicle.Handle, red, green, blue);

            return GetClosestLightColorIndex(
                red.GetResult<int>(),
                green.GetResult<int>(),
                blue.GetResult<int>());
        }

        private static void SetNeonColor(Vehicle vehicle, int colorIndex)
        {
            var preset = LightColorPresets[colorIndex];
            SetAllNeons(vehicle, true);
            Function.Call(Hash.SET_VEHICLE_NEON_COLOUR, vehicle.Handle, preset.Red, preset.Green, preset.Blue);
        }

        private static int GetTireSmokeColorIndex(Vehicle vehicle)
        {
            var red = new OutputArgument();
            var green = new OutputArgument();
            var blue = new OutputArgument();
            Function.Call(Hash.GET_VEHICLE_TYRE_SMOKE_COLOR, vehicle.Handle, red, green, blue);

            return GetClosestLightColorIndex(
                red.GetResult<int>(),
                green.GetResult<int>(),
                blue.GetResult<int>());
        }

        private static void SetTireSmokeColor(Vehicle vehicle, int colorIndex)
        {
            var preset = LightColorPresets[colorIndex];
            Function.Call(Hash.TOGGLE_VEHICLE_MOD, vehicle.Handle, 20, true);
            Function.Call(Hash.SET_VEHICLE_TYRE_SMOKE_COLOR, vehicle.Handle, preset.Red, preset.Green, preset.Blue);
        }

        private static int GetClosestLightColorIndex(int red, int green, int blue)
        {
            var bestIndex = 0;
            var bestDistance = int.MaxValue;

            for (var i = 0; i < LightColorPresets.Length; i++)
            {
                var preset = LightColorPresets[i];
                var redDelta = red - preset.Red;
                var greenDelta = green - preset.Green;
                var blueDelta = blue - preset.Blue;
                var distance = redDelta * redDelta + greenDelta * greenDelta + blueDelta * blueDelta;

                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestIndex = i;
            }

            return bestIndex;
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

        public void Update()
        {
            var character = Game.Player.Character;
            if (character != null && character.Exists())
            {
                var veh = character.LastVehicle;
                if (veh != null && veh.Exists() && veh.IsDriveable && !veh.IsDead)
                {
                    if (GetKeepEngineRunning(veh) && !veh.IsEngineRunning && character.CurrentVehicle == null)
                    {
                        Function.Call(Hash.SET_VEHICLE_ENGINE_ON, veh.Handle, true, true, false);
                    }
                    if (GetForcedBrakeLights(veh))
                    {
                        Function.Call(Hash.SET_VEHICLE_BRAKE_LIGHTS, veh.Handle, true);

                        bool isPedalBraking = character.CurrentVehicle == veh && Game.IsControlPressed(GTA.Control.VehicleBrake);
                        if (isPedalBraking)
                        {
                            DrawRearBrakeLightBoost(veh);
                        }
                    }
                }
            }
        }

        private static void DrawRearBrakeLightBoost(Vehicle veh)
        {
            if (veh == null || !veh.Exists()) return;

            bool drawn = false;
            Vector3 backwardOffset = -veh.ForwardVector * 0.15f;
            string[] lightBones = { "taillight_l", "taillight_r", "brakelight_l", "brakelight_r" };

            for (int i = 0; i < lightBones.Length; i++)
            {
                int boneIndex = Function.Call<int>(Hash.GET_ENTITY_BONE_INDEX_BY_NAME, veh.Handle, lightBones[i]);
                if (boneIndex != -1)
                {
                    Vector3 bonePos = Function.Call<Vector3>(Hash.GET_WORLD_POSITION_OF_ENTITY_BONE, veh.Handle, boneIndex);
                    if (bonePos != Vector3.Zero)
                    {
                        Vector3 pos = bonePos + backwardOffset;
                        Function.Call(Hash.DRAW_LIGHT_WITH_RANGE, pos.X, pos.Y, pos.Z, 255, 20, 20, 1.2f, 1.8f);
                        drawn = true;
                    }
                }
            }

            if (!drawn)
            {
                Vector3 rearLeft = veh.GetOffsetPosition(new Vector3(-0.75f, -2.25f, 0.2f));
                Vector3 rearRight = veh.GetOffsetPosition(new Vector3(0.75f, -2.25f, 0.2f));
                Function.Call(Hash.DRAW_LIGHT_WITH_RANGE, rearLeft.X, rearLeft.Y, rearLeft.Z, 255, 20, 20, 1.2f, 1.8f);
                Function.Call(Hash.DRAW_LIGHT_WITH_RANGE, rearRight.X, rearRight.Y, rearRight.Z, 255, 20, 20, 1.2f, 1.8f);
            }
        }

        public static bool GetForcedBrakeLights(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return false;
            return (VehicleForcedBrakeLights.TryGetValue(vehicle.Handle, out var valByHandle) && valByHandle)
                || (VehicleForcedBrakeLights.TryGetValue(vehicle.Model.Hash, out var valByModel) && valByModel);
        }

        public static void SetForcedBrakeLights(Vehicle vehicle, bool value)
        {
            if (vehicle == null || !vehicle.Exists()) return;
            VehicleForcedBrakeLights[vehicle.Handle] = value;
            VehicleForcedBrakeLights[vehicle.Model.Hash] = value;
        }

        public static bool GetKeepEngineRunning(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return false;
            return VehicleKeepEngineRunning.TryGetValue(vehicle.Model.Hash, out var val) && val;
        }

        public static void SetKeepEngineRunning(Vehicle vehicle, bool value)
        {
            if (vehicle == null || !vehicle.Exists()) return;
            VehicleKeepEngineRunning[vehicle.Model.Hash] = value;
        }

        public static bool GetWindowsDown(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return false;
            return VehicleWindowsDown.TryGetValue(vehicle.Model.Hash, out var val) && val;
        }

        public static void SetWindowsDown(Vehicle vehicle, bool value)
        {
            if (vehicle == null || !vehicle.Exists()) return;
            VehicleWindowsDown[vehicle.Model.Hash] = value;
        }

        private static bool IsDoorOpen(Vehicle vehicle, int doorIndex)
        {
            return Function.Call<float>(Hash.GET_VEHICLE_DOOR_ANGLE_RATIO, vehicle.Handle, doorIndex) > 0.1f;
        }

        private static void ToggleDoor(Vehicle vehicle, int doorIndex)
        {
            if (IsDoorOpen(vehicle, doorIndex))
            {
                Function.Call(Hash.SET_VEHICLE_DOOR_SHUT, vehicle.Handle, doorIndex, false);
            }
            else
            {
                Function.Call(Hash.SET_VEHICLE_DOOR_OPEN, vehicle.Handle, doorIndex, false, false);
            }
        }

        private static bool IsAnyDoorOpen(Vehicle vehicle)
        {
            for (int i = 0; i <= 5; i++)
            {
                if (Function.Call<bool>(Hash.GET_IS_DOOR_VALID, vehicle.Handle, i) && IsDoorOpen(vehicle, i))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ToggleAllDoors(Vehicle vehicle)
        {
            bool anyOpen = IsAnyDoorOpen(vehicle);
            for (int i = 0; i <= 5; i++)
            {
                if (Function.Call<bool>(Hash.GET_IS_DOOR_VALID, vehicle.Handle, i))
                {
                    if (anyOpen)
                    {
                        Function.Call(Hash.SET_VEHICLE_DOOR_SHUT, vehicle.Handle, i, false);
                    }
                    else
                    {
                        Function.Call(Hash.SET_VEHICLE_DOOR_OPEN, vehicle.Handle, i, false, false);
                    }
                }
            }
        }

        private static int GetMaxPage(int itemCount)
        {
            return itemCount == 0
                ? 0
                : (itemCount - 1) / ItemsPerPage;
        }

        private static string FormatModValue(int value, int max)
        {
            return value < 0
                ? "Сток"
                : (value + 1) + "/" + (max + 1);
        }

        private static string GetWheelTypeText(int wheelType)
        {
            return wheelType >= 0 && wheelType < WheelTypeNames.Length
                ? WheelTypeNames[wheelType]
                : wheelType.ToString();
        }

        private static string GetModTypeName(int modType)
        {
            switch (modType)
            {
                case 0: return "Спойлер";
                case 1: return "Передний бампер";
                case 2: return "Задний бампер";
                case 3: return "Пороги";
                case 4: return "Выхлоп";
                case 5: return "Каркас";
                case 6: return "Решетка";
                case 7: return "Капот";
                case 8: return "Левое крыло";
                case 9: return "Правое крыло";
                case 10: return "Крыша";
                case 11: return "Двигатель";
                case 12: return "Тормоза";
                case 13: return "Трансмиссия";
                case 14: return "Сигнал";
                case 15: return "Подвеска";
                case 16: return "Броня";
                case 23: return "Передние диски";
                case 24: return "Задние диски";
                case 25: return "Крепление номера";
                case 26: return "Номерные таблички";
                case 27: return "Отделка";
                case 28: return "Украшения";
                case 29: return "Панель приборов";
                case 30: return "Шкалы";
                case 31: return "Дверные динамики";
                case 32: return "Сиденья";
                case 33: return "Руль";
                case 34: return "Рычаг КПП";
                case 35: return "Таблички";
                case 36: return "Динамики";
                case 37: return "Багажник";
                case 38: return "Гидравлика";
                case 39: return "Блок двигателя";
                case 40: return "Воздушный фильтр";
                case 41: return "Распорки";
                case 42: return "Расширители арок";
                case 43: return "Антенны";
                case 44: return "Молдинги";
                case 45: return "Бак";
                case 46: return "Окна";
                case 47: return "Ливрея";
                case 48: return "Ливрея 2";
                case 49: return "Световая балка";
                default: return "Мод " + modType;
            }
        }

        private sealed class BennyConversion
        {
            public readonly string TargetModel;
            public readonly string DisplayName;
            public readonly int SourceHash;
            public readonly int TargetHash;

            public BennyConversion(string sourceModel, string targetModel, string displayName)
            {
                TargetModel = targetModel;
                DisplayName = displayName;
                SourceHash = new Model(sourceModel).Hash;
                TargetHash = new Model(targetModel).Hash;
            }
        }

        private struct VehicleSnapshot
        {
            public Vector3 Position;
            public float Heading;
            public int WheelType;
            public int WindowTint;
            public int PlateType;
            public string PlateText;
            public int PrimaryColor;
            public int SecondaryColor;
            public int PearlescentColor;
            public int WheelColor;
            public int XenonColor;
            public int Livery;
            public int Livery2;
            public bool BulletproofTires;
            public int NeonRed;
            public int NeonGreen;
            public int NeonBlue;
            public int TireSmokeRed;
            public int TireSmokeGreen;
            public int TireSmokeBlue;
            public bool[] Neons;
            public List<VehicleModSnapshot> Mods;
            public List<VehicleExtraSnapshot> Extras;
        }

        private struct VehicleModSnapshot
        {
            public int Type;
            public int Value;
            public bool Variation;
            public bool IsToggle;
            public bool ToggleValue;
        }

        private struct VehicleExtraSnapshot
        {
            public int Id;
            public bool Enabled;
        }

        private struct ColorPreset
        {
            public readonly string Name;
            public readonly int Red;
            public readonly int Green;
            public readonly int Blue;

            public ColorPreset(string name, int red, int green, int blue)
            {
                Name = name;
                Red = red;
                Green = green;
                Blue = blue;
            }
        }

        private enum TuningCategory
        {
            None,
            Quick,
            Doors,
            Nitro,
            Benny,
            ModKits,
            Performance,
            Body,
            Paint,
            Plates,
            Wheels,
            Lights,
            Interior,
            EngineBay,
            Liveries,
            Extras,
            Misc
        }

        private struct TuningCategoryDefinition
        {
            public readonly TuningCategory Kind;
            public readonly string Name;
            public readonly int Count;
            public readonly string StatusText;
            public readonly bool IsEnabled;

            public TuningCategoryDefinition(TuningCategory kind, string name, int count, string statusText, bool isEnabled)
            {
                Kind = kind;
                Name = name;
                Count = count;
                StatusText = statusText;
                IsEnabled = isEnabled;
            }
        }

        private enum TuningOptionKind
        {
            Command,
            BennyConversion,
            Mod,
            ModKit,
            Extra,
            ToggleMod,
            WindowTint,
            PlateType,
            PrimaryColor,
            SecondaryColor,
            PearlescentColor,
            WheelColor,
            WheelType,
            XenonColor,
            NeonColor,
            TireSmokeColor,
            Livery,
            Livery2,
            BulletproofTires,
            NeonLights,
            NitroBoostToggle,
            NitroFlameMode,
            DoorLockStatus,
            DoorAll,
            DoorHood,
            DoorTrunk,
            DoorFrontLeft,
            DoorFrontRight,
            DoorBackLeft,
            DoorBackRight,
            WindowsAll,
            ConvertibleRoof,
            EngineToggle,
            InteriorLight,
            BrakeLightsToggle,
            KeepEngineRunning,
            HandbrakeToggle
        }

        private enum TuningCommand
        {
            None,
            SaveConfig,
            ApplySavedConfig,
            Repair,
            Clean,
            MaxPerformance,
            MaxAll,
            PanicAlarm
        }

        private struct TuningOption
        {
            public readonly string Name;
            public readonly TuningOptionKind Kind;
            public readonly TuningCommand Command;
            public readonly int ModType;
            public readonly int Min;
            public readonly int Max;
            public readonly string TargetModel;

            private TuningOption(string name, TuningOptionKind kind, TuningCommand command, int modType, int min, int max, string targetModel = null)
            {
                Name = name;
                Kind = kind;
                Command = command;
                ModType = modType;
                Min = min;
                Max = max;
                TargetModel = targetModel;
            }

            public bool IsAvailable
            {
                get { return Kind == TuningOptionKind.Command || Max >= Min; }
            }

            public static TuningOption CreateCommand(string name, TuningCommand command)
            {
                return new TuningOption(name, TuningOptionKind.Command, command, -1, 0, 0);
            }

            public static TuningOption BennyConversion(string name, string targetModel)
            {
                return new TuningOption(name, TuningOptionKind.BennyConversion, TuningCommand.None, -1, 0, 0, targetModel);
            }

            public static TuningOption Mod(string name, int modType, int count)
            {
                return new TuningOption(name, TuningOptionKind.Mod, TuningCommand.None, modType, -1, count - 1);
            }

            public static TuningOption Extra(string name, int extraId)
            {
                return new TuningOption(name, TuningOptionKind.Extra, TuningCommand.None, extraId, 0, 1);
            }

            public static TuningOption Toggle(string name, int modType)
            {
                return new TuningOption(name, TuningOptionKind.ToggleMod, TuningCommand.None, modType, 0, 1);
            }

            public static TuningOption SpecialToggle(string name, TuningOptionKind kind)
            {
                return new TuningOption(name, kind, TuningCommand.None, -1, 0, 1);
            }

            public static TuningOption Range(string name, TuningOptionKind kind, int min, int max)
            {
                return new TuningOption(name, kind, TuningCommand.None, -1, min, max);
            }
        }

        private struct VehicleColors
        {
            public readonly int Primary;
            public readonly int Secondary;

            public VehicleColors(int primary, int secondary)
            {
                Primary = primary;
                Secondary = secondary;
            }
        }

        private struct ExtraColors
        {
            public readonly int Pearlescent;
            public readonly int Wheel;

            public ExtraColors(int pearlescent, int wheel)
            {
                Pearlescent = pearlescent;
                Wheel = wheel;
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
