using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;
using gta.Peds;
using gta.Vehicles;

namespace gta.Player
{
    internal sealed class PlayerInteractionMenuService
    {
        private enum MenuCategory
        {
            Root,
            PlayerStatus,
            KeyFob,
            Weapons,
            Appearance,
            SavedOutfits,
            Companions,
            PoliceOfficer
        }

        private readonly PlayerCheatService _playerCheats;
        private readonly ClothingService _clothing;
        private readonly AnimalMorphService _animalMorph;
        private readonly BongService _bong;
        private readonly BulletTimeService _bulletTime;
        private readonly NoClipService _noClip;
        private readonly TelekinesisService _telekinesis;
        private readonly WeaponService _weapons;
        private readonly CompanionService _companions;
        private readonly VehicleService _vehicles;
        private readonly VehicleUpgradeService _vehicleUpgrades;
        private readonly PoliceOfficerService _policeOfficer;
        private readonly OutfitStore _outfitStore;
        private readonly MenuNavigator<MenuCategory> _nav = new MenuNavigator<MenuCategory>(MenuCategory.Root);

        private bool _isMenuVisible;

        // Player toggles
        private bool _isInvisible;
        private bool _isWantedFrozen;
        private int _frozenWantedStars;
        private bool _isSuperJump;
        private bool _isSuperRun;
        private bool _isInfiniteAmmo;
        private bool _isFireAmmo;
        private bool _isExplosiveAmmo;
        private bool _isExplosiveMelee;

        public PlayerInteractionMenuService(
            PlayerCheatService playerCheats,
            ClothingService clothing,
            AnimalMorphService animalMorph,
            BongService bong,
            BulletTimeService bulletTime,
            NoClipService noClip,
            TelekinesisService telekinesis,
            WeaponService weapons,
            CompanionService companions,
            VehicleService vehicles,
            VehicleUpgradeService vehicleUpgrades,
            PoliceOfficerService policeOfficer,
            OutfitStore outfitStore)
        {
            _playerCheats = playerCheats;
            _clothing = clothing;
            _animalMorph = animalMorph;
            _bong = bong;
            _bulletTime = bulletTime;
            _noClip = noClip;
            _telekinesis = telekinesis;
            _weapons = weapons;
            _companions = companions;
            _vehicles = vehicles;
            _vehicleUpgrades = vehicleUpgrades;
            _policeOfficer = policeOfficer;
            _outfitStore = outfitStore;
        }

        public bool IsMenuVisible => _isMenuVisible;

        public void ToggleMenu()
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            // If player is inside vehicle, delegate to vehicle upgrades / tuning
            if (player.IsInVehicle())
            {
                _isMenuVisible = false;
                _vehicleUpgrades.ToggleMenu();
                return;
            }

            _isMenuVisible = !_isMenuVisible;
            if (_isMenuVisible)
            {
                Notifier.Show("~b~Меню взаимодействия~s~ открыто");
            }
            else
            {
                Notifier.Show("Меню взаимодействия закрыто");
            }
        }

        public void Update()
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            // Close player interaction menu automatically if entering vehicle
            if (_isMenuVisible && player.IsInVehicle())
            {
                _isMenuVisible = false;
                return;
            }

            UpdateWantedLevelControl();

            if (_isSuperJump)
            {
                Function.Call(Hash.SET_SUPER_JUMP_THIS_FRAME, Game.Player.Handle);
            }

            if (_isSuperRun)
            {
                Function.Call(Hash.SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, Game.Player.Handle, 1.49f);
                Function.Call(Hash.SET_SWIM_MULTIPLIER_FOR_PLAYER, Game.Player.Handle, 1.49f);
            }

            if (_isInfiniteAmmo && player.Weapons.Current != null)
            {
                player.Weapons.Current.InfiniteAmmo = true;
                player.Weapons.Current.InfiniteAmmoClip = true;
            }

            if (_isFireAmmo)
            {
                Function.Call(Hash.SET_FIRE_AMMO_THIS_FRAME, Game.Player.Handle);
            }

            if (_isExplosiveAmmo)
            {
                Function.Call(Hash.SET_EXPLOSIVE_AMMO_THIS_FRAME, Game.Player.Handle);
            }

