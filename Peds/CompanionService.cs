using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;

namespace gta.Peds
{
    internal sealed class CompanionService
    {
        private const float ChauffeurCruiseSpeed = 22f;
        private const int MaxVehicleSeatIndex = 14;

        private static readonly VehicleDrivingFlags ChauffeurDrivingFlags =
            VehicleDrivingFlags.DrivingModeAvoidVehiclesObeyLights |
            VehicleDrivingFlags.StopAtTrafficLights |
            VehicleDrivingFlags.StopForPeds |
            VehicleDrivingFlags.SwerveAroundAllVehicles |
            VehicleDrivingFlags.SteerAroundObjects |
            VehicleDrivingFlags.UseWanderFallbackInsteadOfStraightLine;

        private readonly List<Ped> _companions = new List<Ped>();
        private readonly RelationshipGroup _group;
        private readonly PedQueryService _pedQuery;
        private Ped _chauffeur;
        private Vehicle _chauffeurVehicle;
        private bool _isChauffeurActive;
        private DateTime _nextChauffeurTaskUtc = DateTime.MinValue;

        public CompanionService(RelationshipGroup group, PedQueryService pedQuery)
        {
            _group = group;
            _pedQuery = pedQuery;
        }

        public void Spawn()
        {
            var player = Game.Player.Character;
            var playerPosition = player.Position - player.ForwardVector * 5;
            var companion = GTA.World.CreatePed(PedSelector.GetRandomProstituteModel(), playerPosition);

            companion.RelationshipGroup = _group;
            MakeInvincible(companion);
            companion.Weapons.Give(WeaponHash.AssaultRifle, 999, true, true);
            companion.Weapons.Give(WeaponHash.MicroSMG, 999, false, true);
            companion.Task.FollowToOffsetFromEntity(player, new Vector3(0, 2, 0), ModSettings.CompanionFollowSpeed);

            _companions.Add(companion);
        }

        public void ToggleChauffeurCruise()
        {
            if (_isChauffeurActive)
            {
                StopChauffeurCruise();
                Notifier.Show("Шофер остановлен");
                return;
            }

            var player = Game.Player.Character;
            if (player == null || !player.Exists() || !player.IsInVehicle())
            {
                Notifier.Show("Сначала сядь в транспорт");
                return;
            }

            var vehicle = player.CurrentVehicle;
            if (vehicle == null || !vehicle.Exists())
            {
                return;
            }

            if (!PreparePlayerPassengerSeat(player, vehicle))
            {
                Notifier.Show("Нет свободного места для игрока");
                return;
            }

            var driver = GetOrCreateDriver(vehicle);
            if (driver == null || !driver.Exists())
            {
                Notifier.Show("Не удалось создать шофера");
                return;
            }

            SetupCompanion(driver);
            Function.Call(Hash.SET_PED_INTO_VEHICLE, driver.Handle, vehicle.Handle, (int)VehicleSeat.Driver);
            StartChauffeurCruise(driver, vehicle);

            _chauffeur = driver;
            _chauffeurVehicle = vehicle;
            _isChauffeurActive = true;
            _nextChauffeurTaskUtc = DateTime.UtcNow.AddSeconds(5);

            if (!_companions.Contains(driver))
            {
                _companions.Add(driver);
            }

            Notifier.Show("Шофер катает по городу");
        }

        public void ReleaseAll()
        {
            StopChauffeurCruise();

            foreach (var companion in _companions.Where(x => x != null && x.Exists()))
            {
                companion.Task.ClearAllImmediately();
                companion.MarkAsNoLongerNeeded();
                companion.Delete();
            }

            _companions.Clear();
        }

