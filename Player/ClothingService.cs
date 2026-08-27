using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using gta.Core;

namespace gta.Player
{
    internal sealed class ClothingService
    {
        private static readonly Random Random = new Random();

        private static readonly int MaleFreemodeHash = new Model("mp_m_freemode_01").Hash;
        private static readonly int FemaleFreemodeHash = new Model("mp_f_freemode_01").Hash;

        private static readonly ClothingSlot[] FreemodeClothingSlots =
        {
            new ClothingSlot("Руки / торс", ClothingSlotKind.Component, 3),
            new ClothingSlot("Штаны", ClothingSlotKind.Component, 4),
            new ClothingSlot("Обувь", ClothingSlotKind.Component, 6),
            new ClothingSlot("Майка", ClothingSlotKind.Component, 8),
            new ClothingSlot("Верх", ClothingSlotKind.Component, 11)
        };

        private static readonly ClothingSlot[] FreemodeGearSlots =
        {
            new ClothingSlot("Маска", ClothingSlotKind.Component, 1),
            new ClothingSlot("Сумка / парашют", ClothingSlotKind.Component, 5),
            new ClothingSlot("Шея / снаряжение", ClothingSlotKind.Component, 7),
            new ClothingSlot("Бронежилет", ClothingSlotKind.Component, 9),
            new ClothingSlot("Нашивки", ClothingSlotKind.Component, 10)
        };

        private static readonly ClothingSlot[] StoryClothingSlots =
        {
            new ClothingSlot("Руки / торс", ClothingSlotKind.Component, 3),
            new ClothingSlot("Штаны", ClothingSlotKind.Component, 4),
            new ClothingSlot("Обувь", ClothingSlotKind.Component, 6),
            new ClothingSlot("Верх", ClothingSlotKind.Component, 11)
        };

        private static readonly ClothingSlot[] StoryGearSlots =
        {
            new ClothingSlot("Снаряжение спины", ClothingSlotKind.Component, 8),
            new ClothingSlot("Маска / снаряжение", ClothingSlotKind.Component, 9),
            new ClothingSlot("Нашивки / маски", ClothingSlotKind.Component, 10)
        };

        private static readonly ClothingSlot[] PropSlots =
        {
            new ClothingSlot("Головной убор", ClothingSlotKind.Prop, 0),
            new ClothingSlot("Очки", ClothingSlotKind.Prop, 1),
            new ClothingSlot("Серьги / уши", ClothingSlotKind.Prop, 2),
            new ClothingSlot("Часы", ClothingSlotKind.Prop, 6),
            new ClothingSlot("Браслет", ClothingSlotKind.Prop, 7)
        };

        private bool _isMenuVisible;
        private readonly MenuNavigator<ClothingCategory> _nav = new MenuNavigator<ClothingCategory>(ClothingCategory.None);

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
            Notifier.Show(_isMenuVisible ? "Меню одежды открыто" : "Меню одежды закрыто");
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

            if (_nav.IsAtRoot)
            {
                DrawCategories(character);
                return;
            }

            var options = BuildOptions(character, _nav.CurrentCategory);
            if (options.Count == 0)
            {
                ReturnToCategories();
                return;
            }

            _nav.ClampIndex(options.Count);

            var text = "Одежда: " + GetCategoryName(_nav.CurrentCategory) + "\n";
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var marker = i == _nav.CurrentIndex ? "> " : "  ";
                text += marker + option.Name + GetOptionValueText(character, option) + "\n";
            }

            text += "\n" + GetControlsText(_nav.CurrentCategory);
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

            if (_nav.IsAtRoot)
            {
                HandleCategories(character, e);
                return;
            }

            var options = BuildOptions(character, _nav.CurrentCategory);
            if (options.Count == 0)
            {
                ReturnToCategories();
                return;
            }

            _nav.ClampIndex(options.Count);

