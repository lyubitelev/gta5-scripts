using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using gta.Core;

namespace gta.Vehicles
{
    internal sealed class VehicleMenuController
    {
        private const string ClearPoolMenuText = "Очистить пул";

        private readonly VehicleGroupCatalog[] _mainGroups;
        private readonly VehicleMenuEntry[] _mainCategories;
        private readonly int[] _mainGroupPages;
        private readonly int[] _mainGroupIndexes;
        private readonly VehicleFavoritesStore _favorites;
        private readonly GeneratedVehicleCatalog _onlineVehicles;
        private readonly VehicleSpawner _spawner;
        private readonly VehicleMenuRenderer _renderer;

        private bool _isMainVisible;
        private bool _isFavoritesVisible;
        private bool _isOnlineVisible;
        private int _mainPage;
        private int _mainIndex;
        private int _mainVehiclePage;
        private int _mainVehicleIndex;
        private int _mainGroupIndex = -1;
        private int _favoritesPage;
        private int _favoritesIndex;
        private int _favoritesGroupIndex = -1;
        private int _favoritesVehiclePage;
        private int _favoritesVehicleIndex;
        private int _onlinePage;
        private int _onlineIndex;
        private int _onlineGroupIndex = -1;
        private int _onlineVehiclePage;
        private int _onlineVehicleIndex;

        public VehicleMenuController(
            VehicleHash[] vehicleHashes,
            VehicleFavoritesStore favorites,
            GeneratedVehicleCatalog onlineVehicles,
            VehicleSpawner spawner,
            VehicleMenuRenderer renderer)
        {
            _mainGroups = BuildMainVehicleGroups(vehicleHashes);
            _mainCategories = _mainGroups
                .Select(group => VehicleMenuEntry.Category(group.Name + " [" + group.Vehicles.Length + "]"))
                .Concat(new[] { VehicleMenuEntry.Category(ClearPoolMenuText) })
                .ToArray();
            _mainGroupPages = new int[_mainGroups.Length];
            _mainGroupIndexes = new int[_mainGroups.Length];
            _favorites = favorites;
            _onlineVehicles = onlineVehicles;
            _spawner = spawner;
            _renderer = renderer;
        }

        public bool IsVisible
        {
            get { return _isMainVisible || _isFavoritesVisible || _isOnlineVisible; }
        }

        public void ToggleMain()
        {
            _isMainVisible = !_isMainVisible;
            _isFavoritesVisible = false;
            _isOnlineVisible = false;
        }

        public void ToggleFavorites()
        {
            _isFavoritesVisible = !_isFavoritesVisible;
            _isMainVisible = false;
            _isOnlineVisible = false;
        }

        public void ToggleOnline()
        {
            _isOnlineVisible = !_isOnlineVisible;
            _isMainVisible = false;
            _isFavoritesVisible = false;
        }

        public void ToggleReplaceExistingVehicle()
        {
            _spawner.ToggleReplaceExistingVehicle();
        }

        public void Draw()
        {
            if (_isMainVisible)
            {
                DrawMainMenu();
                return;
            }

            if (_isFavoritesVisible)
            {
                DrawFavoritesMenu();
                return;
            }

            if (_isOnlineVisible)
            {
                DrawOnlineMenu();
            }
        }

        public void Handle(KeyEventArgs e)
        {
            var kind = GetVisibleKind();
            if (kind == VehicleMenuKind.Main)
            {
                HandleMainMenu(e);
                return;
            }

            if (kind == VehicleMenuKind.Favorites)
            {
                HandleFavoritesMenu(e);
                return;
            }

            HandleOnlineMenu(e);
        }

