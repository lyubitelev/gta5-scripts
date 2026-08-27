using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;

namespace gta.Vehicles
{
    internal sealed class InflatableBoatService
    {
        private const string SittingAnimDict = "amb@world_human_picnic@male@idle_a";
        private const string SittingAnimName = "idle_a";

        private static readonly HashSet<int> KnownBoatModelHashes = new HashSet<int>
        {
            Function.Call<int>(Hash.GET_HASH_KEY, "p_inflat_boat_s"),
            Function.Call<int>(Hash.GET_HASH_KEY, "prop_rub_boat"),
            Function.Call<int>(Hash.GET_HASH_KEY, "prop_rub_boat_01"),
            Function.Call<int>(Hash.GET_HASH_KEY, "p_rub_boat_s"),
            Function.Call<int>(Hash.GET_HASH_KEY, "v_res_tre_inflatboat"),
            Function.Call<int>(Hash.GET_HASH_KEY, "apa_mp_apa_yacht_dinghy"),
            Function.Call<int>(Hash.GET_HASH_KEY, "prop_inflat_boat_01"),
            Function.Call<int>(Hash.GET_HASH_KEY, "prop_inflatableboat"),
            Function.Call<int>(Hash.GET_HASH_KEY, "prop_yacht_dinghy"),
            Function.Call<int>(Hash.GET_HASH_KEY, "prop_ld_boat_01"),
            Function.Call<int>(Hash.GET_HASH_KEY, "prop_boat_water_treadmill")
        };

        private readonly List<Prop> _idleBoats = new List<Prop>();
        private Prop _currentBoat;
        private int _currentBoatModelHash;
        private Vehicle _proxyVehicle;
        private bool _isDriving;
        private float _idleBobbingTimer;

        public bool IsDriving => _isDriving;

        public void Update()
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists() || player.IsDead)
            {
                ExitBoat();
                return;
            }

            // Update natural wave bobbing for all idle boats in the water
            UpdateIdleBoatsPhysics();

