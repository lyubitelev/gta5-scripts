using System.Windows.Forms;
using gta.Core;
using gta.Peds;
using gta.Player;
using gta.Vehicles;
using GTA;
using gta.Worlds;

namespace gta.Input
{
    internal sealed class InputRouter
    {
        private readonly VehicleIndicatorService _vehicleIndicators;
        private readonly CompanionService _companions;
        private readonly VehicleService _vehicles;
        private readonly VehicleUpgradeService _vehicleUpgrades;
        private readonly VehicleMenuController _vehicleMenu;
        private readonly HelpOverlayService _help;
        private readonly WeaponService _weapons;
        private readonly ClothingService _clothing;
        private readonly PoliceService _police;
        private readonly PedQueryService _pedQuery;
        private readonly PedPhysicsService _pedPhysics;
        private readonly NorthYanktonAliveService _northYanktonAlive;
        private readonly NoClipService _noClip;
        private readonly CameraLockService _cameraLock;
        private readonly AnimalMorphService _animalMorph;
        private readonly SpeedLimiterService _speedLimiter;
        private readonly WorldVehicleStore _worldVehicleStore;
        private readonly BongService _bongService;
        private readonly BulletTimeService _bulletTime;
        private readonly PlayerInteractionMenuService _playerInteractionMenu;
        private readonly PoliceOfficerService _policeOfficer;

        public InputRouter(
            VehicleIndicatorService vehicleIndicators,
            CompanionService companions,
            VehicleService vehicles,
            VehicleUpgradeService vehicleUpgrades,
            VehicleMenuController vehicleMenu,
            HelpOverlayService help,
            WeaponService weapons,
            ClothingService clothing,
            PoliceService police,
            PedQueryService pedQuery,
            PedPhysicsService pedPhysics,
            NorthYanktonAliveService northYanktonAlive,
            NoClipService noClip,
            CameraLockService cameraLock,
            AnimalMorphService animalMorph,
            SpeedLimiterService speedLimiter,
            BongService bongService,
            WorldVehicleStore worldVehicleStore,
            BulletTimeService bulletTime,
            PlayerInteractionMenuService playerInteractionMenu,
            PoliceOfficerService policeOfficer)
        {
            _vehicleIndicators = vehicleIndicators;
            _companions = companions;
            _vehicles = vehicles;
            _vehicleUpgrades = vehicleUpgrades;
            _vehicleMenu = vehicleMenu;
            _help = help;
            _weapons = weapons;
            _clothing = clothing;
            _police = police;
            _pedQuery = pedQuery;
            _pedPhysics = pedPhysics;
            _northYanktonAlive = northYanktonAlive;
            _noClip = noClip;
            _cameraLock = cameraLock;
            _animalMorph = animalMorph;
            _speedLimiter = speedLimiter;
            _bongService = bongService;
            _worldVehicleStore = worldVehicleStore;
            _bulletTime = bulletTime;
            _playerInteractionMenu = playerInteractionMenu;
            _policeOfficer = policeOfficer;
        }

        public void Handle(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                _help.Toggle();
                return;
            }

            if (e.KeyCode == Keys.D0)
            {
                ModLogger.Log("INPUT", $"D0 routed globally. Help={_help.IsVisible}, VehicleUpgradeMenu={_vehicleUpgrades.IsMenuVisible}, WeaponMenu={_weapons.IsMenuVisible}, ClothingMenu={_clothing.IsMenuVisible}, VehicleMenu={_vehicleMenu.IsVisible}, PlayerMenu={_playerInteractionMenu?.IsMenuVisible}");
                _cameraLock.Toggle();
                return;
            }

            if (_help.IsVisible)
            {
                _help.Handle(e);
                return;
            }

            if (_policeOfficer != null && _policeOfficer.IsQuickMenuOpen)
            {
                _policeOfficer.ProcessKey(e);
                return;
            }

            if (_playerInteractionMenu != null && _playerInteractionMenu.IsMenuVisible)
            {
                _playerInteractionMenu.ProcessKey(e);
                return;
            }

            if (_vehicleUpgrades.IsMenuVisible)
            {
                _vehicleUpgrades.Handle(e);
                return;
            }

            if (_weapons.IsMenuVisible)
            {
                _weapons.Handle(e);
                return;
            }

            if (_clothing.IsMenuVisible)
            {
                _clothing.Handle(e);
                return;
            }

            if (_vehicleMenu.IsVisible)
            {
                HandleVehicleMenuInput(e);
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.NumPad7:
                    _vehicleIndicators.ToggleRight();
                    break;
                case Keys.NumPad9:
                    _vehicleIndicators.ToggleLeft();
                    break;
                case Keys.NumPad3:
                    _companions.Spawn();
                    break;
                case Keys.NumPad1:
                    _companions.ToggleChauffeurCruise();
                    break;
                case Keys.NumPad6:
                    _companions.ReleaseAll();
                    break;
                case Keys.X:
                    var player = Game.Player.Character;
                    if (player != null && player.Exists() && player.IsInVehicle())
                    {
                        _vehicleUpgrades.ToggleMenu();
                    }
                    else if (_playerInteractionMenu != null)
                    {
                        _playerInteractionMenu.ToggleMenu();
                    }
                    break;
                case Keys.N:
                    _vehicles.RepairCurrentVehicle();
                    break;
                case Keys.O:
                    _vehicleMenu.ToggleMain();
                    break;
                case Keys.K:
                    SmashNearbyPeds();
                    break;
                case Keys.OemOpenBrackets:
                    _vehicleMenu.ToggleOnline();
                    break;
                case Keys.OemCloseBrackets:
                    _vehicleMenu.ToggleFavorites();
                    break;
                case Keys.L:
                    _weapons.ToggleMenu();
                    break;
                case Keys.Decimal:
                case Keys.Separator:
                    _clothing.ToggleMenu();
                    break;
                case Keys.B:
                    _police.ToggleSuppression();
                    break;
                case Keys.Y:
                    _northYanktonAlive.Load();
                    break;
                case Keys.U:
                    _northYanktonAlive.Toggle();
                    break;
                case Keys.J:
                    _noClip.Toggle();
                    break;
                case Keys.H:
                    _vehicles.ToggleForcedHeadlights();
                    break;
                case Keys.D8:
                    _bongService.Start();
                    break;
                case Keys.D9:
                    _speedLimiter.ToggleLimiter();
                    break;
                case Keys.Add:
                    _speedLimiter.IncreaseLimit();
                    break;
                case Keys.D7:
                    var vehicle = Game.Player.Character.CurrentVehicle;
                    _worldVehicleStore.ToggleSaveCurrentVehicle(vehicle);
                    break;
                case Keys.T:
                    _bulletTime.Toggle();
                    break;
                case Keys.E:
                    var ch = Game.Player.Character;
                    if (ch != null && ch.Exists() && !ch.IsInVehicle())
                    {
                        _policeOfficer.HandleQuickCommandKey();
                    }
                    break;
            }
        }

        private void HandleVehicleMenuInput(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.O:
                    _vehicleMenu.ToggleMain();
                    break;
                case Keys.OemOpenBrackets:
                    _vehicleMenu.ToggleOnline();
                    break;
                case Keys.OemCloseBrackets:
                    _vehicleMenu.ToggleFavorites();
                    break;
                default:
                    _vehicleMenu.Handle(e);
                    break;
            }
        }

        private void SmashNearbyPeds()
        {
            _pedPhysics.QueueSmashWithBlood(_pedQuery.GetNearbyPeds(35.0f, 24), 30.0f, 100.0f);
        }
    }
}