        private void DrawFavoritesMenu()
        {
            var groups = BuildVehicleGroups(CreateFavoriteEntries(_favorites.Vehicles));
            var categories = groups
                .Select(group => VehicleMenuEntry.Category(group.Name + " [" + group.Vehicles.Length + "]"))
                .ToArray();

            if (IsFavoritesGroupOpen(groups.Length))
            {
                var group = groups[_favoritesGroupIndex];
                var state = new MenuState(_favoritesVehiclePage, _favoritesVehicleIndex);
                state.Clamp(group.Vehicles.Length);
                MoveToSelectable(group.Vehicles, ref state, 1);
                _favoritesVehiclePage = state.Page;
                _favoritesVehicleIndex = state.Index;

                _renderer.Draw(
                    "Избранное: " + group.Name,
                    group.Vehicles,
                    state.Page,
                    state.Index,
                    ModSettings.VehicleMenuItemsPerPage,
                    "8/2 - выбор  7/9 - страницы  5 - создать  4 - замена  1 - убрать из избранного  0 - назад");
                return;
            }

            ClampCategorySelection(ref _favoritesIndex, categories.Length);
            _renderer.Draw(
                "Избранное",
                categories,
                0,
                _favoritesIndex,
                Math.Max(1, categories.Length),
                "8/2 - выбор  5 - открыть  0 - назад");
        }

        private void DrawOnlineMenu()
        {
            var groups = BuildVehicleGroups(CreateVehicleEntries(_onlineVehicles.Vehicles));
            var categories = groups
                .Select(group => VehicleMenuEntry.Category(group.Name + " [" + group.Vehicles.Length + "]"))
                .ToArray();

            if (IsOnlineGroupOpen(groups.Length))
            {
                var group = groups[_onlineGroupIndex];
                var state = new MenuState(_onlineVehiclePage, _onlineVehicleIndex);
                state.Clamp(group.Vehicles.Length);
                MoveToSelectable(group.Vehicles, ref state, 1);
                _onlineVehiclePage = state.Page;
                _onlineVehicleIndex = state.Index;

                _renderer.Draw(
                    "Онлайн транспорт: " + group.Name,
                    group.Vehicles,
                    state.Page,
                    state.Index,
                    ModSettings.VehicleMenuItemsPerPage,
                    "8/2 - выбор  7/9 - страницы  5 - создать  4 - замена  1 - добавить в избранное  0 - назад");
                return;
            }

            ClampCategorySelection(ref _onlineIndex, categories.Length);
            _renderer.Draw(
                "Онлайн транспорт",
                categories,
                0,
                _onlineIndex,
                Math.Max(1, categories.Length),
                "8/2 - выбор  5 - открыть  0 - назад");
        }