        public void Update()
        {
            if (_companions.Count == 0)
            {
                return;
            }

            var player = Game.Player.Character;
            var hostilePeds = GTA.World.GetNearbyPeds(player, 50f)
                .Where(_pedQuery.IsHostile)
                .ToList();
            var playerVehicle = player.CurrentVehicle;

            foreach (var companion in _companions.Where(x => x != null && x.Exists()).ToList())
            {
                MakeInvincible(companion);

                if (IsChauffeur(companion))
                {
                    MaintainChauffeurCruise(player);
                    continue;
                }

                if (TryEnterPlayerVehicle(companion, player, playerVehicle))
                {
                    continue;
                }

                if (!companion.IsInVehicle() || !player.IsInVehicle())
                {
                    FollowPlayerAndAttackHostiles(companion, player, hostilePeds);
                }
            }
        }

        private static void MakeInvincible(Ped ped)
        {
            ped.IsInvincible = true;
            ped.Armor = ModSettings.MaxStat;
            ped.Health = ModSettings.MaxStat;
            ped.ClearVisibleDamage();
        }

        private void SetupCompanion(Ped companion)
        {
            companion.RelationshipGroup = _group;
            MakeInvincible(companion);
            companion.Weapons.Give(WeaponHash.AssaultRifle, 999, true, true);
            companion.Weapons.Give(WeaponHash.MicroSMG, 999, false, true);
        }

        private void StopChauffeurCruise()
        {
            _isChauffeurActive = false;
            _nextChauffeurTaskUtc = DateTime.MinValue;

            if (_chauffeur != null && _chauffeur.Exists())
            {
                _chauffeur.Task.ClearAll();
            }

            _chauffeur = null;
            _chauffeurVehicle = null;
        }

        private bool IsChauffeur(Ped companion)
        {
            return _isChauffeurActive &&
                   _chauffeur != null &&
                   _chauffeur.Exists() &&
                   companion.Handle == _chauffeur.Handle;
        }

        private void MaintainChauffeurCruise(Ped player)
        {
            if (!_isChauffeurActive ||
                _chauffeur == null ||
                !_chauffeur.Exists() ||
                _chauffeurVehicle == null ||
                !_chauffeurVehicle.Exists() ||
                !player.IsInVehicle(_chauffeurVehicle))
            {
                StopChauffeurCruise();
                return;
            }

            if (_chauffeurVehicle.Driver == null ||
                !_chauffeurVehicle.Driver.Exists() ||
                _chauffeurVehicle.Driver.Handle != _chauffeur.Handle)
            {
                Function.Call(Hash.SET_PED_INTO_VEHICLE, _chauffeur.Handle, _chauffeurVehicle.Handle, (int)VehicleSeat.Driver);
            }

            if (DateTime.UtcNow < _nextChauffeurTaskUtc)
            {
                return;
            }

            StartChauffeurCruise(_chauffeur, _chauffeurVehicle);
            _nextChauffeurTaskUtc = DateTime.UtcNow.AddSeconds(5);
        }

        private static void StartChauffeurCruise(Ped driver, Vehicle vehicle)
        {
            Function.Call(Hash.SET_DRIVER_ABILITY, driver.Handle, 1.0f);
            Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, driver.Handle, 0.15f);
            Function.Call(Hash.SET_PED_KEEP_TASK, driver.Handle, true);
            driver.Task.CruiseWithVehicle(vehicle, ChauffeurCruiseSpeed, ChauffeurDrivingFlags);
        }

        private Ped GetOrCreateDriver(Vehicle vehicle)
        {
            var driver = vehicle.Driver;
            if (driver != null && driver.Exists() && !driver.IsPlayer)
            {
                return driver;
            }

            if (!Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, vehicle.Handle, (int)VehicleSeat.Driver, false))
            {
                return null;
            }

