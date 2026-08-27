using System;
using System.Linq;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using gta.Core;
using gta.Input;
using gta.Peds;
using gta.Player;
using gta.Vehicles;
using gta.Worlds;

namespace gta
{
    public class MainScript : Script
    {
        private static readonly RelationshipGroup PlayerGroup = GTA.World.AddRelationshipGroup("player");

        private readonly PlayerCheatService _playerCheats;
        private readonly VehicleService _vehicles;
        private readonly VehicleIndicatorService _vehicleIndicators;
        private readonly VehicleUpgradeService _vehicleUpgrades;
        private readonly VehicleMenuController _vehicleMenu;
        private readonly HelpOverlayService _help;
        private readonly WeaponService _weapons;
        private readonly ClothingService _clothing;
        private readonly CompanionService _companions;
        private readonly PoliceService _police;
        private readonly PedPhysicsService _pedPhysics;
        private readonly NorthYanktonAliveService _northYanktonAlive;
        private readonly NoClipService _noClip;
        private readonly CameraLockService _cameraLock;
        private readonly AnimalMorphService _animalMorph;
        private readonly InputRouter _input;
        private readonly Ai.AiController _aiController;
        private readonly SpeedLimiterService _speedLimiter;
        private readonly BongService _bongService;
        private readonly BulletTimeService _bulletTime;
        private readonly VehicleNitroService _nitroService;
        private readonly WorldVehicleStore _worldVehicleStore;
        private readonly VehicleSirenService _vehicleSirens;
        private readonly OnlineRadioService _onlineRadio;
        private readonly OnlineTrafficService _onlineTraffic;
        private readonly TelekinesisService _telekinesis;
        private readonly InflatableBoatService _inflatableBoats;
        private readonly PoliceOfficerService _policeOfficer;
        private readonly PlayerInteractionMenuService _playerInteractionMenu;

        public MainScript()
        {
            ModLogger.Log("INIT", "MainScript constructor started");
            Notifier.Show("Инициализация завершена");
            Game.Player.Character.RelationshipGroup = PlayerGroup;

            var pedQuery = new PedQueryService();
            _pedPhysics = new PedPhysicsService();
            _weapons = new WeaponService(Enum.GetValues(typeof(WeaponHash)).Cast<WeaponHash>().ToArray());
            _clothing = new ClothingService();
            _worldVehicleStore = new WorldVehicleStore();
            var vehicleSpawner = new VehicleSpawner(_worldVehicleStore);
            var vehicleFavorites = new VehicleFavoritesStore(Core.ScriptPaths.FavoritesPath);
            var generatedVehicles = new GeneratedVehicleCatalog(Core.ScriptPaths.GeneratedVehiclesPath);
            _vehicleUpgrades = new VehicleUpgradeService();
            _help = new HelpOverlayService();
            var northYanktonLoader = new NorthYanktonLoader();

            _playerCheats = new PlayerCheatService();
            _vehicles = new VehicleService();
            _vehicleIndicators = new VehicleIndicatorService();
            _vehicleSirens = new VehicleSirenService();
            _onlineRadio = new OnlineRadioService();
            _onlineTraffic = new OnlineTrafficService(_worldVehicleStore, generatedVehicles);
            _vehicleMenu = new VehicleMenuController(
                Enum.GetValues(typeof(VehicleHash)).Cast<VehicleHash>().ToArray(),
                vehicleFavorites,
                generatedVehicles,
                vehicleSpawner,
                new VehicleMenuRenderer());
            _companions = new CompanionService(PlayerGroup, pedQuery);
            _police = new PoliceService();
            _northYanktonAlive = new NorthYanktonAliveService(northYanktonLoader);
            _noClip = new NoClipService();
            _cameraLock = new CameraLockService();
            _animalMorph = new AnimalMorphService(PlayerGroup);
            _speedLimiter = new SpeedLimiterService();
            _bongService = new BongService();
            _bulletTime = new BulletTimeService();
            _nitroService = new VehicleNitroService();
            _telekinesis = new TelekinesisService();
            _inflatableBoats = new InflatableBoatService();
            _policeOfficer = new PoliceOfficerService(PlayerGroup, pedQuery);
            var outfitStore = new OutfitStore();

            _playerInteractionMenu = new PlayerInteractionMenuService(
                _playerCheats,
                _clothing,
                _animalMorph,
                _bongService,
                _bulletTime,
                _noClip,
                _telekinesis,
                _weapons,
                _companions,
                _vehicles,
                _vehicleUpgrades,
                _policeOfficer,
                outfitStore);

            _input = new InputRouter(
                _vehicleIndicators,
                _companions,
                _vehicles,
                _vehicleUpgrades,
                _vehicleMenu,
                _help,
                _weapons,
                _clothing,
                _police,
                pedQuery,
                _pedPhysics,
                _northYanktonAlive,
                _noClip,
                _cameraLock,
                _animalMorph,
                _speedLimiter,
                _bongService,
                _worldVehicleStore,
                _bulletTime,
                _playerInteractionMenu,
                _policeOfficer);

            _aiController = new Ai.AiController(pedQuery);

            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            Tick += OnTick;
            Aborted += OnAborted;
            ModLogger.Log("INIT", "MainScript constructor finished");
        }

        private void OnTick(object sender, EventArgs e)
        {
            _worldVehicleStore.RestoreSavedVehiclesOnTick();
            _onlineRadio.UnlockAllOnlineRadioStations();
            _onlineTraffic.Update();
            if (_playerCheats.IsEnabled)
            {
                _playerCheats.Apply();
            }
            _cameraLock.Update();
            _noClip.Update();
            _vehicles.ProtectCurrentVehicle(_speedLimiter.IsLimiterActive);
            _speedLimiter.Update();
            _bongService.Update();
            _bulletTime.Update();
            _nitroService.Update();
            _telekinesis.Update();
            _inflatableBoats.Update();
            _policeOfficer.Update();
            _playerInteractionMenu.Update();
            _vehicles.ApplyForcedHeadlights();
            _vehicleIndicators.ApplyToCurrentVehicle();
            _vehicleSirens.Update();
            _vehicleUpgrades.Update();
            _vehicleUpgrades.Draw();
            _playerInteractionMenu.Draw();
            _policeOfficer.Draw();
            _weapons.Draw();
            _clothing.Draw();
            _companions.Update();
            _police.ApplyWantedState();
            _pedPhysics.Update();
            _northYanktonAlive.Update();
            _vehicleMenu.Draw();
            _help.Draw();
            _aiController.Update();
            _aiController.ProcessQueue();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode == Keys.D0)
                {
                    ModLogger.Log("INPUT", "OnKeyDown received D0");
                }

                _input.Handle(e);
                _aiController.HandleKeyDown(e);
            }
            catch (Exception ex)
            {
                Notifier.Show($"Ошибка: {ex.Message}");
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                _aiController.HandleKeyUp(e);
            }
            catch (Exception ex)
            {
                Notifier.Show($"Ошибка: {ex.Message}");
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            _speedLimiter.Abort();
            _bongService.Abort();
            _bulletTime.Abort();
            _nitroService.Abort();
            _telekinesis.Release(false);
            _inflatableBoats.ExitBoat();
            _worldVehicleStore.Abort();
        }
    }
}