        private void HandleFavoritesMenu(KeyEventArgs e)
        {
            var groups = BuildVehicleGroups(CreateFavoriteEntries(_favorites.Vehicles));
            var categories = groups
                .Select(group => VehicleMenuEntry.Category(group.Name + " [" + group.Vehicles.Length + "]"))
                .ToArray();

            if (IsFavoritesGroupOpen(groups.Length))
            {
                var group = groups[_favoritesGroupIndex];
                if (group.Vehicles.Length == 0)
                {
                    _favoritesGroupIndex = -1;
                    return;
                }

                var state = new MenuState(_favoritesVehiclePage, _favoritesVehicleIndex);
                state.Clamp(group.Vehicles.Length);
                MoveToSelectable(group.Vehicles, ref state, 1);

                switch (e.KeyCode)
                {
                    case Keys.NumPad8:
                        MoveSelection(group.Vehicles, ref state, -1);
                        break;
                    case Keys.NumPad2:
                        MoveSelection(group.Vehicles, ref state, 1);
                        break;
                    case Keys.NumPad5:
                        SpawnVehicle(group.Vehicles[state.Index]);
                        break;
                    case Keys.NumPad4:
                        ToggleReplaceExistingVehicle();
                        break;
                    case Keys.NumPad7:
                        if (state.Page > 0)
                        {
                            state.Page--;
                            state.Index = state.Page * ModSettings.VehicleMenuItemsPerPage;
                            MoveToSelectableOnCurrentPage(group.Vehicles, ref state, 1);
                        }
                        break;
                    case Keys.NumPad9:
                        if (state.Page < GetMaxPage(group.Vehicles.Length))
                        {
                            state.Page++;
                            state.Index = state.Page * ModSettings.VehicleMenuItemsPerPage;
                            MoveToSelectableOnCurrentPage(group.Vehicles, ref state, 1);
                        }
                        break;
                    case Keys.NumPad1:
                        ToggleFavorite(VehicleMenuKind.Favorites, group.Vehicles[state.Index]);
                        break;
                    case Keys.NumPad0:
                    case Keys.Back:
                        _favoritesGroupIndex = -1;
                        break;
                    case Keys.Escape:
                        _isFavoritesVisible = false;
                        break;
                }

                _favoritesVehiclePage = state.Page;
                _favoritesVehicleIndex = state.Index;
                return;
            }

            ClampCategorySelection(ref _favoritesIndex, categories.Length);

            switch (e.KeyCode)
            {
                case Keys.NumPad8:
                    _favoritesIndex = _favoritesIndex == 0 ? categories.Length - 1 : _favoritesIndex - 1;
                    break;
                case Keys.NumPad2:
                    _favoritesIndex = _favoritesIndex + 1 >= categories.Length ? 0 : _favoritesIndex + 1;
                    break;
                case Keys.NumPad5:
                    if (_favoritesIndex < groups.Length)
                    {
                        _favoritesGroupIndex = _favoritesIndex;
                        _favoritesVehiclePage = 0;
                        _favoritesVehicleIndex = 0;
                    }
                    break;
                case Keys.NumPad4:
                    ToggleReplaceExistingVehicle();
                    break;
                case Keys.NumPad0:
                case Keys.Back:
                case Keys.Escape:
                    _isFavoritesVisible = false;
                    break;
            }
        }

        private void HandleOnlineMenu(KeyEventArgs e)
        {
            var groups = BuildVehicleGroups(CreateVehicleEntries(_onlineVehicles.Vehicles));
            var categories = groups
                .Select(group => VehicleMenuEntry.Category(group.Name + " [" + group.Vehicles.Length + "]"))
                .ToArray();

            if (IsOnlineGroupOpen(groups.Length))
            {
                var group = groups[_onlineGroupIndex];
                if (group.Vehicles.Length == 0)
                {
                    _onlineGroupIndex = -1;
                    return;
                }

                var state = new MenuState(_onlineVehiclePage, _onlineVehicleIndex);
                state.Clamp(group.Vehicles.Length);
                MoveToSelectable(group.Vehicles, ref state, 1);

                switch (e.KeyCode)
                {
                    case Keys.NumPad8:
                        MoveSelection(group.Vehicles, ref state, -1);
                        break;
                    case Keys.NumPad2:
                        MoveSelection(group.Vehicles, ref state, 1);
                        break;
                    case Keys.NumPad5:
                        SpawnVehicle(group.Vehicles[state.Index]);
                        break;
                    case Keys.NumPad4:
                        ToggleReplaceExistingVehicle();
                        break;
                    case Keys.NumPad7:
                        if (state.Page > 0)
                        {
                            state.Page--;
                            state.Index = state.Page * ModSettings.VehicleMenuItemsPerPage;
                            MoveToSelectableOnCurrentPage(group.Vehicles, ref state, 1);
                        }
                        break;
                    case Keys.NumPad9:
                        if (state.Page < GetMaxPage(group.Vehicles.Length))
                        {
                            state.Page++;
                            state.Index = state.Page * ModSettings.VehicleMenuItemsPerPage;
                            MoveToSelectableOnCurrentPage(group.Vehicles, ref state, 1);
                        }
                        break;
                    case Keys.NumPad1:
                        ToggleFavorite(VehicleMenuKind.Online, group.Vehicles[state.Index]);
                        break;
                    case Keys.NumPad0:
                    case Keys.Back:
                        _onlineGroupIndex = -1;
                        break;
                    case Keys.Escape:
                        _isOnlineVisible = false;
                        break;
                }

                _onlineVehiclePage = state.Page;
                _onlineVehicleIndex = state.Index;
                return;
            }

            ClampCategorySelection(ref _onlineIndex, categories.Length);

            switch (e.KeyCode)
            {
                case Keys.NumPad8:
                    _onlineIndex = _onlineIndex == 0 ? categories.Length - 1 : _onlineIndex - 1;
                    break;
                case Keys.NumPad2:
                    _onlineIndex = _onlineIndex + 1 >= categories.Length ? 0 : _onlineIndex + 1;
                    break;
                case Keys.NumPad5:
                    if (_onlineIndex < groups.Length)
                    {
                        _onlineGroupIndex = _onlineIndex;
                        _onlineVehiclePage = 0;
                        _onlineVehicleIndex = 0;
                    }
                    break;
                case Keys.NumPad4:
                    ToggleReplaceExistingVehicle();
                    break;
                case Keys.NumPad0:
                case Keys.Back:
                case Keys.Escape:
                    _isOnlineVisible = false;
                    break;
            }
        }