            var spawnPosition = vehicle.Position - vehicle.ForwardVector * 2f;
            return GTA.World.CreatePed(PedSelector.GetRandomProstituteModel(), spawnPosition);
        }

        private static bool PreparePlayerPassengerSeat(Ped player, Vehicle vehicle)
        {
            if (vehicle.Driver == null || !vehicle.Driver.Exists() || vehicle.Driver.Handle != player.Handle)
            {
                return true;
            }

            var seat = FindBestPassengerSeat(vehicle);
            if (!seat.HasValue)
            {
                return false;
            }

            Function.Call(Hash.SET_PED_INTO_VEHICLE, player.Handle, vehicle.Handle, seat.Value);
            return true;
        }

        private static int? FindBestPassengerSeat(Vehicle vehicle)
        {
            var turretSeat = FindFreePassengerSeat(vehicle, true);
            return turretSeat.HasValue
                ? turretSeat
                : FindFreePassengerSeat(vehicle, false);
        }

        private static int? FindFreePassengerSeat(Vehicle vehicle, bool turretOnly)
        {
            for (var seat = 0; seat <= MaxVehicleSeatIndex; seat++)
            {
                if (!Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, vehicle.Handle, seat, false))
                {
                    continue;
                }

                var isTurretSeat = Function.Call<bool>(Hash.IS_TURRET_SEAT, vehicle.Handle, seat);
                if (turretOnly != isTurretSeat)
                {
                    continue;
                }

                return seat;
            }

            return null;
        }

        public static int? FindPrioritizedFreePassengerSeat(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) return null;

            // 1. Front Passenger Seat (Right Front)
            if (Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, vehicle.Handle, (int)VehicleSeat.Passenger, false))
            {
                return (int)VehicleSeat.Passenger;
            }

            // 2. Rear Left Seat
            if (Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, vehicle.Handle, (int)VehicleSeat.LeftRear, false))
            {
                return (int)VehicleSeat.LeftRear;
            }

            // 3. Rear Right Seat
            if (Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, vehicle.Handle, (int)VehicleSeat.RightRear, false))
            {
                return (int)VehicleSeat.RightRear;
            }

            // 4. Additional Passenger Seats (3 to MaxVehicleSeatIndex)
            for (var seat = 3; seat <= MaxVehicleSeatIndex; seat++)
            {
                if (Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, vehicle.Handle, seat, false))
                {
                    return seat;
                }
            }

            return null;
        }

        public static bool TryEnterPlayerVehicle(Ped companion, Ped player, Vehicle playerVehicle)
        {
            if (!player.IsInVehicle() || playerVehicle == null)
            {
                return false;
            }

            if (playerVehicle.PassengerCount == playerVehicle.PassengerCapacity || companion.IsInVehicle(playerVehicle))
            {
                return false;
            }

            if (!companion.IsEnteringVehicle)
            {
                var targetSeat = FindPrioritizedFreePassengerSeat(playerVehicle);
                if (targetSeat.HasValue)
                {
                    companion.Task.EnterVehicle(
                        playerVehicle,
                        (VehicleSeat)targetSeat.Value,
                        -1,
                        ModSettings.CompanionEnterVehicleSpeed,
                        EnterVehicleFlags.WarpIfDoorIsBlocked |
                        EnterVehicleFlags.WarpIfShuffleLinkIsBlocked |
                        EnterVehicleFlags.BlockSeatShuffling);
                }
                else
                {
                    companion.Task.EnterVehicle(
                        playerVehicle,
                        speed: ModSettings.CompanionEnterVehicleSpeed,
                        flag: EnterVehicleFlags.WarpIfDoorIsBlocked |
                              EnterVehicleFlags.WarpIfShuffleLinkIsBlocked |
                              EnterVehicleFlags.BlockSeatShuffling);
                }
            }

            return true;
        }

        private static void FollowPlayerAndAttackHostiles(Ped companion, Ped player, IEnumerable<Ped> hostilePeds)
        {
            companion.Task.FollowToOffsetFromEntity(player, new Vector3(0, 2, 0), ModSettings.CompanionFollowSpeed);
            companion.Task.FollowPointRoute(3f, player.Position + player.ForwardVector * 4);

            try
            {
                foreach (var hostile in hostilePeds)
                {
                    companion.Task.Combat(hostile, TaskCombatFlags.None, TaskThreatResponseFlags.None);
                }
            }
            catch (Exception)
            {
                // ScriptHook can invalidate entities between scan and task assignment.
            }
        }
    }
}
