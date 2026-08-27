using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;

namespace gta.Peds
{
    internal sealed class PoliceOfficerService
    {
        private static readonly Random Random = new Random();

        private static readonly string[] SearchContraband =
        {
            "Чисто: Документы проверены, запрещенных предметов нет.",
            "Чисто: Водительское удостоверение действительно, штрафов нет.",
            "Контрабанда: Изъят пакет с запрещенным веществом и $1,500 наличными.",
            "Нелегал: Изъят скрытый пистолет без лицензии и $3,200.",
            "Улика: Найден набор профессиональных отмычек и $850.",
            "Розыск: Личность подтверждена. Гражданин числился в ориентировках LSPD."
        };

        private enum DetainedPose
        {
            None,
            HandsUp,
            Kneeling,
            Escorting
        }

        private class DetainedPedInfo
        {
            public Ped Ped;
            public DetainedPose Pose;
            public int LastApplyTime;
            public Vehicle OriginalVehicle;
            public VehicleSeat OriginalSeat;
            public bool WasInVehicle;
            public bool IsInServiceVehicle;
        }

        private readonly RelationshipGroup _playerGroup;
        private readonly PedQueryService _pedQuery;
        private readonly List<Ped> _recruitedCops = new List<Ped>();
        private readonly Dictionary<int, DetainedPedInfo> _activeDetained = new Dictionary<int, DetainedPedInfo>();
        private static readonly HashSet<int> _bookedSuspects = new HashSet<int>();

        private class BookingTransfer
        {
            public Ped Officer;
            public Ped Suspect;
            public int StartGameTime;
            public Vector3 TargetCellPos;
        }

        private readonly List<BookingTransfer> _activeTransfers = new List<BookingTransfer>();

        private class RespondingBackup
        {
            public Vehicle Vehicle;
            public Ped Driver;
            public List<Ped> Crew = new List<Ped>();
            public int StartGameTime;
        }

        private readonly List<RespondingBackup> _respondingBackups = new List<RespondingBackup>();

        private struct StationChairSlot
        {
            public Vector3 Position;
            public float Heading;
            public StationChairSlot(Vector3 pos, float heading)
            {
                Position = pos;
                Heading = heading;
            }
        }

        private static readonly StationChairSlot[] MissionRowChairs =
        {
            new StationChairSlot(new Vector3(436.0f, -984.5f, 30.69f), 270.0f),
            new StationChairSlot(new Vector3(436.8f, -984.5f, 30.69f), 270.0f),
            new StationChairSlot(new Vector3(437.6f, -984.5f, 30.69f), 270.0f),
            new StationChairSlot(new Vector3(435.3f, -986.8f, 30.69f), 90.0f),
            new StationChairSlot(new Vector3(436.1f, -986.8f, 30.69f), 90.0f),
            new StationChairSlot(new Vector3(436.9f, -986.8f, 30.69f), 90.0f),
            new StationChairSlot(new Vector3(437.7f, -986.8f, 30.69f), 90.0f),
            new StationChairSlot(new Vector3(438.5f, -986.8f, 30.69f), 90.0f)
        };

        private bool _isFriendlyCopsEnabled = true;

        // Quick Command Menu State
        private bool _isQuickMenuOpen;
        private Ped _targetedPed;
        private int _quickMenuIndex;
        private List<QuickMenuItem> _currentQuickOptions = new List<QuickMenuItem>();

        private struct QuickMenuItem
        {
            public string Label;
            public Action Action;

            public QuickMenuItem(string label, Action action)
            {
                Label = label;
                Action = action;
            }
        }

        public PoliceOfficerService(RelationshipGroup playerGroup, PedQueryService pedQuery)
        {
            _playerGroup = playerGroup;
            _pedQuery = pedQuery;
            PreloadAnimDicts();
            ModLogger.Log("POLICE", "PoliceOfficerService initialized.");
        }

        private static void PreloadAnimDicts()
        {
            Function.Call(Hash.REQUEST_ANIM_DICT, "missminuteman_1ig_2");
            Function.Call(Hash.REQUEST_ANIM_DICT, "random@arrests@busted");
            Function.Call(Hash.REQUEST_ANIM_DICT, "mp_arresting");
        }

        public bool IsFriendlyCopsEnabled
        {
            get => _isFriendlyCopsEnabled;
            set
            {
                _isFriendlyCopsEnabled = value;
                ApplyFriendlyCopsState();
                ModLogger.Log("POLICE", $"Friendly cops toggle set to: {_isFriendlyCopsEnabled}");
            }
        }

        public bool IsQuickMenuOpen => _isQuickMenuOpen;

        public void Update()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            if (_isFriendlyCopsEnabled)
            {
                // Force permanent peace and alliance with police
                Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, Game.Player.Handle);
                Function.Call(Hash.SET_MAX_WANTED_LEVEL, 0);
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player.Handle, true);
                Function.Call(Hash.SET_DISPATCH_COPS_FOR_PLAYER, Game.Player.Handle, false);