        private bool IsFavoritesGroupOpen(int groupCount)
        {
            return _favoritesGroupIndex >= 0 && _favoritesGroupIndex < groupCount;
        }

        private bool IsOnlineGroupOpen(int groupCount)
        {
            return _onlineGroupIndex >= 0 && _onlineGroupIndex < groupCount;
        }

        private static void ClampCategorySelection(ref int selection, int count)
        {
            if (count <= 0)
            {
                selection = 0;
                return;
            }

            selection = Math.Min(selection, count - 1);
            selection = Math.Max(selection, 0);
        }

        private void DrawMainMenu()
        {
            if (IsMainGroupOpen())
            {
                var group = _mainGroups[_mainGroupIndex];
                var state = new MenuState(_mainVehiclePage, _mainVehicleIndex);
                state.Clamp(group.Vehicles.Length);
                MoveToSelectable(group.Vehicles, ref state, 1);
                _mainVehiclePage = state.Page;
                _mainVehicleIndex = state.Index;

                _renderer.Draw(
                    "Транспорт: " + group.Name,
                    group.Vehicles,
                    state.Page,
                    state.Index,
                    ModSettings.VehicleMenuItemsPerPage,
                    "8/2 - выбор  7/9 - страницы  5 - создать  4 - замена  1 - избранное  0 - назад");
                return;
            }

            ClampMainCategoryState();

            _renderer.Draw(
                "Транспорт",
                _mainCategories,
                0,
                _mainIndex,
                Math.Max(1, _mainCategories.Length),
                "8/2 - выбор  5 - открыть/очистить  4 - замена  0 - закрыть");
        }

        private void HandleMainMenu(KeyEventArgs e)
        {
            if (IsMainGroupOpen())
            {
                HandleMainVehicleGroup(e);
                return;
            }

            HandleMainCategories(e);
        }

        private void HandleMainCategories(KeyEventArgs e)
        {
            if (_mainCategories.Length == 0)
            {
                return;
            }

            ClampMainCategoryState();

            switch (e.KeyCode)
            {
                case Keys.NumPad8:
                    _mainIndex = _mainIndex == 0 ? _mainCategories.Length - 1 : _mainIndex - 1;
                    break;
                case Keys.NumPad2:
                    _mainIndex = _mainIndex + 1 >= _mainCategories.Length ? 0 : _mainIndex + 1;
                    break;
                case Keys.NumPad5:
                    if (_mainIndex >= _mainGroups.Length)
                    {
                        ClearSpawnedVehiclePool();
                        break;
                    }

                    _mainGroupIndex = _mainIndex;
                    LoadMainGroupSelection(_mainGroupIndex);
                    break;
                case Keys.NumPad4:
                    ToggleReplaceExistingVehicle();
                    break;
                case Keys.NumPad0:
                case Keys.Back:
                case Keys.Escape:
                    _isMainVisible = false;
                    _mainGroupIndex = -1;
                    break;
            }
        }