            if (_isExplosiveMelee)
            {
                Function.Call(Hash.SET_EXPLOSIVE_MELEE_THIS_FRAME, Game.Player.Handle);
            }
        }

        private void UpdateWantedLevelControl()
        {
            if (_isWantedFrozen)
            {
                // Locked/Fixed mode: forces exact stars every frame
                Function.Call(Hash.SET_MAX_WANTED_LEVEL, 5);
                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player.Handle, _frozenWantedStars, false);
                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player.Handle, false);
                if (_frozenWantedStars == 0)
                {
                    Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player.Handle, true);
                }
                else
                {
                    Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player.Handle, false);
                }
            }
            else
            {
                // Limiter/Cap mode: allows natural pursuit from 0 up to _frozenWantedStars, but never higher!
                if (_frozenWantedStars == 0)
                {
                    Function.Call(Hash.SET_MAX_WANTED_LEVEL, 0);
                    Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player.Handle, 0, false);
                    Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player.Handle, false);
                    Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player.Handle, true);
                }
                else
                {
                    Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player.Handle, false);
                    Function.Call(Hash.SET_MAX_WANTED_LEVEL, _frozenWantedStars);
                    int currentWanted = Function.Call<int>(Hash.GET_PLAYER_WANTED_LEVEL, Game.Player.Handle);
                    if (currentWanted > _frozenWantedStars)
                    {
                        Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player.Handle, _frozenWantedStars, false);
                        Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player.Handle, false);
                    }
                }
            }
        }

        public void Draw()
        {
            if (!_isMenuVisible) return;

            var items = GetCurrentMenuItems();
            if (items.Count == 0) return;

            _nav.ClampIndex(items.Count);

            string title = GetCategoryTitle(_nav.CurrentCategory);
            string menuText = title + "\n\n";

            for (int i = 0; i < items.Count; i++)
            {
                if (i == _nav.CurrentIndex)
                {
                    menuText += "~b~> " + items[i].Text + "~s~\n";
                }
                else
                {
                    menuText += "  " + items[i].Text + "\n";
                }
            }

            menuText += "\n~g~[8/2/Вверх/Вниз]~s~ Навигация  ~g~[4/6/Влево/Вправо]~s~ Выбор\n~g~[5/Enter]~s~ Переключить  ~g~[0/Back]~s~ Назад  ~g~[X/Esc]~s~ Закрыть";

            MenuPanelRenderer.Draw(menuText, new PointF(10, 10), 0.42f);
        }

        public void ProcessKey(KeyEventArgs e)
        {
            if (!_isMenuVisible) return;

            var items = GetCurrentMenuItems();
            if (items.Count == 0) return;

            _nav.ClampIndex(items.Count);

            switch (e.KeyCode)
            {
                case Keys.X:
                case Keys.Escape:
                    _isMenuVisible = false;
                    Notifier.Show("Меню закрыто");
                    break;

                case Keys.NumPad0:
                case Keys.Back:
                    if (_nav.CanGoBack)
                    {
                        _nav.GoBack();
                    }
                    else
                    {
                        _isMenuVisible = false;
                        Notifier.Show("Меню закрыто");
                    }
                    break;

                case Keys.NumPad8:
                case Keys.Up:
                    _nav.MovePrevious(items.Count);
                    break;

                case Keys.NumPad2:
                case Keys.Down:
                    _nav.MoveNext(items.Count);
                    break;

                case Keys.NumPad5:
                case Keys.Enter:
                    if (_nav.CurrentIndex >= 0 && _nav.CurrentIndex < items.Count)
                    {
                        items[_nav.CurrentIndex].SelectAction?.Invoke();
                    }
                    break;

                case Keys.NumPad4:
                case Keys.Left:
                    if (_nav.CurrentIndex >= 0 && _nav.CurrentIndex < items.Count)
                    {
                        if (items[_nav.CurrentIndex].LeftAction != null)
                        {
                            items[_nav.CurrentIndex].LeftAction.Invoke();
                        }
                        else
                        {
                            items[_nav.CurrentIndex].SelectAction?.Invoke();
                        }
                    }
                    break;

                case Keys.NumPad6:
                case Keys.Right:
                    if (_nav.CurrentIndex >= 0 && _nav.CurrentIndex < items.Count)
                    {
                        if (items[_nav.CurrentIndex].RightAction != null)
                        {
                            items[_nav.CurrentIndex].RightAction.Invoke();
                        }
                        else
                        {
                            items[_nav.CurrentIndex].SelectAction?.Invoke();
                        }
                    }
                    break;
            }
        }

        private struct MenuItem
        {
            public string Text;
            public Action SelectAction;
            public Action LeftAction;
            public Action RightAction;

            public MenuItem(string text, Action selectAction, Action leftAction = null, Action rightAction = null)
            {
                Text = text;
                SelectAction = selectAction;
                LeftAction = leftAction;
                RightAction = rightAction;
            }
        }

        private List<MenuItem> GetCurrentMenuItems()
        {
            var list = new List<MenuItem>();

            switch (_nav.CurrentCategory)
            {
                case MenuCategory.Root:
                    list.Add(new MenuItem("Персонаж и Состояние >>", () => SwitchCategory(MenuCategory.PlayerStatus)));
                    list.Add(new MenuItem("Брелок автомобиля >>", () => SwitchCategory(MenuCategory.KeyFob)));
                    list.Add(new MenuItem("Оружие и Способности >>", () => SwitchCategory(MenuCategory.Weapons)));
                    list.Add(new MenuItem("Облик и Гардероб >>", () => SwitchCategory(MenuCategory.Appearance)));
                    list.Add(new MenuItem("Телохранители и Охрана >>", () => SwitchCategory(MenuCategory.Companions)));
                    list.Add(new MenuItem("Полиция и Спецназ LSPD >>", () => SwitchCategory(MenuCategory.PoliceOfficer)));
                    list.Add(new MenuItem(_noClip.IsEnabled ? "Режим NoClip [~g~ВКЛ~s~]" : "Режим NoClip [~r~ВЫКЛ~s~]", () =>
                    {
                        _noClip.Toggle();
                    }));
                    list.Add(new MenuItem(_bulletTime.IsActive ? "Замедление времени [~g~ВКЛ~s~]" : "Замедление времени [~r~ВЫКЛ~s~]", () =>
                    {
                        _bulletTime.Toggle();
                    }));
                    break;

                case MenuCategory.PlayerStatus:
                    list.Add(new MenuItem(_playerCheats.IsEnabled ? "Бессмертие (God Mode) [~g~ВКЛ~s~]" : "Бессмертие (God Mode) [~r~ВЫКЛ~s~]", () =>
                    {
                        _playerCheats.IsEnabled = !_playerCheats.IsEnabled;
                        if (_playerCheats.IsEnabled)
                        {
                            _playerCheats.Apply();
                            Notifier.Show("Бессмертие: ~g~Включено");
                        }
                        else
                        {
                            _playerCheats.Disable();
                            Notifier.Show("Бессмертие: ~r~Выключено");
                        }
                    }));

                    list.Add(new MenuItem("Восстановить Здоровье и Броню", () =>
                    {
                        var p = Game.Player.Character;
                        if (p != null && p.Exists())
                        {
                            p.Health = p.MaxHealth;
                            p.Armor = ModSettings.MaxStat;
                            p.ClearVisibleDamage();
                            Game.Player.RefillSpecialAbility();
                            Notifier.Show("~g~Здоровье и броня восстановлены на 100%");
                        }
                    }));

                    string starStr = _frozenWantedStars == 0 ? "0 * (Чисто)" : $"{_frozenWantedStars} *";
                    string freezeStatus = _isWantedFrozen ? "[~g~ВКЛ: Фиксация~s~]" : "[~y~ВЫКЛ: Лимит~s~]";
                    list.Add(new MenuItem($"Заморозка розыска: [ ~y~{starStr}~s~ ] {freezeStatus}",
                        () =>
                        {
                            _isWantedFrozen = !_isWantedFrozen;
                            if (_isWantedFrozen)
                            {
                                Function.Call(Hash.SET_MAX_WANTED_LEVEL, 5);
                                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player.Handle, _frozenWantedStars, false);
                                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player.Handle, false);
                                if (_frozenWantedStars == 0)
                                {
                                    Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player.Handle, true);
                                    Notifier.Show("~g~Заморозка [ВКЛ]:~s~ 0 * (Игнор копов)");
                                }
                                else
                                {
                                    Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player.Handle, false);
                                    Notifier.Show($"~g~Заморозка [ВКЛ]:~s~ Выдано и зафиксировано ~y~{_frozenWantedStars} *~s~");
                                }
                            }
                            else
                            {
                                if (_frozenWantedStars == 0)
                                {
                                    Function.Call(Hash.SET_MAX_WANTED_LEVEL, 0);
                                    Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player.Handle, 0, false);
                                    Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player.Handle, false);
                                    Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player.Handle, true);
                                    Notifier.Show("~y~Лимит розыска [ВЫКЛ]:~s~ 0 * (Розыск запрещен)");
                                }
                                else
                                {
                                    Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player.Handle, false);
                                    Function.Call(Hash.SET_MAX_WANTED_LEVEL, _frozenWantedStars);
                                    int curWanted = Function.Call<int>(Hash.GET_PLAYER_WANTED_LEVEL, Game.Player.Handle);
                                    if (curWanted > _frozenWantedStars)
                                    {
                                        Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player.Handle, _frozenWantedStars, false);
                                        Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player.Handle, false);
                                    }
                                    Notifier.Show($"~y~Лимит розыска [ВЫКЛ]:~s~ Погоня не превысит ~y~{_frozenWantedStars} *~s~");
                                }
                            }
                        },
                        leftAction: () =>
                        {
                            _frozenWantedStars = Math.Max(0, _frozenWantedStars - 1);
                            if (_isWantedFrozen)
                            {
                                Function.Call(Hash.SET_MAX_WANTED_LEVEL, 5);
                                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player.Handle, _frozenWantedStars, false);
                                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player.Handle, false);
                            }
                            else
                            {
                                Function.Call(Hash.SET_MAX_WANTED_LEVEL, _frozenWantedStars);
                                int curWanted = Function.Call<int>(Hash.GET_PLAYER_WANTED_LEVEL, Game.Player.Handle);
                                if (curWanted > _frozenWantedStars)
                                {
                                    Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player.Handle, _frozenWantedStars, false);
                                    Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player.Handle, false);
                                }
                            }
                        },
                        rightAction: () =>
                        {
                            _frozenWantedStars = Math.Min(5, _frozenWantedStars + 1);
                            if (_isWantedFrozen)
                            {
                                Function.Call(Hash.SET_MAX_WANTED_LEVEL, 5);
                                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, Game.Player.Handle, _frozenWantedStars, false);
                                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player.Handle, false);
                            }
                            else
                            {
                                Function.Call(Hash.SET_MAX_WANTED_LEVEL, _frozenWantedStars);
                            }
                        }
                    ));

                    list.Add(new MenuItem(_isInvisible ? "Невидимость [~g~ВКЛ~s~]" : "Невидимость [~r~ВЫКЛ~s~]", () =>
                    {
                        _isInvisible = !_isInvisible;
                        var p = Game.Player.Character;
                        if (p != null && p.Exists())
                        {
                            Function.Call(Hash.SET_ENTITY_VISIBLE, p.Handle, !_isInvisible, false);
                        }
                        Notifier.Show(_isInvisible ? "Невидимость: ~g~Включена" : "Невидимость: ~r~Выключена");
                    }));

                    list.Add(new MenuItem(_isSuperJump ? "Суперпрыжок [~g~ВКЛ~s~]" : "Суперпрыжок [~r~ВЫКЛ~s~]", () =>
                    {
                        _isSuperJump = !_isSuperJump;
                        Notifier.Show(_isSuperJump ? "Суперпрыжок: ~g~Включен" : "Суперпрыжок: ~r~Выключен");
                    }));

                    list.Add(new MenuItem(_isSuperRun ? "Супербег и плавание [~g~ВКЛ~s~]" : "Супербег и плавание [~r~ВЫКЛ~s~]", () =>
                    {
                        _isSuperRun = !_isSuperRun;
                        if (!_isSuperRun)
                        {
                            Function.Call(Hash.SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, Game.Player.Handle, 1.0f);
                            Function.Call(Hash.SET_SWIM_MULTIPLIER_FOR_PLAYER, Game.Player.Handle, 1.0f);
                        }
                        Notifier.Show(_isSuperRun ? "Супербег: ~g~Включен" : "Супербег: ~r~Выключен");
                    }));

                    list.Add(new MenuItem("Курение бонга (Отдых)", () =>
                    {
                        _isMenuVisible = false;
                        _bong.Start();
                    }));

                    list.Add(new MenuItem("Сесть на пол (Поза по-турецки)", () =>
                    {
                        var p = Game.Player.Character;
                        if (p != null && p.Exists())
                        {
                            const string dict = "amb@world_human_picnic@male@idle_a";
                            if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, dict))
                            {
                                Function.Call(Hash.REQUEST_ANIM_DICT, dict);
                            }
                            Function.Call(Hash.TASK_PLAY_ANIM, p.Handle, dict, "idle_a", 4.0f, -4.0f, -1, 1, 0, false, false, false);
                            _isMenuVisible = false;
                            Notifier.Show("Персонаж сел на пол (движение для отмены)");
                        }
                    }));
                    break;

                case MenuCategory.KeyFob:
                    var veh = GetNearestVehicle(30f);
                    string vehName = veh != null && veh.Exists() ? veh.LocalizedName : "Авто не найдено";
                    list.Add(new MenuItem($"Транспорт: ~y~{vehName}~s~", null));

                    list.Add(new MenuItem("Запустить / Заглушить двигатель", () =>
                    {
                        var v = GetNearestVehicle(30f);
                        if (v == null || !v.Exists()) { Notifier.Show("Поблизости нет транспорта"); return; }
                        v.IsEngineRunning = !v.IsEngineRunning;
                        PlayFobClick(v);
                        Notifier.Show(v.IsEngineRunning ? "Двигатель: ~g~Запущен" : "Двигатель: ~r~Заглушен");
                    }));

                    list.Add(new MenuItem("Открыть / Закрыть все двери", () =>
                    {
                        var v = GetNearestVehicle(30f);
                        if (v == null || !v.Exists()) { Notifier.Show("Поблизости нет транспорта"); return; }
                        bool anyOpen = v.Doors[VehicleDoorIndex.FrontLeftDoor].IsOpen || v.Doors[VehicleDoorIndex.FrontRightDoor].IsOpen;
                        if (anyOpen)
                        {
                            for (int d = 0; d < 6; d++) v.Doors[(VehicleDoorIndex)d].Close(false);
                            Notifier.Show("Все двери: ~r~Закрыты");
                        }
                        else
                        {
                            for (int d = 0; d < 6; d++) v.Doors[(VehicleDoorIndex)d].Open(false);
                            Notifier.Show("Все двери: ~g~Открыты");
                        }
                        PlayFobClick(v);
                    }));

                    list.Add(new MenuItem("Открыть / Закрыть капот", () =>
                    {
                        var v = GetNearestVehicle(30f);
                        if (v == null || !v.Exists()) { Notifier.Show("Поблизости нет транспорта"); return; }
                        var hood = v.Doors[VehicleDoorIndex.Hood];
                        if (hood.IsOpen) hood.Close(false); else hood.Open(false);
                        PlayFobClick(v);
                    }));

                    list.Add(new MenuItem("Открыть / Закрыть багажник", () =>
                    {
                        var v = GetNearestVehicle(30f);
                        if (v == null || !v.Exists()) { Notifier.Show("Поблизости нет транспорта"); return; }
                        var trunk = v.Doors[VehicleDoorIndex.Trunk];
                        if (trunk.IsOpen) trunk.Close(false); else trunk.Open(false);
                        PlayFobClick(v);
                    }));

                    list.Add(new MenuItem("Сигнализация / Звуковой сигнал", () =>
                    {
                        var v = GetNearestVehicle(30f);
                        if (v == null || !v.Exists()) { Notifier.Show("Поблизости нет транспорта"); return; }
                        v.StartAlarm();
                        Notifier.Show("Сигнализация активирована");
                    }));

                    list.Add(new MenuItem("Призвать авто к себе (Телепорт)", () =>
                    {
                        var v = GetNearestVehicle(150f);
                        var p = Game.Player.Character;
                        if (v == null || !v.Exists() || p == null) { Notifier.Show("Автомобиль слишком далеко"); return; }
                        v.Position = p.Position + p.ForwardVector * 4.5f;
                        v.Heading = p.Heading;
                        v.PlaceOnGround();
                        PlayFobClick(v);
                        Notifier.Show("~g~Автомобиль доставлен к вам");
                    }));

                    list.Add(new MenuItem("Починить и помыть автомобиль", () =>
                    {
                        var v = GetNearestVehicle(30f);
                        if (v == null || !v.Exists()) { Notifier.Show("Поблизости нет транспорта"); return; }
                        v.Repair();
                        v.Wash();
                        PlayFobClick(v);
                        Notifier.Show("~g~Автомобиль полностью отремонтирован и помыт");
                    }));

                    bool isHandbraked = veh != null && veh.Exists() && VehicleUpgradeService.IsVehicleHandbraked(veh);
                    string handbrakeLabel = "Ручной тормоз (Ручник) " + (isHandbraked ? "[~r~ВКЛ~s~]" : "[~g~ВЫКЛ~s~]");
                    list.Add(new MenuItem(handbrakeLabel, () =>
                    {
                        var v = GetNearestVehicle(30f);
                        if (v == null || !v.Exists()) { Notifier.Show("Поблизости нет транспорта"); return; }
                        VehicleUpgradeService.ToggleHandbrake(v);
                        PlayFobClick(v);
                    }));
                    break;

                case MenuCategory.Weapons:
                    list.Add(new MenuItem("Выдать всё оружие и полный боезапас", () =>
                    {
                        var p = Game.Player.Character;
                        if (p != null && p.Exists())
                        {
                            foreach (WeaponHash wh in Enum.GetValues(typeof(WeaponHash)))
                            {
                                if (wh == WeaponHash.Unarmed) continue;
                                p.Weapons.Give(wh, 9999, false, true);
                            }
                            Notifier.Show("~g~Выдан полный арсенал оружия и боеприпасов");
                        }
                    }));

                    list.Add(new MenuItem(_isInfiniteAmmo ? "Бесконечные патроны [~g~ВКЛ~s~]" : "Бесконечные патроны [~r~ВЫКЛ~s~]", () =>
                    {
                        _isInfiniteAmmo = !_isInfiniteAmmo;
                        var p = Game.Player.Character;
                        if (p != null && p.Weapons.Current != null)
                        {
                            p.Weapons.Current.InfiniteAmmo = _isInfiniteAmmo;
                            p.Weapons.Current.InfiniteAmmoClip = _isInfiniteAmmo;
                        }
                        Notifier.Show(_isInfiniteAmmo ? "Бесконечные патроны: ~g~Включены" : "Бесконечные патроны: ~r~Выключены");
                    }));

                    list.Add(new MenuItem(_isFireAmmo ? "Зажигательные пули [~g~ВКЛ~s~]" : "Зажигательные пули [~r~ВЫКЛ~s~]", () =>
                    {
                        _isFireAmmo = !_isFireAmmo;
                        Notifier.Show(_isFireAmmo ? "Зажигательные пули: ~g~Включены" : "Зажигательные пули: ~r~Выключены");
                    }));

                    list.Add(new MenuItem(_isExplosiveAmmo ? "Взрывные пули [~g~ВКЛ~s~]" : "Взрывные пули [~r~ВЫКЛ~s~]", () =>
                    {
                        _isExplosiveAmmo = !_isExplosiveAmmo;
                        Notifier.Show(_isExplosiveAmmo ? "Взрывные пули: ~g~Включены" : "Взрывные пули: ~r~Выключены");
                    }));

                    list.Add(new MenuItem(_isExplosiveMelee ? "Взрывной удар кулаком [~g~ВКЛ~s~]" : "Взрывной удар кулаком [~r~ВЫКЛ~s~]", () =>
                    {
                        _isExplosiveMelee = !_isExplosiveMelee;
                        Notifier.Show(_isExplosiveMelee ? "Взрывной удар: ~g~Включен" : "Взрывной удар: ~r~Выключен");
                    }));

                    list.Add(new MenuItem("Телекинез / Гравипушка (ПКМ + E)", () =>
                    {
                        Notifier.Show("~b~Гравипушка:~s~ Прицеливание + ~y~E~s~ (Захват), ~y~ЛКМ~s~ (Бросок)");
                    }));
                    break;

                case MenuCategory.Appearance:
                    list.Add(new MenuItem("[+] Сохранить текущий наряд", () =>
                    {
                        var p = Game.Player.Character;
                        if (p != null && p.Exists())
                        {
                            var saved = _outfitStore.SaveCurrentOutfit(p);
                            if (saved != null)
                            {
                                Notifier.Show($"~g~Наряд успешно сохранен:~s~\n~y~{saved.Name}~s~");
                            }
                        }
                    }));

                    int outfitCount = _outfitStore.Outfits.Count;
                    string countStr = outfitCount > 0 ? $"~g~[{outfitCount}]~s~" : "~m~[Пусто]~s~";
                    list.Add(new MenuItem($"Сохраненные наряды {countStr} >>", () => SwitchCategory(MenuCategory.SavedOutfits)));

                    list.Add(new MenuItem("Открыть Гардероб и Одежду", () =>
                    {
                        _isMenuVisible = false;
                        _clothing.ToggleMenu();
                    }));

                    list.Add(new MenuItem("Случайный наряд гардероба", () =>
                    {
                        var p = Game.Player.Character;
                        if (p != null && p.Exists())
                        {
                            Function.Call(Hash.SET_PED_RANDOM_COMPONENT_VARIATION, p.Handle, 0);
                            Notifier.Show("~g~Применен случайный наряд");
                        }
                    }));

                    list.Add(new MenuItem("Превратиться в MP Freemode (Online)", () =>
                    {
                        _animalMorph.Cycle();
                    }));

                    list.Add(new MenuItem("Морф в случайное животное", () =>
                    {
                        _animalMorph.Cycle();
                    }));

                    list.Add(new MenuItem("Вернуть оригинальную модель", () =>
                    {
                        _animalMorph.RestoreOriginalModel();
                        Notifier.Show("~g~Оригинальный облик персонажа восстановлен");
                    }));
                    break;

                case MenuCategory.SavedOutfits:
                    var allOutfits = _outfitStore.Outfits;
                    list.Add(new MenuItem("[+] Сохранить текущий наряд как новый", () =>
                    {
                        var p = Game.Player.Character;
                        if (p != null && p.Exists())
                        {
                            var saved = _outfitStore.SaveCurrentOutfit(p);
                            if (saved != null)
                            {
                                Notifier.Show($"~g~Наряд сохранен:~s~\n~y~{saved.Name}~s~");
                                _nav.CurrentIndex = 0;
                            }
                        }
                    }));

                    if (allOutfits.Count == 0)
                    {
                        list.Add(new MenuItem("~m~Нет сохраненных нарядов~s~", null));
                    }
                    else
                    {
                        foreach (var outfit in allOutfits.ToList())
                        {
                            var o = outfit;
                            list.Add(new MenuItem($"* {o.Name} [Enter: Надеть | 4/6: Удалить]",
                                () =>
                                {
                                    var p = Game.Player.Character;
                                    if (p != null && p.Exists())
                                    {
                                        if (_outfitStore.ApplyOutfit(p, o))
                                        {
                                            Notifier.Show($"~g~Надет комплект:~s~\n~y~{o.Name}~s~");
                                        }
                                    }
                                },
                                leftAction: () =>
                                {
                                    _outfitStore.DeleteOutfit(o.Id);
                                    Notifier.Show($"~r~Наряд удален:~s~ {o.Name}");
                                    _nav.CurrentIndex = Math.Max(0, _nav.CurrentIndex - 1);
                                },
                                rightAction: () =>
                                {
                                    _outfitStore.DeleteOutfit(o.Id);
                                    Notifier.Show($"~r~Наряд удален:~s~ {o.Name}");
                                    _nav.CurrentIndex = Math.Max(0, _nav.CurrentIndex - 1);
                                }
                            ));
                        }
                    }
                    break;

                case MenuCategory.Companions:
                    list.Add(new MenuItem("Нанять вооруженного телохранителя", () =>
                    {
                        _companions.Spawn();
                    }));

                    list.Add(new MenuItem("Режим личного шофера (Круиз)", () =>
                    {
                        _companions.ToggleChauffeurCruise();
                    }));

                    list.Add(new MenuItem("Отпустить всех телохранителей", () =>
                    {
                        _companions.ReleaseAll();
                    }));
                    break;

                case MenuCategory.PoliceOfficer:
                    list.Add(new MenuItem(_policeOfficer.IsFriendlyCopsEnabled ? "Дружба с полицией [~g~ВКЛ~s~]" : "Дружба с полицией [~r~ВЫКЛ~s~]", () =>
                    {
                        _policeOfficer.IsFriendlyCopsEnabled = !_policeOfficer.IsFriendlyCopsEnabled;
                        Notifier.Show(_policeOfficer.IsFriendlyCopsEnabled ? "Полиция LSPD: ~g~Ваши союзники" : "Полиция LSPD: ~r~Стандартный режим");
                    }));

                    list.Add(new MenuItem("Вызов спецназа NOOSE (4 бойца SWAT)", () =>
                    {
                        var p = Game.Player.Character;
                        if (p != null && p.Exists()) _policeOfficer.SpawnSwatVan(p);
                    }));

                    list.Add(new MenuItem("Вызов экипажа LSPD Interceptor", () =>
                    {
                        var p = Game.Player.Character;
                        if (p != null && p.Exists()) _policeOfficer.SpawnPoliceInterceptor(p);
                    }));

                    list.Add(new MenuItem("Вызов полицейского вертолета Maverick", () =>
                    {
                        var p = Game.Player.Character;
                        if (p != null && p.Exists()) _policeOfficer.SpawnPoliceHelicopter(p);
                    }));

                    list.Add(new MenuItem("Приказ офицерам: Штурмовать цель", () =>
                    {
                        var p = Game.Player.Character;
                        if (p != null && p.Exists()) _policeOfficer.OrderAttackAimedTarget(p);
                    }));

                    list.Add(new MenuItem("Отпустить всех задержанных гражданских", () =>
                    {
                        _policeOfficer.ReleaseAllDetained();
                    }));

                    list.Add(new MenuItem("Надеть форму офицера LSPD", () =>
                    {
                        var p = Game.Player.Character;
                        if (p != null && p.Exists()) _policeOfficer.WearPoliceUniform(p);
                    }));
                    break;
            }

            return list;
        }

        private void SwitchCategory(MenuCategory cat)
        {
            _nav.NavigateTo(cat);
        }

        private static string GetCategoryTitle(MenuCategory cat)
        {
            switch (cat)
            {
                case MenuCategory.PlayerStatus: return "== Персонаж и Состояние ==";
                case MenuCategory.KeyFob: return "== Брелок Автомобиля ==";
                case MenuCategory.Weapons: return "== Оружие и Способности ==";
                case MenuCategory.Appearance: return "== Облик и Гардероб ==";
                case MenuCategory.SavedOutfits: return "== Сохраненные Наряды ==";
                case MenuCategory.Companions: return "== Телохранители и Охрана ==";
                case MenuCategory.PoliceOfficer: return "== Полиция и Спецназ LSPD ==";
                default: return "== Главное Меню Персонажа ==";
            }
        }

        private static Vehicle GetNearestVehicle(float maxRadius)
        {
            var p = Game.Player.Character;
            if (p == null || !p.Exists()) return null;

            return GTA.World.GetClosestVehicle(p.Position, maxRadius);
        }

        private static void PlayFobClick(Vehicle v)
        {
            if (v == null || !v.Exists()) return;
            Function.Call(Hash.PLAY_SOUND_FROM_ENTITY, -1, "Remote_Click", "PI_MENU_SOUNDS", v.Handle, 0, 0, 0);
        }
    }
}