            switch (e.KeyCode)
            {
                case Keys.Decimal:
                case Keys.Separator:
                case Keys.Escape:
                    _isMenuVisible = false;
                    Notifier.Show("Меню одежды закрыто");
                    break;
                case Keys.NumPad0:
                case Keys.Back:
                    ReturnToCategories();
                    break;
                case Keys.NumPad8:
                    _nav.MovePrevious(options.Count);
                    break;
                case Keys.NumPad2:
                    _nav.MoveNext(options.Count);
                    break;
                case Keys.NumPad4:
                    ChangeDrawable(character, options[_nav.CurrentIndex], -1);
                    break;
                case Keys.NumPad6:
                    ChangeDrawable(character, options[_nav.CurrentIndex], 1);
                    break;
                case Keys.NumPad7:
                    ChangeTexture(character, options[_nav.CurrentIndex], -1);
                    break;
                case Keys.NumPad9:
                    ChangeTexture(character, options[_nav.CurrentIndex], 1);
                    break;
                case Keys.NumPad5:
                    RandomizeOption(character, options[_nav.CurrentIndex]);
                    break;
            }
        }

        private void DrawCategories(Ped character)
        {
            var categories = BuildCategories(character);
            if (categories.Count == 0)
            {
                return;
            }

            _nav.ClampIndex(categories.Count);

            var text = "Одежда\n";
            for (var i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                var marker = i == _nav.CurrentIndex ? "> " : "  ";
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

            _nav.ClampIndex(categories.Count);

            switch (e.KeyCode)
            {
                case Keys.Decimal:
                case Keys.Separator:
                case Keys.NumPad0:
                case Keys.Back:
                case Keys.Escape:
                    _isMenuVisible = false;
                    Notifier.Show("Меню одежды закрыто");
                    break;
                case Keys.NumPad8:
                    _nav.MovePrevious(categories.Count);
                    break;
                case Keys.NumPad2:
                    _nav.MoveNext(categories.Count);
                    break;
                case Keys.NumPad5:
                    if (_nav.CurrentIndex >= 0 && _nav.CurrentIndex < categories.Count)
                    {
                        _nav.NavigateTo(categories[_nav.CurrentIndex].Kind);
                    }
                    break;
            }
        }

        private List<ClothingCategoryDefinition> BuildCategories(Ped character)
        {
            return new List<ClothingCategoryDefinition>
            {
                new ClothingCategoryDefinition(ClothingCategory.Quick, "Быстро", BuildOptions(character, ClothingCategory.Quick).Count.ToString()),
                new ClothingCategoryDefinition(ClothingCategory.Components, "Одежда", BuildOptions(character, ClothingCategory.Components).Count.ToString()),
                new ClothingCategoryDefinition(ClothingCategory.Props, "Аксессуары", BuildOptions(character, ClothingCategory.Props).Count.ToString()),
                new ClothingCategoryDefinition(ClothingCategory.Gear, "Снаряжение", BuildOptions(character, ClothingCategory.Gear).Count.ToString())
            };
        }

        private static List<ClothingOption> BuildOptions(Ped character, ClothingCategory category)
        {
            var options = new List<ClothingOption>();

            switch (category)
            {
                case ClothingCategory.Quick:
                    options.Add(ClothingOption.CreateCommand("Случайная одежда", ClothingCommand.RandomComponents));
                    options.Add(ClothingOption.CreateCommand("Случайный образ", ClothingCommand.RandomAll));
                    options.Add(ClothingOption.CreateCommand("Снять аксессуары", ClothingCommand.ClearProps));
                    options.Add(ClothingOption.CreateCommand("Снять снаряжение", ClothingCommand.ClearGear));
                    break;
                case ClothingCategory.Components:
                    AddSlotOptions(character, options, GetClothingSlots(character));
                    break;
                case ClothingCategory.Props:
                    AddSlotOptions(character, options, PropSlots);
                    break;
                case ClothingCategory.Gear:
                    AddSlotOptions(character, options, GetGearSlots(character));
                    break;
            }

            return options;
        }

        private static void AddSlotOptions(Ped character, ICollection<ClothingOption> options, IEnumerable<ClothingSlot> slots)
        {
            foreach (var slot in slots)
            {
                if (GetDrawableCount(character, slot) <= 0)
                {
                    continue;
                }

                options.Add(ClothingOption.Slot(slot.Name, slot.Kind, slot.Id));
            }
        }

        private static string GetOptionValueText(Ped character, ClothingOption option)
        {
            if (option.Kind == ClothingOptionKind.Command)
            {
                return "";
            }

            var slot = new ClothingSlot(option.Name, option.SlotKind, option.SlotId);
            var drawableCount = GetDrawableCount(character, slot);
            var drawable = GetDrawable(character, slot);
            var texture = GetTexture(character, slot);

            if (slot.Kind == ClothingSlotKind.Prop && drawable < 0)
            {
                return " [снято]";
            }

            var textureCount = GetTextureCount(character, slot, drawable);
            return " [" + (drawable + 1) + "/" + drawableCount + ", текстура " + (texture + 1) + "/" + Math.Max(1, textureCount) + "]";
        }

        private static void ChangeDrawable(Ped character, ClothingOption option, int direction)
        {
            if (option.Kind == ClothingOptionKind.Command)
            {
                return;
            }

            var slot = new ClothingSlot(option.Name, option.SlotKind, option.SlotId);
            var drawableCount = GetDrawableCount(character, slot);
            if (drawableCount <= 0)
            {
                return;
            }

            var min = slot.Kind == ClothingSlotKind.Prop ? -1 : 0;
            var nextDrawable = Wrap(GetDrawable(character, slot) + direction, min, drawableCount - 1);
            SetSlot(character, slot, nextDrawable, 0);
        }

        private static void ChangeTexture(Ped character, ClothingOption option, int direction)
        {
            if (option.Kind == ClothingOptionKind.Command)
            {
                return;
            }

            var slot = new ClothingSlot(option.Name, option.SlotKind, option.SlotId);
            var drawable = GetDrawable(character, slot);
            if (slot.Kind == ClothingSlotKind.Prop && drawable < 0)
            {
                return;
            }

            var textureCount = GetTextureCount(character, slot, drawable);
            if (textureCount <= 1)
            {
                SetSlot(character, slot, drawable, 0);
                return;
            }

            SetSlot(character, slot, drawable, Wrap(GetTexture(character, slot) + direction, 0, textureCount - 1));
        }

        private static void RandomizeOption(Ped character, ClothingOption option)
        {
            if (option.Kind == ClothingOptionKind.Command)
            {
                RunCommand(character, option.Command);
                return;
            }

            RandomizeSlot(character, new ClothingSlot(option.Name, option.SlotKind, option.SlotId));
        }

        private static void RunCommand(Ped character, ClothingCommand command)
        {
            switch (command)
            {
                case ClothingCommand.RandomComponents:
                    RandomizeSlots(character, GetClothingSlots(character));
                    Notifier.Show("Случайная одежда");
                    break;
                case ClothingCommand.RandomAll:
                    RandomizeSlots(character, GetClothingSlots(character));
                    RandomizeSlots(character, PropSlots);
                    Notifier.Show("Случайный образ");
                    break;
                case ClothingCommand.ClearProps:
                    ClearProps(character);
                    Notifier.Show("Аксессуары сняты");
                    break;
                case ClothingCommand.ClearGear:
                    ClearGear(character);
                    Notifier.Show("Снаряжение снято");
                    break;
            }
        }

        private static void RandomizeSlots(Ped character, IEnumerable<ClothingSlot> slots)
        {
            foreach (var slot in slots)
            {
                RandomizeSlot(character, slot);
            }
        }

        private static void RandomizeSlot(Ped character, ClothingSlot slot)
        {
            var drawableCount = GetDrawableCount(character, slot);
            if (drawableCount <= 0)
            {
                return;
            }

            var min = slot.Kind == ClothingSlotKind.Prop ? -1 : 0;
            var drawable = Random.Next(min, drawableCount);
            var textureCount = drawable < 0 ? 0 : GetTextureCount(character, slot, drawable);
            var texture = textureCount <= 0 ? 0 : Random.Next(0, textureCount);
            SetSlot(character, slot, drawable, texture);
        }

        private static void ClearProps(Ped character)
        {
            foreach (var slot in PropSlots)
            {
                Function.Call(Hash.CLEAR_PED_PROP, character.Handle, slot.Id);
            }
        }

        private static void ClearGear(Ped character)
        {
            foreach (var slot in GetGearSlots(character))
            {
                SetSlot(character, slot, 0, 0);
            }
        }

        private static IEnumerable<ClothingSlot> GetClothingSlots(Ped character)
        {
            return IsFreemodePed(character)
                ? FreemodeClothingSlots
                : StoryClothingSlots;
        }

        private static IEnumerable<ClothingSlot> GetGearSlots(Ped character)
        {
            return IsFreemodePed(character)
                ? FreemodeGearSlots
                : StoryGearSlots;
        }

        private static bool IsFreemodePed(Ped character)
        {
            var modelHash = character.Model.Hash;
            return modelHash == MaleFreemodeHash || modelHash == FemaleFreemodeHash;
        }

        private static int GetDrawableCount(Ped character, ClothingSlot slot)
        {
            return slot.Kind == ClothingSlotKind.Component
                ? Function.Call<int>(Hash.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS, character.Handle, slot.Id)
                : Function.Call<int>(Hash.GET_NUMBER_OF_PED_PROP_DRAWABLE_VARIATIONS, character.Handle, slot.Id);
        }

        private static int GetTextureCount(Ped character, ClothingSlot slot, int drawable)
        {
            if (drawable < 0)
            {
                return 0;
            }

            return slot.Kind == ClothingSlotKind.Component
                ? Function.Call<int>(Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS, character.Handle, slot.Id, drawable)
                : Function.Call<int>(Hash.GET_NUMBER_OF_PED_PROP_TEXTURE_VARIATIONS, character.Handle, slot.Id, drawable);
        }

        private static int GetDrawable(Ped character, ClothingSlot slot)
        {
            return slot.Kind == ClothingSlotKind.Component
                ? Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, character.Handle, slot.Id)
                : Function.Call<int>(Hash.GET_PED_PROP_INDEX, character.Handle, slot.Id);
        }

        private static int GetTexture(Ped character, ClothingSlot slot)
        {
            return slot.Kind == ClothingSlotKind.Component
                ? Function.Call<int>(Hash.GET_PED_TEXTURE_VARIATION, character.Handle, slot.Id)
                : Function.Call<int>(Hash.GET_PED_PROP_TEXTURE_INDEX, character.Handle, slot.Id);
        }

        private static void SetSlot(Ped character, ClothingSlot slot, int drawable, int texture)
        {
            if (slot.Kind == ClothingSlotKind.Prop)
            {
                if (drawable < 0)
                {
                    Function.Call(Hash.CLEAR_PED_PROP, character.Handle, slot.Id);
                    return;
                }

                Function.Call(Hash.SET_PED_PROP_INDEX, character.Handle, slot.Id, drawable, Math.Max(0, texture), true);
                return;
            }

            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, character.Handle, slot.Id, Math.Max(0, drawable), Math.Max(0, texture), 0);
        }

        private void ReturnToCategories()
        {
            _nav.GoBack();
        }

        private static Ped GetPlayerCharacter()
        {
            var character = Game.Player.Character;
            return character != null && character.Exists()
                ? character
                : null;
        }

        private static string GetCategoryName(ClothingCategory category)
        {
            switch (category)
            {
                case ClothingCategory.Quick: return "Быстро";
                case ClothingCategory.Components: return "Одежда";
                case ClothingCategory.Props: return "Аксессуары";
                case ClothingCategory.Gear: return "Снаряжение";
                default: return "Одежда";
            }
        }

        private static string GetControlsText(ClothingCategory category)
        {
            return category == ClothingCategory.Quick
                ? "8/2 - выбор  5 - выполнить  0 - назад"
                : "8/2 - выбор  4/6 - модель  7/9 - текстура  5 - случайно  0 - назад";
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

        private enum ClothingCategory
        {
            None,
            Quick,
            Components,
            Props,
            Gear
        }

        private enum ClothingOptionKind
        {
            Command,
            Slot
        }

        private enum ClothingSlotKind
        {
            Component,
            Prop
        }

        private enum ClothingCommand
        {
            RandomComponents,
            RandomAll,
            ClearProps,
            ClearGear
        }

        private struct ClothingCategoryDefinition
        {
            public readonly ClothingCategory Kind;
            public readonly string Name;
            public readonly string StatusText;

            public ClothingCategoryDefinition(ClothingCategory kind, string name, string statusText)
            {
                Kind = kind;
                Name = name;
                StatusText = statusText;
            }
        }

        private struct ClothingSlot
        {
            public readonly string Name;
            public readonly ClothingSlotKind Kind;
            public readonly int Id;

            public ClothingSlot(string name, ClothingSlotKind kind, int id)
            {
                Name = name;
                Kind = kind;
                Id = id;
            }
        }

        private struct ClothingOption
        {
            public readonly string Name;
            public readonly ClothingOptionKind Kind;
            public readonly ClothingCommand Command;
            public readonly ClothingSlotKind SlotKind;
            public readonly int SlotId;

            private ClothingOption(string name, ClothingOptionKind kind, ClothingCommand command, ClothingSlotKind slotKind, int slotId)
            {
                Name = name;
                Kind = kind;
                Command = command;
                SlotKind = slotKind;
                SlotId = slotId;
            }

            public static ClothingOption CreateCommand(string name, ClothingCommand command)
            {
                return new ClothingOption(name, ClothingOptionKind.Command, command, ClothingSlotKind.Component, -1);
            }

            public static ClothingOption Slot(string name, ClothingSlotKind slotKind, int slotId)
            {
                return new ClothingOption(name, ClothingOptionKind.Slot, ClothingCommand.RandomAll, slotKind, slotId);
            }
        }
    }
}