        private void HandleMainVehicleGroup(KeyEventArgs e)
        {
            var activeGroupIndex = _mainGroupIndex;
            var group = _mainGroups[activeGroupIndex];
            if (group.Vehicles.Length == 0)
            {
                _mainGroupIndex = -1;
                return;
            }

            var state = new MenuState(_mainVehiclePage, _mainVehicleIndex);
            state.Clamp(group.Vehicles.Length);
            MoveToSelectable(group.Vehicles, ref state, 1);

            switch (e.KeyCode)
            {
                case Keys.NumPad8:
                    MoveSelection(group.Vehicles, ref state, -1);
                    break;
                case Keys.NumPad2:
                    MoveSelection(group.Vehicles, ref state, 1);
                    break;
                case Keys.NumPad5:
                    SpawnVehicle(group.Vehicles[state.Index]);
                    break;
                case Keys.NumPad4:
                    ToggleReplaceExistingVehicle();
                    break;
                case Keys.NumPad7:
                    if (state.Page > 0)
                    {
                        state.Page--;
                        state.Index = state.Page * ModSettings.VehicleMenuItemsPerPage;
                        MoveToSelectableOnCurrentPage(group.Vehicles, ref state, 1);
                    }
                    break;
                case Keys.NumPad9:
                    if (state.Page < GetMaxPage(group.Vehicles.Length))
                    {
                        state.Page++;
                        state.Index = state.Page * ModSettings.VehicleMenuItemsPerPage;
                        MoveToSelectableOnCurrentPage(group.Vehicles, ref state, 1);
                    }
                    break;
                case Keys.NumPad1:
                    ToggleFavorite(VehicleMenuKind.Main, group.Vehicles[state.Index]);
                    break;
                case Keys.NumPad0:
                case Keys.Back:
                    _mainGroupIndex = -1;
                    break;
                case Keys.Escape:
                    _isMainVisible = false;
                    _mainGroupIndex = -1;
                    break;
            }

            _mainVehiclePage = state.Page;
            _mainVehicleIndex = state.Index;
            SaveMainGroupSelection(activeGroupIndex, state);
        }

        private bool IsMainGroupOpen()
        {
            return _mainGroupIndex >= 0 && _mainGroupIndex < _mainGroups.Length;
        }

        private void ClampMainCategoryState()
        {
            _mainPage = 0;

            if (_mainCategories.Length == 0)
            {
                _mainIndex = 0;
                return;
            }

            _mainIndex = Math.Min(_mainIndex, _mainCategories.Length - 1);
            _mainIndex = Math.Max(_mainIndex, 0);
        }

        private void LoadMainGroupSelection(int groupIndex)
        {
            if (groupIndex < 0 || groupIndex >= _mainGroups.Length)
            {
                _mainVehiclePage = 0;
                _mainVehicleIndex = 0;
                return;
            }

            _mainVehiclePage = _mainGroupPages[groupIndex];
            _mainVehicleIndex = _mainGroupIndexes[groupIndex];
        }

        private void SaveMainGroupSelection(int groupIndex, MenuState state)
        {
            if (groupIndex < 0 || groupIndex >= _mainGroups.Length)
            {
                return;
            }

            _mainGroupPages[groupIndex] = state.Page;
            _mainGroupIndexes[groupIndex] = state.Index;
        }

        private void ClearSpawnedVehiclePool()
        {
            var removedCount = _spawner.ClearPool();
            Notifier.Show(removedCount > 0
                ? "Пул транспорта очищен: " + removedCount
                : "Пул транспорта уже пуст");
        }