                int copGroup = Function.Call<int>(Hash.GET_HASH_KEY, "COP");
                int playerGroup = _playerGroup.Hash;
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 0, copGroup, playerGroup);
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 0, playerGroup, copGroup);

                var nearbyCops = GTA.World.GetNearbyPeds(player.Position, 60.0f)
                    .Where(p => p.Exists() && !p.IsDead && IsPolicePed(p))
                    .ToArray();

                foreach (var cop in nearbyCops)
                {
                    if (cop.IsInCombatAgainst(player))
                    {
                        cop.Task.ClearAllImmediately();
                    }
                }
            }

            // Auto-release residual police handbrake when player is driving, unless player manually engaged handbrake
            if (player.IsInVehicle() && player.SeatIndex == VehicleSeat.Driver)
            {
                var currentVeh = player.CurrentVehicle;
                if (currentVeh != null && currentVeh.Exists() && !Vehicles.VehicleUpgradeService.IsVehicleHandbraked(currentVeh))
                {
                    Function.Call(Hash.SET_VEHICLE_HANDBRAKE, currentVeh.Handle, false);
                }
            }

            // Update recruited squad/cop companions (reusing companion vehicle entry & tactical formation)
            var playerVeh = player.IsInVehicle() ? player.CurrentVehicle : null;
            for (int i = _recruitedCops.Count - 1; i >= 0; i--)
            {
                var cop = _recruitedCops[i];
                if (cop == null || !cop.Exists() || cop.IsDead)
                {
                    _recruitedCops.RemoveAt(i);
                    continue;
                }

                if (player.IsInVehicle())
                {
                    CompanionService.TryEnterPlayerVehicle(cop, player, playerVeh);
                }
                else
                {
                    if (cop.IsInVehicle())
                    {
                        cop.Task.LeaveVehicle(LeaveVehicleFlags.None);
                    }
                    else if (!cop.IsInCombat)
                    {
                        var offset = new Vector3((i % 2 == 0 ? 1.8f : -1.8f), -1.8f - (i / 2) * 1.2f, 0f);
                        if (player.Position.DistanceTo(cop.Position) > 3.5f && !cop.IsWalking && !cop.IsRunning)
                        {
                            cop.Task.FollowToOffsetFromEntity(player, offset, ModSettings.CompanionFollowSpeed);
                        }
                    }
                }
            }

            // Update active cell escort transfers
            for (int i = _activeTransfers.Count - 1; i >= 0; i--)
            {
                var transfer = _activeTransfers[i];
                if (transfer.Suspect == null || !transfer.Suspect.Exists() || transfer.Suspect.IsDead)
                {
                    _activeTransfers.RemoveAt(i);
                    continue;
                }

                // Keep handcuffs animation on suspect
                if (!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, transfer.Suspect.Handle, "mp_arresting", "idle", 3))
                {
                    Function.Call(Hash.TASK_PLAY_ANIM, transfer.Suspect.Handle, "mp_arresting", "idle", 8.0f, -8.0f, -1, 49, 0, false, false, false);
                }

                bool reachedCell = transfer.Officer != null && transfer.Officer.Exists() &&
                                   transfer.Officer.Position.DistanceTo(transfer.TargetCellPos) < 3.0f;
                bool timedOut = Game.GameTime - transfer.StartGameTime > 22000;

                if (reachedCell || timedOut)
                {
                    transfer.Suspect.Task.ClearAllImmediately();
                    transfer.Suspect.MarkAsNoLongerNeeded();
                    transfer.Suspect.Delete();

                    if (transfer.Officer != null && transfer.Officer.Exists())
                    {
                        transfer.Officer.Task.ClearAllImmediately();
                        transfer.Officer.BlockPermanentEvents = false;
                        Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, transfer.Officer.Handle, false);
                        Function.Call(Hash.TASK_WANDER_STANDARD, transfer.Officer.Handle, 10.0f, 10);
                    }

                    _activeTransfers.RemoveAt(i);
                    ModLogger.Log("POLICE", "Cell transfer completed: suspect locked into jail cell.");
                }
            }

            // Update responding backup vehicles (drive to player with sirens before disembarking)
            for (int i = _respondingBackups.Count - 1; i >= 0; i--)
            {
                var backup = _respondingBackups[i];
                if (backup.Vehicle == null || !backup.Vehicle.Exists() || backup.Vehicle.IsDead ||
                    backup.Driver == null || !backup.Driver.Exists() || backup.Driver.IsDead)
                {
                    foreach (var member in backup.Crew.Where(m => m != null && m.Exists() && !m.IsDead))
                    {
                        if (!_recruitedCops.Contains(member))
                        {
                            _recruitedCops.Add(member);
                        }
                    }
                    _respondingBackups.RemoveAt(i);
                    continue;
                }

                float distToPlayer = backup.Vehicle.Position.DistanceTo(player.Position);
                bool arrived = distToPlayer <= 14.0f;
                bool stuckOrStoppedNear = distToPlayer <= 28.0f && backup.Vehicle.Speed < 1.5f && (Game.GameTime - backup.StartGameTime > 5000);
                bool timeout = Game.GameTime - backup.StartGameTime > 35000;

                if (arrived || stuckOrStoppedNear || timeout)
                {
                    backup.Vehicle.Speed = 0f;
                    Function.Call(Hash.SET_VEHICLE_BRAKE_LIGHTS, backup.Vehicle.Handle, true);
                    Function.Call(Hash.SET_VEHICLE_HANDBRAKE, backup.Vehicle.Handle, true);

                    foreach (var member in backup.Crew.Where(m => m != null && m.Exists() && !m.IsDead))
                    {
                        member.Task.ClearAll();
                        member.Task.LeaveVehicle(LeaveVehicleFlags.LeaveDoorOpen);
                        if (!_recruitedCops.Contains(member))
                        {
                            _recruitedCops.Add(member);
                        }
                    }

                    _respondingBackups.RemoveAt(i);
                    Notifier.Show("~b~[Подкрепление]~s~ Экипаж прибыл к вам и занял боевые позиции!");
                    ModLogger.Log("POLICE", "Backup vehicle arrived and crew disembarked.");
                }
            }

            // Continuous Pose Sentinel: Auto-recovers pose after pushes, bumps, tackles or ragdolls
            int currentTime = Game.GameTime;
            var handles = _activeDetained.Keys.ToArray();
            foreach (var handle in handles)
            {
                if (!_activeDetained.TryGetValue(handle, out var info)) continue;
                var ped = info.Ped;
                if (ped == null || !ped.Exists() || ped.IsDead)
                {
                    _activeDetained.Remove(handle);
                    continue;
                }

                if (info.Pose == DetainedPose.None) continue;

                // If ped is in ragdoll physics or currently getting up, wait until they stand on their feet
                if (ped.IsRagdoll || ped.IsGettingUp)
                {
                    continue;
                }

                // 600ms cooldown to avoid re-triggering during animation blend-in
                if (currentTime - info.LastApplyTime < 600)
                {
                    continue;
                }

                bool needsRestore = false;

                switch (info.Pose)
                {
                    case DetainedPose.HandsUp:
                        if (!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, ped.Handle, "missminuteman_1ig_2", "handsup_base", 3))
                        {
                            needsRestore = true;
                        }
                        break;

                    case DetainedPose.Kneeling:
                        if (!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, ped.Handle, "random@arrests@busted", "idle_a", 3))
                        {
                            needsRestore = true;
                        }
                        break;

                    case DetainedPose.Escorting:
                        if (player.Position.DistanceTo(ped.Position) > 2.5f && !ped.IsWalking && !ped.IsRunning)
                        {
                            needsRestore = true;
                        }
                        break;
                }

                if (needsRestore)
                {
                    info.LastApplyTime = currentTime;
                    ModLogger.Log("POLICE", $"Ped {ped.Handle} interrupted/bumped from pose {info.Pose}. Auto-restoring command.");
                    ApplyPose(ped, info.Pose);
                }
            }

            // If targeted ped is dead or too far, close quick menu
            if (_isQuickMenuOpen)
            {
                if (_targetedPed == null || !_targetedPed.Exists() || _targetedPed.IsDead || player.Position.DistanceTo(_targetedPed.Position) > 35.0f)
                {
                    ModLogger.Log("POLICE", "Target ped lost or dead, closing quick menu.");
                    CloseQuickMenu();
                }
            }
        }

        public void ApplyFriendlyCopsState()
        {
            int copGroup = Function.Call<int>(Hash.GET_HASH_KEY, "COP");
            int playerGroup = _playerGroup.Hash;
            if (_isFriendlyCopsEnabled)
            {
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 0, copGroup, playerGroup);
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 0, playerGroup, copGroup);
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player.Handle, true);
                Function.Call(Hash.SET_DISPATCH_COPS_FOR_PLAYER, Game.Player.Handle, false);
            }
            else
            {
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 3, copGroup, playerGroup);
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 3, playerGroup, copGroup);
                Function.Call(Hash.SET_MAX_WANTED_LEVEL, 5);
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player.Handle, false);
                Function.Call(Hash.SET_DISPATCH_COPS_FOR_PLAYER, Game.Player.Handle, true);
                CloseQuickMenu();
            }
        }

        public void HandleQuickCommandKey()
        {
            if (!_isFriendlyCopsEnabled) return;

            Ped player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            if (_isQuickMenuOpen)
            {
                ExecuteQuickMenuItem();
                return;
            }

            // Find target ped aimed at or in front of player
            Ped target = _pedQuery.GetAimedPed(player);
            if (target == null || !target.Exists() || target.IsDead)
            {
                target = GetLookingAtPed(player, 25.0f);
            }

            if (target == null || !target.Exists() || target.IsDead)
            {
                Notifier.Show("~y~[LSPD]~s~ Наведите прицел или подойдите к гражданину / офицеру");
                return;
            }

            if (_bookedSuspects.Contains(target.Handle))
            {
                Notifier.Show("~b~[LSPD]~s~ Этот гражданин уже оформлен и находится под арестом");
                return;
            }

            _targetedPed = target;
            _targetedPed.BlockPermanentEvents = true;
            Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, _targetedPed.Handle, true);
            Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, _targetedPed.Handle, 0, false);

            if (!_activeDetained.TryGetValue(_targetedPed.Handle, out var existingInfo))
            {
                Vehicle origVeh = null;
                VehicleSeat origSeat = VehicleSeat.None;
                bool wasInVehicle = _targetedPed.IsInVehicle();

                if (wasInVehicle)
                {
                    var veh = _targetedPed.CurrentVehicle;
                    // ONLY store vehicle if it's NOT the player's current vehicle
                    if (veh != null && veh.Exists() && (!player.IsInVehicle() || veh != player.CurrentVehicle))
                    {
                        origVeh = veh;
                        origSeat = _targetedPed.SeatIndex;
                        origVeh.IsEngineRunning = false;
                        origVeh.Speed = 0f;
                        Function.Call(Hash.SET_VEHICLE_HANDBRAKE, origVeh.Handle, true);
                    }
                    else
                    {
                        wasInVehicle = false;
                    }
                }

                existingInfo = new DetainedPedInfo
                {
                    Ped = _targetedPed,
                    Pose = DetainedPose.None,
                    LastApplyTime = Game.GameTime,
                    OriginalVehicle = origVeh,
                    OriginalSeat = origSeat,
                    WasInVehicle = wasInVehicle,
                    IsInServiceVehicle = false
                };
                _activeDetained[_targetedPed.Handle] = existingInfo;

                ModLogger.Log("POLICE", $"Target ped acquired (NEW): Handle={_targetedPed.Handle}, Model={_targetedPed.Model.Hash}, InVehicle={wasInVehicle}, VehicleHandle={origVeh?.Handle ?? 0}, Seat={origSeat}, IsCop={IsPolicePed(_targetedPed)}");
            }
            else
            {
                existingInfo.Ped = _targetedPed;
                if (_targetedPed.IsInVehicle() && !existingInfo.WasInVehicle && !existingInfo.IsInServiceVehicle)
                {
                    var veh = _targetedPed.CurrentVehicle;
                    if (veh != null && veh.Exists() && (!player.IsInVehicle() || veh != player.CurrentVehicle))
                    {
                        existingInfo.OriginalVehicle = veh;
                        existingInfo.OriginalSeat = _targetedPed.SeatIndex;
                        existingInfo.WasInVehicle = true;
                    }
                }
                ModLogger.Log("POLICE", $"Target ped acquired (EXISTING): Handle={_targetedPed.Handle}, WasInVehicle={existingInfo.WasInVehicle}, IsInServiceVehicle={existingInfo.IsInServiceVehicle}, VehicleHandle={existingInfo.OriginalVehicle?.Handle ?? 0}, Seat={existingInfo.OriginalSeat}");
            }

            OpenQuickMenuForTarget();
        }

        private void OpenQuickMenuForTarget()
        {
            _currentQuickOptions.Clear();
            _quickMenuIndex = 0;

            Ped player = Game.Player.Character;
            bool isCop = IsPolicePed(_targetedPed);
            bool isInVehicle = _targetedPed.IsInVehicle();

            PlayPoliceSpeech(player, "CHALLENGE_THREAT");

            if (isCop)
            {
                var nearbySuspects = _activeDetained.Values
                    .Where(info => info.Ped != null && info.Ped.Exists() && !info.Ped.IsDead &&
                                  !_bookedSuspects.Contains(info.Ped.Handle) &&
                                  !IsPolicePed(info.Ped) &&
                                  info.Ped != player &&
                                  (info.Pose == DetainedPose.Escorting ||
                                   (info.Pose != DetainedPose.None && info.Ped.Position.DistanceTo(player.Position) <= 6.0f)))
                    .Select(info => info.Ped)
                    .Distinct()
                    .ToList();

                if (nearbySuspects.Count > 0)
                {
                    _currentQuickOptions.Add(new QuickMenuItem($"Сдать задержанного в участок (+$3,500)", () => BookSuspectsAtStation(player, _targetedPed, nearbySuspects)));
                }

                _currentQuickOptions.Add(new QuickMenuItem("Присоединиться к патрулю (Вербовать)", () => RecruitCop(player, _targetedPed)));
                _currentQuickOptions.Add(new QuickMenuItem("Штурмовать вражескую цель", () => OrderAttackAimedTarget(player)));
                _currentQuickOptions.Add(new QuickMenuItem("Следовать за мной", () =>
                {
                    _targetedPed.Task.ClearAllImmediately();
                    _targetedPed.Task.FollowToOffsetFromEntity(player, new Vector3(1.5f, -1.5f, 0f), 2.0f, -1, 1.5f, true);
                    ModLogger.Log("POLICE", $"Cop {_targetedPed.Handle} following player.");
                    Notifier.Show("~b~Офицер LSPD:~s~ «Следую за вами, сэр!»");
                }));
                _currentQuickOptions.Add(new QuickMenuItem("Отпустить со службы", () =>
                {
                    _recruitedCops.Remove(_targetedPed);
                    _targetedPed.Task.ClearAllImmediately();
                    _targetedPed.BlockPermanentEvents = false;
                    Function.Call(Hash.TASK_WANDER_STANDARD, _targetedPed.Handle, 10.0f, 10);
                    ModLogger.Log("POLICE", $"Cop {_targetedPed.Handle} released from squad.");
                    Notifier.Show("~g~[LSPD] Офицер вернулся к стандартному патрулированию~s~");
                }));
            }
            else if (isInVehicle)
            {
                _currentQuickOptions.Add(new QuickMenuItem("Приказ: Заглушить мотор и выйти!", () =>
                {
                    var veh = _targetedPed.CurrentVehicle;
                    if (veh != null && veh.Exists())
                    {
                        veh.IsEngineRunning = false;
                        veh.Speed = 0f;
                        Function.Call(Hash.SET_VEHICLE_HANDBRAKE, veh.Handle, true);
                    }
                    _targetedPed.Task.LeaveVehicle(LeaveVehicleFlags.None);
                    SetDetainedPedState(_targetedPed, DetainedPose.HandsUp);
                    ModLogger.Log("POLICE", $"Driver {_targetedPed.Handle} ordered to leave vehicle.");
                    Notifier.Show("~r~[LSPD] «Водитель, заглушите мотор и выходите с поднятыми руками!»~s~");
                }));
                _currentQuickOptions.Add(new QuickMenuItem("Отпустить водителя (Свободен)", () => ReleaseTargetedPed()));
            }
            else
            {
                // Civilian on foot
                _currentQuickOptions.Add(new QuickMenuItem("1. Стоять! Руки вверх!", () =>
                {
                    SetDetainedPedState(_targetedPed, DetainedPose.HandsUp);
                    ModLogger.Log("POLICE", $"Ped {_targetedPed.Handle}: Command HandsUp assigned.");
                    Notifier.Show("~r~[LSPD] «Стоять на месте! Руки вверх!»~s~");
                }));

                _currentQuickOptions.Add(new QuickMenuItem("2. На колени лицом в пол!", () =>
                {
                    SetDetainedPedState(_targetedPed, DetainedPose.Kneeling);
                    ModLogger.Log("POLICE", $"Ped {_targetedPed.Handle}: Command Kneeling assigned.");
                    Notifier.Show("~r~[LSPD] «На колени лицом в пол!»~s~");
                }));

                _currentQuickOptions.Add(new QuickMenuItem("3. Провести обыск и досмотр", () =>
                {
                    const string friskDict = "mp_arresting";
                    if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, friskDict))
                    {
                        Function.Call(Hash.REQUEST_ANIM_DICT, friskDict);
                    }
                    player.Task.PlayAnimation(friskDict, "a_uncuff", 4.0f, -4.0f, 2500, (AnimationFlags)0, 0f);

                    string report = SearchContraband[Random.Next(SearchContraband.Length)];
                    ModLogger.Log("POLICE", $"Ped {_targetedPed.Handle} searched. Report: {report}");
                    Notifier.Show($"~b~[ПРОТОКОЛ ОБЫСКА]~s~\n{report}");
                }));

                _currentQuickOptions.Add(new QuickMenuItem("4. Вести за собой (Конвоировать)", () =>
                {
                    SetDetainedPedState(_targetedPed, DetainedPose.Escorting);
                    ModLogger.Log("POLICE", $"Ped {_targetedPed.Handle} escorting behind player.");
                    Notifier.Show("~y~[LSPD] Задержанный следует за вами в конвое~s~");
                }));

                _currentQuickOptions.Add(new QuickMenuItem("5. Посадить в служебный автомобиль", () =>
                {
                    var nearestVeh = GTA.World.GetClosestVehicle(player.Position, 20.0f);
                    if (nearestVeh != null && nearestVeh.Exists())
                    {
                        if (!_activeDetained.TryGetValue(_targetedPed.Handle, out var info))
                        {
                            info = new DetainedPedInfo { Ped = _targetedPed };
                            _activeDetained[_targetedPed.Handle] = info;
                        }

                        info.Pose = DetainedPose.None;
                        info.IsInServiceVehicle = true;
                        info.OriginalVehicle = null;
                        info.WasInVehicle = false;

                        _targetedPed.BlockPermanentEvents = true;
                        _targetedPed.Task.EnterVehicle(nearestVeh, VehicleSeat.RightRear);
                        ModLogger.Log("POLICE", $"Ped {_targetedPed.Handle} placed into service vehicle {nearestVeh.Handle} as prisoner.");
                        Notifier.Show("~b~[LSPD] Подозреваемый помещен в служебный транспорт~s~");
                    }
                    else
                    {
                        Notifier.Show("Поблизости нет автомобиля");
                    }
                }));

                _currentQuickOptions.Add(new QuickMenuItem("6. Отпустить гражданина (Свободен)", () => ReleaseTargetedPed()));
            }

            _isQuickMenuOpen = true;
            ModLogger.Log("POLICE", $"Quick Menu opened with {_currentQuickOptions.Count} options.");
        }

        private void SetDetainedPedState(Ped ped, DetainedPose pose)
        {
            if (ped == null || !ped.Exists() || ped.IsDead) return;

            if (!_activeDetained.TryGetValue(ped.Handle, out var info))
            {
                info = new DetainedPedInfo
                {
                    Ped = ped,
                    WasInVehicle = false,
                    OriginalVehicle = null,
                    OriginalSeat = VehicleSeat.None
                };
            }

            info.Pose = pose;
            info.LastApplyTime = Game.GameTime;
            _activeDetained[ped.Handle] = info;

            ApplyPose(ped, pose);
        }

        private static void ApplyPose(Ped ped, DetainedPose pose)
        {
            if (ped == null || !ped.Exists() || ped.IsDead) return;

            ped.Task.ClearAllImmediately();
            ped.BlockPermanentEvents = true;
            Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, true);
            Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped.Handle, 0, false);

            switch (pose)
            {
                case DetainedPose.HandsUp:
                    const string dictHands = "missminuteman_1ig_2";
                    if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, dictHands))
                    {
                        Function.Call(Hash.REQUEST_ANIM_DICT, dictHands);
                    }
                    Function.Call(Hash.TASK_PLAY_ANIM, ped.Handle, dictHands, "handsup_base", 8.0f, -8.0f, -1, 1, 0, false, false, false);
                    break;

                case DetainedPose.Kneeling:
                    const string dictKneel = "random@arrests@busted";
                    if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, dictKneel))
                    {
                        Function.Call(Hash.REQUEST_ANIM_DICT, dictKneel);
                    }
                    Function.Call(Hash.TASK_PLAY_ANIM, ped.Handle, dictKneel, "idle_a", 8.0f, -8.0f, -1, 1, 0, false, false, false);
                    break;

                case DetainedPose.Escorting:
                    ped.Task.FollowToOffsetFromEntity(Game.Player.Character, new Vector3(0f, -1.8f, 0f), 1.5f, -1, 1.2f, true);
                    break;
            }
        }

        public void Draw()
        {
            if (!_isQuickMenuOpen || _targetedPed == null || !_targetedPed.Exists()) return;

            string targetType = IsPolicePed(_targetedPed) ? "~b~Офицер LSPD~s~" : (_targetedPed.IsInVehicle() ? "~y~Водитель ТС~s~" : "~g~Гражданский~s~");
            string text = $"== Приказы Офицера: {targetType} ==\n\n";

            for (int i = 0; i < _currentQuickOptions.Count; i++)
            {
                if (i == _quickMenuIndex)
                {
                    text += $"~b~> {i + 1}. {_currentQuickOptions[i].Label}~s~\n";
                }
                else
                {
                    text += $"  {i + 1}. {_currentQuickOptions[i].Label}\n";
                }
            }

            text += "\n~g~[8/2/Вверх/Вниз]~s~ Навигация  ~g~[E/5/Enter]~s~ Приказать  ~g~[0/Esc]~s~ Закрыть";

            MenuPanelRenderer.Draw(text, new PointF(10, 10), 0.42f);
        }

        public void ProcessKey(KeyEventArgs e)
        {
            if (!_isQuickMenuOpen) return;

            switch (e.KeyCode)
            {
                case Keys.Escape:
                case Keys.Back:
                case Keys.NumPad0:
                    ModLogger.Log("POLICE", "Quick menu closed via cancel key.");
                    CloseQuickMenu();
                    break;

                case Keys.NumPad8:
                case Keys.Up:
                    _quickMenuIndex = _quickMenuIndex <= 0 ? _currentQuickOptions.Count - 1 : _quickMenuIndex - 1;
                    break;

                case Keys.NumPad2:
                case Keys.Down:
                    _quickMenuIndex = _quickMenuIndex >= _currentQuickOptions.Count - 1 ? 0 : _quickMenuIndex + 1;
                    break;

                case Keys.E:
                case Keys.NumPad5:
                case Keys.Enter:
                    ExecuteQuickMenuItem();
                    break;

                case Keys.D1: TrySelectIndex(0); break;
                case Keys.D2: TrySelectIndex(1); break;
                case Keys.D3: TrySelectIndex(2); break;
                case Keys.D4: TrySelectIndex(3); break;
                case Keys.D5: TrySelectIndex(4); break;
                case Keys.D6: TrySelectIndex(5); break;
            }
        }

        private void TrySelectIndex(int index)
        {
            if (index >= 0 && index < _currentQuickOptions.Count)
            {
                _quickMenuIndex = index;
                ExecuteQuickMenuItem();
            }
        }

        private void ExecuteQuickMenuItem()
        {
            if (_quickMenuIndex >= 0 && _quickMenuIndex < _currentQuickOptions.Count)
            {
                ModLogger.Log("POLICE", $"Executing option #{_quickMenuIndex + 1}: {_currentQuickOptions[_quickMenuIndex].Label}");
                _currentQuickOptions[_quickMenuIndex].Action?.Invoke();
            }
        }

        private void ReleaseTargetedPed()
        {
            if (_targetedPed != null && _targetedPed.Exists() && !_targetedPed.IsDead)
            {
                ReleaseDetainedPed(_targetedPed);
            }
            CloseQuickMenu();
        }

        private void ReleaseDetainedPed(Ped ped)
        {
            if (ped == null || !ped.Exists() || ped.IsDead) return;

            Ped player = Game.Player.Character;
            bool isTracked = _activeDetained.TryGetValue(ped.Handle, out var info);

            // If ped is currently in a vehicle and was placed there as a prisoner / in service vehicle
            if (ped.IsInVehicle())
            {
                var currentVeh = ped.CurrentVehicle;
                if (info != null && (info.IsInServiceVehicle || info.OriginalVehicle == null || info.OriginalVehicle != currentVeh))
                {
                    ped.BlockPermanentEvents = false;
                    ped.Task.ClearAllImmediately();
                    ped.Task.LeaveVehicle(LeaveVehicleFlags.None);
                    Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, false);
                    Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped.Handle, 0, false);
                    Function.Call(Hash.SET_PED_MAX_MOVE_BLEND_RATIO, ped.Handle, 1.0f);
                    Function.Call(Hash.TASK_WANDER_STANDARD, ped.Handle, 10.0f, 10);
                    ModLogger.Log("POLICE", $"Ped {ped.Handle} released from service vehicle: exiting and walking away.");
                    Notifier.Show("~g~[LSPD] «Вы свободны, выходите из служебного транспорта.»~s~");
                    _activeDetained.Remove(ped.Handle);
                    return;
                }
            }

            if (isTracked && info.WasInVehicle && !info.IsInServiceVehicle && info.OriginalVehicle != null && info.OriginalVehicle.Exists() && !info.OriginalVehicle.IsDead && info.OriginalVehicle.IsDriveable && (!player.IsInVehicle() || info.OriginalVehicle != player.CurrentVehicle))
            {
                // Release handbrake on their vehicle
                Function.Call(Hash.SET_VEHICLE_HANDBRAKE, info.OriginalVehicle.Handle, false);
                
                ped.BlockPermanentEvents = false;
                ped.Task.ClearAllImmediately();
                Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, false);
                Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped.Handle, 0, false);

                if (ped.IsInVehicle())
                {
                    // If still inside car, start engine and drive away calmly
                    info.OriginalVehicle.IsEngineRunning = true;
                    Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, ped.Handle, info.OriginalVehicle.Handle, 14.0f, 786603);
                    ModLogger.Log("POLICE", $"Driver {ped.Handle} released inside vehicle {info.OriginalVehicle.Handle}: driving away.");
                    Notifier.Show("~g~[LSPD] «Вы свободны. Можете продолжить движение.»~s~");
                }
                else
                {
                    // If out of vehicle, enter vehicle and drive away
                    VehicleSeat targetSeat = info.OriginalSeat != VehicleSeat.None ? info.OriginalSeat : VehicleSeat.Driver;
                    ped.Task.EnterVehicle(info.OriginalVehicle, targetSeat, -1, 1.5f, EnterVehicleFlags.None);
                    if (targetSeat == VehicleSeat.Driver)
                    {
                        Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, ped.Handle, info.OriginalVehicle.Handle, 14.0f, 786603);
                        ModLogger.Log("POLICE", $"Driver {ped.Handle} released: returning to vehicle {info.OriginalVehicle.Handle} to drive away.");
                        Notifier.Show("~g~[LSPD] «Вы свободны. Можете вернуться в автомобиль и продолжить движение.»~s~");
                    }
                    else
                    {
                        ModLogger.Log("POLICE", $"Passenger {ped.Handle} released: returning to vehicle {info.OriginalVehicle.Handle} seat {targetSeat}.");
                        Notifier.Show("~g~[LSPD] «Вы свободны. Можете вернуться в автомобиль.»~s~");
                    }
                }
            }
            else
            {
                // Pedestrian walking away peacefully
                ped.BlockPermanentEvents = false;
                ped.Task.ClearAllImmediately();
                Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, false);
                Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped.Handle, 0, false);
                Function.Call(Hash.SET_PED_MAX_MOVE_BLEND_RATIO, ped.Handle, 1.0f);
                Function.Call(Hash.TASK_WANDER_STANDARD, ped.Handle, 10.0f, 10);
                ModLogger.Log("POLICE", $"Ped {ped.Handle} released peacefully on foot with TASK_WANDER_STANDARD.");
                Notifier.Show("~g~[LSPD] «Вы свободны, больше не нарушайте порядок.»~s~");
            }

            _activeDetained.Remove(ped.Handle);
        }

        public void CloseQuickMenu()
        {
            _isQuickMenuOpen = false;
            _currentQuickOptions.Clear();
            _targetedPed = null;
        }

        private void RecruitCop(Ped player, Ped cop)
        {
            if (_recruitedCops.Contains(cop))
            {
                Notifier.Show("~b~Офицер LSPD:~s~ «Так точно, сэр, прикрываю вас!»");
                CloseQuickMenu();
                return;
            }

            cop.Task.ClearAllImmediately();
            cop.RelationshipGroup = _playerGroup;
            cop.NeverLeavesGroup = true;
            cop.BlockPermanentEvents = true;
            cop.Accuracy = 85;
            cop.Armor = 100;
            cop.MaxHealth = 200;
            cop.Health = 200;

            cop.Weapons.Give(WeaponHash.CombatPistol, 999, true, true);
            cop.Weapons.Give(WeaponHash.CarbineRifle, 999, false, true);

            Function.Call(Hash.SET_PED_AS_GROUP_MEMBER, cop.Handle, Game.Player.Character.RelationshipGroup.Hash);
            cop.Task.FollowToOffsetFromEntity(player, new Vector3((_recruitedCops.Count % 2 == 0 ? 1.5f : -1.5f), -1.5f, 0f), 2.0f, -1, 1.5f, true);

            _recruitedCops.Add(cop);

            PlayPoliceSpeech(player, "GENERIC_HI");
            ModLogger.Log("POLICE", $"Cop {cop.Handle} recruited to patrol group.");
            Notifier.Show("~g~[LSPD] Офицер полиции присоединился к вашему патрулю!~s~");
            CloseQuickMenu();
        }

        private void BookSuspectsAtStation(Ped player, Ped deskOfficer, List<Ped> suspects)
        {
            if (suspects == null || suspects.Count == 0) return;

            int totalReward = 3500 * suspects.Count;
            Game.Player.Money += totalReward;

            PlayPoliceSpeech(deskOfficer, "GENERIC_THANKS");

            Vector3 missionRowCenter = new Vector3(441.0f, -982.0f, 30.69f);
            bool isNearMissionRow = player.Position.DistanceTo(missionRowCenter) < 65.0f;
            Vector3 targetCellPos = isNearMissionRow ? new Vector3(459.5f, -994.0f, 24.91f) : deskOfficer.Position + deskOfficer.ForwardVector * 18.0f;

            deskOfficer.BlockPermanentEvents = true;
            Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, deskOfficer.Handle, true);
            deskOfficer.Task.ClearAllImmediately();
            deskOfficer.Task.FollowPointRoute(1.2f, targetCellPos);

            for (int i = 0; i < suspects.Count; i++)
            {
                var suspect = suspects[i];
                if (suspect == null || !suspect.Exists() || suspect.IsDead) continue;

                _bookedSuspects.Add(suspect.Handle);
                _activeDetained.Remove(suspect.Handle);

                suspect.Task.ClearAllImmediately();
                suspect.BlockPermanentEvents = true;
                Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, suspect.Handle, true);
                Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, suspect.Handle, 0, false);

                // Suspect follows officer in escort formation
                suspect.Task.FollowToOffsetFromEntity(deskOfficer, new Vector3(0f, -1.1f - (i * 0.9f), 0f), 1.2f, -1, 0.6f, true);
                Function.Call(Hash.TASK_PLAY_ANIM, suspect.Handle, "mp_arresting", "idle", 8.0f, -8.0f, -1, 49, 0, false, false, false);

                _activeTransfers.Add(new BookingTransfer
                {
                    Officer = deskOfficer,
                    Suspect = suspect,
                    StartGameTime = Game.GameTime,
                    TargetCellPos = targetCellPos
                });

                ModLogger.Log("POLICE", $"Suspect {suspect.Handle} transferred to officer {deskOfficer.Handle} for cell escort. Reward: +$3,500");
            }

            Notifier.Show($"~b~[LSPD Front Desk]~s~ «Задержанный принят! Офицер конвоирует его в камеру изолятора.»\n~g~Премия за арест: +${totalReward:N0}~s~");
            CloseQuickMenu();
        }

        private static Vector3 GetRoadBackupSpawnPosition(Ped player, out float heading)
        {
            heading = player.Heading;
            Vector3 playerPos = player.Position;
            Random rnd = new Random();

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = (float)(rnd.NextDouble() * Math.PI * 2);
                float dist = (float)rnd.Next(65, 115);
                Vector3 samplePos = playerPos + new Vector3((float)Math.Cos(angle) * dist, (float)Math.Sin(angle) * dist, 0f);

                Vector3 streetPos = GTA.World.GetNextPositionOnStreet(samplePos);
                if (streetPos != Vector3.Zero && streetPos.DistanceTo(playerPos) >= 35.0f)
                {
                    Vector3 dir = (playerPos - streetPos).Normalized;
                    heading = (float)(Math.Atan2(dir.X, -dir.Y) * (180.0 / Math.PI));
                    return streetPos;
                }
            }

            Vector3 fallback = playerPos - player.ForwardVector * 60.0f;
            return fallback;
        }

        public void SpawnSwatVan(Ped player)
        {
            var spawnPos = GetRoadBackupSpawnPosition(player, out float spawnHeading);
            var carModel = new Model(VehicleHash.Riot);
            var swatModel = new Model(PedHash.Swat01SMY);

            carModel.Request(1000);
            swatModel.Request(1000);

            if (!carModel.IsLoaded || !swatModel.IsLoaded)
            {
                Notifier.Show("Не удалось вызвать спецназ NOOSE");
                return;
            }

            try
            {
                Vehicle riot = GTA.World.CreateVehicle(carModel, spawnPos, spawnHeading);
                if (riot == null || !riot.Exists()) return;

                riot.IsSirenActive = true;
                riot.Speed = 15.0f;

                Ped driver = null;
                var crewList = new List<Ped>();
                for (int seat = -1; seat < 3; seat++)
                {
                    Ped swat = riot.CreatePedOnSeat((VehicleSeat)seat, swatModel);
                    if (swat == null || !swat.Exists()) continue;

                    swat.RelationshipGroup = _playerGroup;
                    swat.Armor = 100;
                    swat.Health = 200;
                    swat.Accuracy = 90;
                    swat.BlockPermanentEvents = true;
                    swat.Weapons.Give(WeaponHash.CarbineRifle, 999, true, true);
                    swat.Weapons.Give(WeaponHash.PumpShotgun, 999, false, true);
                    crewList.Add(swat);

                    if (seat == -1)
                    {
                        driver = swat;
                    }
                }

                if (driver != null && driver.Exists())
                {
                    Function.Call(Hash.SET_DRIVER_ABILITY, driver.Handle, 1.0f);
                    Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, driver.Handle, 1.0f);
                    Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, driver.Handle, riot.Handle, player.Position.X, player.Position.Y, player.Position.Z, 30.0f, 1, riot.Model.Hash, 786475, 7.0f, true);
                }

                _respondingBackups.Add(new RespondingBackup
                {
                    Vehicle = riot,
                    Driver = driver,
                    Crew = crewList,
                    StartGameTime = Game.GameTime
                });

                ModLogger.Log("POLICE", "NOOSE SWAT backup dispatched on road.");
                Notifier.Show("~b~[NOOSE SWAT] Броневик спецназа с сиренами спешит к вашей позиции!~s~");
            }
            finally
            {
                carModel.MarkAsNoLongerNeeded();
                swatModel.MarkAsNoLongerNeeded();
            }
        }

        public void SpawnPoliceInterceptor(Ped player)
        {
            var spawnPos = GetRoadBackupSpawnPosition(player, out float spawnHeading);
            var carModel = new Model(VehicleHash.Police3);
            var copModel = new Model(PedHash.Cop01SMY);

            carModel.Request(1000);
            copModel.Request(1000);

            if (!carModel.IsLoaded || !copModel.IsLoaded)
            {
                Notifier.Show("Не удалось вызвать экипаж LSPD");
                return;
            }

            try
            {
                Vehicle cruiser = GTA.World.CreateVehicle(carModel, spawnPos, spawnHeading);
                if (cruiser == null || !cruiser.Exists()) return;

                cruiser.IsSirenActive = true;
                cruiser.Speed = 18.0f;

                Ped cop1 = cruiser.CreatePedOnSeat(VehicleSeat.Driver, copModel);
                Ped cop2 = cruiser.CreatePedOnSeat(VehicleSeat.Passenger, copModel);

                var crewList = new List<Ped>();
                foreach (var cop in new[] { cop1, cop2 }.Where(c => c != null && c.Exists()))
                {
                    cop.RelationshipGroup = _playerGroup;
                    cop.Armor = 100;
                    cop.Health = 200;
                    cop.BlockPermanentEvents = true;
                    cop.Weapons.Give(WeaponHash.CombatPistol, 999, true, true);
                    cop.Weapons.Give(WeaponHash.CarbineRifle, 999, false, true);
                    crewList.Add(cop);
                }

                if (cop1 != null && cop1.Exists())
                {
                    Function.Call(Hash.SET_DRIVER_ABILITY, cop1.Handle, 1.0f);
                    Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, cop1.Handle, 1.0f);
                    Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, cop1.Handle, cruiser.Handle, player.Position.X, player.Position.Y, player.Position.Z, 32.0f, 1, cruiser.Model.Hash, 786475, 6.0f, true);
                }

                _respondingBackups.Add(new RespondingBackup
                {
                    Vehicle = cruiser,
                    Driver = cop1,
                    Crew = crewList,
                    StartGameTime = Game.GameTime
                });

                ModLogger.Log("POLICE", "LSPD Interceptor backup dispatched on road.");
                Notifier.Show("~b~[LSPD] Патрульный перехватчик с сиренами мчится на ваш вызов!~s~");
            }
            finally
            {
                carModel.MarkAsNoLongerNeeded();
                copModel.MarkAsNoLongerNeeded();
            }
        }

        public void SpawnPoliceHelicopter(Ped player)
        {
            var spawnPos = player.Position + (player.ForwardVector * -1f + player.RightVector * 0.5f) * 120.0f + Vector3.WorldUp * 65.0f;
            var heliModel = new Model(VehicleHash.Polmav);
            var copModel = new Model(PedHash.Cop01SMY);

            heliModel.Request(1000);
            copModel.Request(1000);

            if (!heliModel.IsLoaded || !copModel.IsLoaded)
            {
                Notifier.Show("Не удалось вызвать полицейский вертолет");
                return;
            }

            try
            {
                Vehicle heli = GTA.World.CreateVehicle(heliModel, spawnPos, player.Heading);
                if (heli == null || !heli.Exists()) return;

                heli.IsEngineRunning = true;
                heli.HeliBladesSpeed = 1.0f;

                Ped pilot = heli.CreatePedOnSeat(VehicleSeat.Driver, copModel);
                if (pilot != null && pilot.Exists())
                {
                    pilot.RelationshipGroup = _playerGroup;
                    pilot.Health = 200;
                    pilot.BlockPermanentEvents = true;
                    Function.Call(Hash.TASK_HELI_MISSION, pilot.Handle, heli.Handle, 0, player.Handle, 0f, 0f, 30f, 4, 25f, 5f, -1f, 30, 40, 500f, 0);
                }

                ModLogger.Log("POLICE", "LSPD Helicopter air support spawned.");
                Notifier.Show("~b~[Air Support] Полицейский вертолет Maverick вылетел в ваш сектор для прикрытия!~s~");
            }
            finally
            {
                heliModel.MarkAsNoLongerNeeded();
                copModel.MarkAsNoLongerNeeded();
            }
        }

        public void OrderAttackAimedTarget(Ped player)
        {
            Ped target = _pedQuery.GetAimedPed(player);
            if (target == null || !target.Exists() || target.IsDead)
            {
                target = GetLookingAtPed(player, 40.0f);
            }

            if (target == null || !target.Exists() || target.IsDead)
            {
                Notifier.Show("Прицельтесь во вражескую цель, чтобы отдать приказ атаки");
                return;
            }

            foreach (var cop in _recruitedCops.Where(c => c != null && c.Exists() && !c.IsDead))
            {
                cop.Task.Combat(target, TaskCombatFlags.None, TaskThreatResponseFlags.None);
            }

            PlayPoliceSpeech(player, "WAR_CRY");
            ModLogger.Log("POLICE", $"Attack order issued on target {target.Handle}.");
            Notifier.Show($"~r~[LSPD] Приказ отдан: Все офицеры штурмуют цель #{target.Handle}!~s~");
            CloseQuickMenu();
        }

        public void ReleaseAllDetained()
        {
            var handles = _activeDetained.Keys.ToArray();
            foreach (var handle in handles)
            {
                if (_activeDetained.TryGetValue(handle, out var info) && info.Ped != null && info.Ped.Exists() && !info.Ped.IsDead)
                {
                    ReleaseDetainedPed(info.Ped);
                }
            }
            _activeDetained.Clear();
            CloseQuickMenu();
            ModLogger.Log("POLICE", "Release all detained executed.");
            Notifier.Show("~g~[LSPD] Все задержанные граждане отпущены~s~");
        }

        public void WearPoliceUniform(Ped player)
        {
            var copModel = new Model(PedHash.Cop01SMY);
            copModel.Request(1000);
            if (copModel.IsLoaded)
            {
                Game.Player.ChangeModel(copModel);
                copModel.MarkAsNoLongerNeeded();
                ModLogger.Log("POLICE", "Police uniform applied.");
                Notifier.Show("~g~[LSPD] Надет комплект униформы офицера полиции Лос-Сантоса~s~");
            }
        }

        private static bool IsPolicePed(Ped ped)
        {
            if (ped == null || !ped.Exists()) return false;
            return ped.Model == PedHash.Cop01SMY ||
                   ped.Model == PedHash.Cop01SFY ||
                   ped.Model == PedHash.Sheriff01SMY ||
                   ped.Model == PedHash.Sheriff01SFY ||
                   ped.Model == PedHash.Swat01SMY ||
                   ped.Model == PedHash.Hwaycop01SMY ||
                   ped.RelationshipGroup == Function.Call<int>(Hash.GET_HASH_KEY, "COP");
        }

        private static Ped GetLookingAtPed(Ped player, float maxDist)
        {
            Vector3 camDir = GameplayCamera.Direction;
            Vector3 camPos = GameplayCamera.Position;

            var ray = GTA.World.Raycast(camPos, camDir, maxDist, IntersectFlags.Peds);
            if (ray.DidHit && ray.HitEntity is Ped hitPed && hitPed.Exists() && hitPed != player && !_bookedSuspects.Contains(hitPed.Handle))
            {
                return hitPed;
            }

            return GTA.World.GetNearbyPeds(player.Position, maxDist)
                .Where(p => p.Exists() && p != player && !p.IsDead && !_bookedSuspects.Contains(p.Handle))
                .OrderBy(p => player.Position.DistanceTo(p.Position))
                .FirstOrDefault();
        }

        private static void PlayPoliceSpeech(Ped player, string speechName)
        {
            Function.Call(Hash.PLAY_PED_AMBIENT_SPEECH_NATIVE, player.Handle, speechName, "SPEECH_PARAMS_FORCE_SHOUTED");
        }
    }
}