            if (!_isDriving)
            {
                CheckBoatInteraction(player);
            }
            else
            {
                ProcessProxyVehicleDriving(player);
            }
        }

        private void UpdateIdleBoatsPhysics()
        {
            _idleBobbingTimer += 0.04f;
            float waveBob = (float)Math.Sin(_idleBobbingTimer) * 0.06f;

            for (int i = _idleBoats.Count - 1; i >= 0; i--)
            {
                var boat = _idleBoats[i];
                if (boat == null || !boat.Exists())
                {
                    _idleBoats.RemoveAt(i);
                    continue;
                }

                // If currently being driven, skip idle bobbing
                if (_isDriving && boat == _currentBoat) continue;

                Vector3 bPos = boat.Position;
                var outZ = new OutputArgument();
                if (Function.Call<bool>(Hash.GET_WATER_HEIGHT, bPos.X, bPos.Y, bPos.Z, outZ))
                {
                    float waterZ = outZ.GetResult<float>();
                    float targetZ = waterZ - 0.18f + waveBob;
                    float newZ = bPos.Z + (targetZ - bPos.Z) * 0.15f;

                    boat.Position = new Vector3(bPos.X, bPos.Y, newZ);
                    Function.Call(Hash.SET_ENTITY_VELOCITY, boat.Handle, 0f, 0f, 0f);
                    boat.LocalRotationVelocity = Vector3.Zero;
                }
            }
        }

        private void CheckBoatInteraction(Ped player)
        {
            if (player.CurrentVehicle != null) return;

            Vector3 playerPos = player.Position;
            Entity candidateBoat = null;

            // 1. Raycast downwards directly under player's feet
            var downHit = GTA.World.Raycast(
                playerPos + new Vector3(0f, 0f, 0.5f),
                playerPos - new Vector3(0f, 0f, 2.2f),
                IntersectFlags.Everything,
                player);

            if (downHit.DidHit && downHit.HitEntity != null && downHit.HitEntity.Exists() && !(downHit.HitEntity is Ped) && !(downHit.HitEntity is Vehicle))
            {
                candidateBoat = downHit.HitEntity;
            }

            // 2. Scan nearby props if swimming close to boat (< 3.5m)
            if (candidateBoat == null)
            {
                var nearbyProps = GTA.World.GetNearbyProps(playerPos, 4.5f);
                if (nearbyProps != null)
                {
                    foreach (var prop in nearbyProps)
                    {
                        if (prop == null || !prop.Exists()) continue;
                        if (KnownBoatModelHashes.Contains(prop.Model.Hash))
                        {
                            candidateBoat = prop;
                            break;
                        }
                    }
                }
            }

            // 3. If a valid boat/prop is found near water or under feet
            if (candidateBoat != null && candidateBoat.Exists())
            {
                bool isKnownBoat = KnownBoatModelHashes.Contains(candidateBoat.Model.Hash);
                bool inWater = IsInOrNearWater(candidateBoat.Position);

                if (isKnownBoat || inWater)
                {
                    GTA.UI.Screen.ShowHelpTextThisFrame("Нажмите ~INPUT_ENTER~ чтобы сесть за руль надувной лодки");

                    if (Game.IsControlJustPressed(GTA.Control.Enter))
                    {
                        EnterBoatWithProxy(player, candidateBoat);
                    }
                }
            }
        }

        private void EnterBoatWithProxy(Ped player, Entity boat)
        {
            Vector3 spawnPos = boat.Position;
            float spawnHeading = boat.Heading;
            _currentBoatModelHash = boat.Model.Hash != 0 ? boat.Model.Hash : Function.Call<int>(Hash.GET_HASH_KEY, "p_inflat_boat_s");

            // Remove from idle list if present
            if (boat is Prop p)
            {
                _idleBoats.Remove(p);
            }

            // Delete original map-bound prop so it doesn't get culled by engine sector unloader
            boat.Delete();

            // 1. Load native low-profile Seashark chassis
            var boatModel = new Model(VehicleHash.Seashark);
            if (!boatModel.IsLoaded)
            {
                boatModel.Request(1500);
            }

            // 2. Spawn native boat chassis
            _proxyVehicle = GTA.World.CreateVehicle(boatModel, spawnPos, spawnHeading);
            if (_proxyVehicle == null || !_proxyVehicle.Exists())
            {
                Notifier.Show("Ошибка: не удалось создать шасси лодки");
                return;
            }

            // 3. Make proxy vehicle chassis 100% transparent via Alpha
            Function.Call(Hash.SET_ENTITY_ALPHA, _proxyVehicle.Handle, 0, false);
            _proxyVehicle.Opacity = 0;
            _proxyVehicle.IsPersistent = true;
            Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, _proxyVehicle.Handle, true, true);
            _proxyVehicle.IsEngineRunning = true;

            // 4. Create fresh, persistent scripted boat prop immune to map sector despawns
            CreateAndAttachBoatProp();

            // 5. Put player into driver seat of native boat
            player.SetIntoVehicle(_proxyVehicle, VehicleSeat.Driver);
            player.IsVisible = true;
            Function.Call(Hash.RESET_ENTITY_ALPHA, player.Handle);

            // 6. Play cross-legged picnic seated pose on the floor of the boat
            PlaySittingAnim(player);

            _isDriving = true;
            Notifier.Show("~b~Надувная лодка:~s~ Нативное управление и физика GTA V");
        }

        private void CreateAndAttachBoatProp()
        {
            if (_proxyVehicle == null || !_proxyVehicle.Exists()) return;

            var propModel = new Model(_currentBoatModelHash);
            if (!propModel.IsLoaded)
            {
                propModel.Request(1000);
            }

            _currentBoat = GTA.World.CreateProp(propModel, _proxyVehicle.Position, false, false);
            if (_currentBoat != null && _currentBoat.Exists())
            {
                _currentBoat.IsPersistent = true;
                Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, _currentBoat.Handle, true, true);
                Function.Call(Hash.SET_ENTITY_LOD_DIST, _currentBoat.Handle, 2500);
                Function.Call(Hash.SET_ENTITY_COLLISION, _currentBoat.Handle, false, false);
                Function.Call(Hash.RESET_ENTITY_ALPHA, _currentBoat.Handle);
                _currentBoat.AttachTo(_proxyVehicle, new Vector3(0f, 0.10f, 0.02f), Vector3.Zero);
            }
        }

        private void ProcessProxyVehicleDriving(Ped player)
        {
            if (_proxyVehicle == null || !_proxyVehicle.Exists())
            {
                ExitBoat();
                return;
            }

            // Keep proxy chassis transparent
            Function.Call(Hash.SET_ENTITY_ALPHA, _proxyVehicle.Handle, 0, false);

            // Auto-keeper: If game engine attempts to drop the prop in open ocean, restore it immediately
            if (_currentBoat == null || !_currentBoat.Exists())
            {
                CreateAndAttachBoatProp();
            }

            // Maintain cross-legged picnic sitting pose
            if (!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, player.Handle, SittingAnimDict, SittingAnimName, 3))
            {
                PlaySittingAnim(player);
            }

            // If player exits vehicle (or presses F / gets thrown out)
            if (player.CurrentVehicle != _proxyVehicle)
            {
                ExitBoat();
                return;
            }
        }

        private static void PlaySittingAnim(Ped player)
        {
            if (player == null || !player.Exists()) return;

            if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, SittingAnimDict))
            {
                Function.Call(Hash.REQUEST_ANIM_DICT, SittingAnimDict);
                int start = Game.GameTime;
                while (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, SittingAnimDict) && Game.GameTime - start < 300)
                {
                    Script.Yield();
                }
            }

            if (Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, SittingAnimDict))
            {
                // Flag 49 = Looping + Enable Player Driving Controls
                Function.Call(Hash.TASK_PLAY_ANIM, player.Handle, SittingAnimDict, SittingAnimName, 4.0f, -4.0f, -1, 49, 0, false, false, false);
            }
        }

        private static bool IsInOrNearWater(Vector3 pos)
        {
            var outZ = new OutputArgument();
            return Function.Call<bool>(Hash.GET_WATER_HEIGHT, pos.X, pos.Y, pos.Z, outZ);
        }

        public void ExitBoat()
        {
            var player = Game.Player.Character;
            if (player != null && player.Exists())
            {
                player.Detach();
                player.Task.ClearAll();
            }

            if (_currentBoat != null && _currentBoat.Exists())
            {
                _currentBoat.Detach();
                Function.Call(Hash.SET_ENTITY_DYNAMIC, _currentBoat.Handle, true);
                Function.Call(Hash.SET_ENTITY_COLLISION, _currentBoat.Handle, true, true);
                _currentBoat.IsPositionFrozen = false;
                _currentBoat.IsPersistent = true;
                Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, _currentBoat.Handle, true, true);
                Function.Call(Hash.SET_ENTITY_LOD_DIST, _currentBoat.Handle, 2500);

                // Place boat smoothly on the water surface (natural draft)
                Vector3 bPos = _currentBoat.Position;
                var outZ = new OutputArgument();
                if (Function.Call<bool>(Hash.GET_WATER_HEIGHT, bPos.X, bPos.Y, bPos.Z, outZ))
                {
                    float waterZ = outZ.GetResult<float>();
                    _currentBoat.Position = new Vector3(bPos.X, bPos.Y, waterZ - 0.18f);
                }

                if (!_idleBoats.Contains(_currentBoat))
                {
                    _idleBoats.Add(_currentBoat);
                }
            }

            if (_proxyVehicle != null && _proxyVehicle.Exists())
            {
                if (player != null && player.Exists() && player.CurrentVehicle == _proxyVehicle)
                {
                    player.Task.LeaveVehicle(_proxyVehicle, LeaveVehicleFlags.None);
                }
                _proxyVehicle.Delete();
                _proxyVehicle = null;
            }

            _isDriving = false;
            _currentBoat = null;
        }
    }
}