        private void DrawMenu(VehicleMenuKind kind, string title, VehicleMenuEntry[] vehicles)
        {
            var state = GetState(kind);
            state.Clamp(vehicles.Length);
            MoveToSelectable(vehicles, ref state, 1);
            SetState(kind, state);

            _renderer.Draw(title, vehicles, state.Page, state.Index, ModSettings.VehicleMenuItemsPerPage);
        }

        private VehicleMenuKind GetVisibleKind()
        {
            if (_isMainVisible)
            {
                return VehicleMenuKind.Main;
            }

            return _isFavoritesVisible
                ? VehicleMenuKind.Favorites
                : VehicleMenuKind.Online;
        }

        private VehicleMenuEntry[] GetVehicles(VehicleMenuKind kind)
        {
            switch (kind)
            {
                case VehicleMenuKind.Favorites:
                    return CreateFavoriteEntries(_favorites.Vehicles);
                case VehicleMenuKind.Online:
                    return CreateVehicleEntries(_onlineVehicles.Vehicles);
                default:
                    return _mainCategories;
            }
        }

        private MenuState GetState(VehicleMenuKind kind)
        {
            switch (kind)
            {
                case VehicleMenuKind.Favorites:
                    return new MenuState(_favoritesPage, _favoritesIndex);
                case VehicleMenuKind.Online:
                    return new MenuState(_onlinePage, _onlineIndex);
                default:
                    return new MenuState(_mainPage, _mainIndex);
            }
        }

        private void SetState(VehicleMenuKind kind, MenuState state)
        {
            switch (kind)
            {
                case VehicleMenuKind.Favorites:
                    _favoritesPage = state.Page;
                    _favoritesIndex = state.Index;
                    break;
                case VehicleMenuKind.Online:
                    _onlinePage = state.Page;
                    _onlineIndex = state.Index;
                    break;
                default:
                    _mainPage = state.Page;
                    _mainIndex = state.Index;
                    break;
            }
        }

        private void CloseVisibleMenu(VehicleMenuKind kind)
        {
            switch (kind)
            {
                case VehicleMenuKind.Favorites:
                    _isFavoritesVisible = false;
                    break;
                case VehicleMenuKind.Online:
                    _isOnlineVisible = false;
                    break;
                default:
                    _isMainVisible = false;
                    _mainGroupIndex = -1;
                    break;
            }
        }

        private void SpawnVehicle(VehicleMenuEntry vehicle)
        {
            if (vehicle.HasModelHash)
            {
                _spawner.Spawn(vehicle.ModelHash, vehicle.DisplayName);
                return;
            }

            _spawner.Spawn(vehicle.VehicleName);
        }

        private void ToggleFavorite(VehicleMenuKind kind, VehicleMenuEntry vehicle)
        {
            if (kind == VehicleMenuKind.Favorites)
            {
                _favorites.Remove(vehicle.FavoriteName);
                return;
            }

            _favorites.Add(vehicle.FavoriteName);
        }

        private static VehicleMenuEntry[] CreateVehicleEntries(IEnumerable<string> vehicleNames)
        {
            return vehicleNames
                .Select(vehicleName => VehicleMenuEntry.Vehicle(vehicleName))
                .ToArray();
        }

        private static VehicleMenuEntry[] CreateFavoriteEntries(IEnumerable<string> vehicleNames)
        {
            return vehicleNames
                .Select(vehicleName => VehicleMenuEntry.Favorite(vehicleName))
                .ToArray();
        }

        private static VehicleGroupCatalog[] BuildVehicleGroups(IEnumerable<VehicleMenuEntry> entries)
        {
            return entries
                .Where(entry => entry.IsSelectable)
                .Select(entry => new
                {
                    Entry = entry,
                    Group = GetVehicleGroup(entry)
                })
                .GroupBy(item => item.Group)
                .OrderBy(group => GetVehicleGroupOrder(group.Key))
                .Select(group => new VehicleGroupCatalog(
                    GetVehicleGroupName(group.Key),
                    group
                        .OrderBy(item => item.Entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .Select(item => item.Entry)
                        .ToArray()))
                .Where(group => group.Vehicles.Length > 0)
                .ToArray();
        }

        private static VehicleGroupCatalog[] BuildMainVehicleGroups(IEnumerable<VehicleHash> vehicleHashes)
        {
            return vehicleHashes
                .Select(vehicleHash => new
                {
                    Name = vehicleHash.ToString(),
                    Hash = unchecked((int)(uint)vehicleHash),
                    Group = GetVehicleGroup(vehicleHash)
                })
                .GroupBy(vehicle => vehicle.Hash)
                .Select(group => group
                    .OrderBy(vehicle => vehicle.Name, StringComparer.OrdinalIgnoreCase)
                    .First())
                .GroupBy(vehicle => vehicle.Group)
                .OrderBy(group => GetVehicleGroupOrder(group.Key))
                .Select(group => new VehicleGroupCatalog(
                    GetVehicleGroupName(group.Key),
                    group
                        .OrderBy(vehicle => vehicle.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(vehicle => VehicleMenuEntry.Vehicle(vehicle.Name, vehicle.Hash))
                        .ToArray()))
                .Where(group => group.Vehicles.Length > 0)
                .ToArray();
        }

        private static VehicleGroup GetVehicleGroup(VehicleMenuEntry entry)
        {
            if (entry.HasModelHash)
            {
                return GetVehicleGroup(entry.ModelHash);
            }

            return GetVehicleGroup(entry.VehicleName);
        }

        private static VehicleGroup GetVehicleGroup(string vehicleName)
        {
            if (string.IsNullOrWhiteSpace(vehicleName))
            {
                return VehicleGroup.Other;
            }

            VehicleHash vehicleHash;
            if (Enum.TryParse(vehicleName, true, out vehicleHash))
            {
                return GetVehicleGroup(vehicleHash);
            }

            return VehicleGroup.Other;
        }

        private static VehicleGroup GetVehicleGroup(int hash)
        {
            if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BICYCLE, hash))
            {
                return VehicleGroup.Bicycle;
            }

            if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BIKE, hash) ||
                Function.Call<bool>(Hash.IS_THIS_MODEL_A_QUADBIKE, hash) ||
                Function.Call<bool>(Hash.IS_THIS_MODEL_AN_AMPHIBIOUS_QUADBIKE, hash))
            {
                return VehicleGroup.Motorcycle;
            }

            if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BOAT, hash) ||
                Function.Call<bool>(Hash.IS_THIS_MODEL_A_JETSKI, hash))
            {
                return VehicleGroup.Boat;
            }

            if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_HELI, hash))
            {
                return VehicleGroup.Helicopter;
            }

            if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_PLANE, hash))
            {
                return VehicleGroup.Plane;
            }

            if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_TRAIN, hash))
            {
                return VehicleGroup.Train;
            }

            if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_CAR, hash) ||
                Function.Call<bool>(Hash.IS_THIS_MODEL_AN_AMPHIBIOUS_CAR, hash))
            {
                return VehicleGroup.Car;
            }

            return VehicleGroup.Other;
        }

        private static VehicleGroup GetVehicleGroup(VehicleHash vehicleHash)
        {
            return GetVehicleGroup(unchecked((int)(uint)vehicleHash));
        }

        private static string GetVehicleGroupName(VehicleGroup group)
        {
            switch (group)
            {
                case VehicleGroup.Car: return "Машины";
                case VehicleGroup.Motorcycle: return "Мотоциклы";
                case VehicleGroup.Boat: return "Лодки";
                case VehicleGroup.Helicopter: return "Вертолеты";
                case VehicleGroup.Plane: return "Самолеты";
                case VehicleGroup.Bicycle: return "Велосипеды";
                case VehicleGroup.Train: return "Поезда";
                default: return "Прочее";
            }
        }

        private static int GetVehicleGroupOrder(VehicleGroup group)
        {
            switch (group)
            {
                case VehicleGroup.Car: return 0;
                case VehicleGroup.Motorcycle: return 1;
                case VehicleGroup.Boat: return 2;
                case VehicleGroup.Helicopter: return 3;
                case VehicleGroup.Plane: return 4;
                case VehicleGroup.Bicycle: return 5;
                case VehicleGroup.Train: return 6;
                default: return 7;
            }
        }

        private static void MoveSelection(VehicleMenuEntry[] vehicles, ref MenuState state, int direction)
        {
            var startIndex = state.Page * ModSettings.VehicleMenuItemsPerPage;
            var endIndex = Math.Min(startIndex + ModSettings.VehicleMenuItemsPerPage, vehicles.Length);
            var index = state.Index;

            for (var step = startIndex; step < endIndex; step++)
            {
                index += direction;
                if (index < startIndex)
                {
                    index = endIndex - 1;
                }
                else if (index >= endIndex)
                {
                    index = startIndex;
                }

                if (vehicles[index].IsSelectable)
                {
                    state.Index = index;
                    return;
                }
            }
        }

        private static void MoveToSelectableOnCurrentPage(VehicleMenuEntry[] vehicles, ref MenuState state, int direction)
        {
            if (vehicles.Length == 0)
            {
                return;
            }

            var startIndex = state.Page * ModSettings.VehicleMenuItemsPerPage;
            var endIndex = Math.Min(startIndex + ModSettings.VehicleMenuItemsPerPage, vehicles.Length);
            var index = Math.Min(Math.Max(state.Index, startIndex), endIndex - 1);

            for (var step = startIndex; step < endIndex; step++)
            {
                if (vehicles[index].IsSelectable)
                {
                    state.Index = index;
                    return;
                }

                index += direction;
                if (index < startIndex)
                {
                    index = endIndex - 1;
                }
                else if (index >= endIndex)
                {
                    index = startIndex;
                }
            }

            MoveToSelectable(vehicles, ref state, direction);
        }

        private static void MoveToSelectable(VehicleMenuEntry[] vehicles, ref MenuState state, int direction)
        {
            if (vehicles.Length == 0 || vehicles[state.Index].IsSelectable)
            {
                return;
            }

            var index = state.Index;
            for (var step = 0; step < vehicles.Length; step++)
            {
                index += direction;
                if (index < 0)
                {
                    index = vehicles.Length - 1;
                }
                else if (index >= vehicles.Length)
                {
                    index = 0;
                }

                if (!vehicles[index].IsSelectable)
                {
                    continue;
                }

                state.Index = index;
                state.Page = index / ModSettings.VehicleMenuItemsPerPage;
                return;
            }
        }

        private static int GetMaxPage(int itemCount)
        {
            return itemCount == 0
                ? 0
                : (itemCount - 1) / ModSettings.VehicleMenuItemsPerPage;
        }

        private struct MenuState
        {
            public int Page;
            public int Index;

            public MenuState(int page, int index)
            {
                Page = page;
                Index = index;
            }

            public void Clamp(int itemCount)
            {
                if (itemCount <= 0)
                {
                    Page = 0;
                    Index = 0;
                    return;
                }

                Page = Math.Min(Page, GetMaxPage(itemCount));
                Index = Math.Min(Index, itemCount - 1);
                Index = Math.Max(Index, Page * ModSettings.VehicleMenuItemsPerPage);
            }
        }

        private enum VehicleGroup
        {
            Car,
            Motorcycle,
            Boat,
            Helicopter,
            Plane,
            Bicycle,
            Train,
            Other
        }

        private sealed class VehicleGroupCatalog
        {
            public readonly string Name;
            public readonly VehicleMenuEntry[] Vehicles;

            public VehicleGroupCatalog(string name, VehicleMenuEntry[] vehicles)
            {
                Name = name;
                Vehicles = vehicles;
            }
        }
    }
}
