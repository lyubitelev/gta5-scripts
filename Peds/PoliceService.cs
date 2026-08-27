using System;
using System.IO;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;

namespace gta.Peds
{
    internal sealed class PoliceService
    {
        private bool _isSuppressed;

        public bool IsSuppressed => _isSuppressed;

        public void ToggleSuppression()
        {
            _isSuppressed = !_isSuppressed;
            ApplySuppressionState();
            Notifier.Show(_isSuppressed ? "В жопу полицию (Полиция отключена)" : "Теперь они следят (Полиция включена)");
        }

        private void ApplySuppressionState()
        {
            var player = Game.Player;
            if (player == null) return;

            if (_isSuppressed)
            {
                Function.Call(Hash.SET_MAX_WANTED_LEVEL, 0);
                Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, player.Handle);
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, player.Handle, true);
                Function.Call(Hash.SET_DISPATCH_COPS_FOR_PLAYER, player.Handle, false);
                Function.Call(Hash.SET_CREATE_RANDOM_COPS, false);

                if (player.Character != null && player.Character.Exists())
                {
                    Function.Call(Hash.SET_IGNORE_LOW_PRIORITY_SHOCKING_EVENTS, player.Character.Handle, true);
                }
            }
            else
            {
                Function.Call(Hash.SET_MAX_WANTED_LEVEL, 5);
                Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, player.Handle, false);
                Function.Call(Hash.SET_DISPATCH_COPS_FOR_PLAYER, player.Handle, true);
                Function.Call(Hash.SET_CREATE_RANDOM_COPS, true);

                if (player.Character != null && player.Character.Exists())
                {
                    Function.Call(Hash.SET_IGNORE_LOW_PRIORITY_SHOCKING_EVENTS, player.Character.Handle, false);
                }
            }
        }

        public void ApplyWantedState()
        {
            if (!_isSuppressed) return;

            var player = Game.Player;
            if (player == null) return;

            Function.Call(Hash.SET_MAX_WANTED_LEVEL, 0);
            if (player.Wanted.WantedLevel > 0)
            {
                Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, player.Handle);
            }
        }

        public void Hunt(Ped target, Ped player)
        {
            File.AppendAllText(ScriptPaths.PoliceHuntLog, "\n--- PoliceHunt вызван ---\n");

            try
            {
                if (target == null || !target.Exists() || target.IsDead)
                {
                    File.AppendAllText(ScriptPaths.PoliceHuntLog, "Цель не существует или мертва, выход из функции.\n");
                    Notifier.Show("Цель уже не существует или мертва!");
                    return;
                }

                Notifier.Show($"Подстава: полиция охотится за NPC {target.Handle}");
                File.AppendAllText(ScriptPaths.PoliceHuntLog, $"Target найден: {target.Handle}\n");

                var cops = GTA.World.GetNearbyPeds(target.Position, 100f)
                    .Where(p => p.Exists() && (p.Model == PedHash.Cop01SMY || p.Model == PedHash.Cop01SFY))
                    .ToArray();

                File.AppendAllText(ScriptPaths.PoliceHuntLog, $"Найдено копов: {cops.Length}\n");

                if (cops.Length == 0)
                {
                    cops = SpawnBackupCops(player);
                    if (cops.Length == 0)
                    {
                        return;
                    }
                }

                foreach (var cop in cops)
                {
                    if (cop.Exists() && target.Exists() && !target.IsDead)
                    {
                        cop.Task.Combat(target, TaskCombatFlags.None, TaskThreatResponseFlags.None);
                        File.AppendAllText(ScriptPaths.PoliceHuntLog, $"Коп {cop.Handle} атакует {target.Handle}.\n");
                    }
                }

                TryConfigureHuntRelationships();
            }
            catch (Exception ex)
            {
                File.AppendAllText(ScriptPaths.PoliceHuntLog, $"Ошибка в PoliceHunt: {ex.Message}\n");
                Notifier.Show("Ошибка при вызове подкрепления!");
            }
        }

        private static Ped[] SpawnBackupCops(Ped player)
        {
            var spawnHeading = (player.Heading + 180f) % 360f;
            var spawnDirection = Vector3.Normalize(new Vector3(
                (float)-Math.Sin(spawnHeading * Math.PI / 180.0),
                (float)Math.Cos(spawnHeading * Math.PI / 180.0),
                0f));
            var spawnPos = player.Position + (spawnDirection * 40f);
            spawnPos.Z = GetGroundZ(spawnPos);

            var carModel = new Model(VehicleHash.Police);
            var copModel = new Model(PedHash.Cop01SMY);

            if (!carModel.Request(1000) || !copModel.Request(1000))
            {
                File.AppendAllText(ScriptPaths.PoliceHuntLog, "Не удалось загрузить модели копов или машины.\n");
                Notifier.Show("Не удалось загрузить модели копов!");
                return Array.Empty<Ped>();
            }

            var policeCar = GTA.World.CreateVehicle(carModel, spawnPos, spawnHeading);
            if (policeCar == null || !policeCar.Exists())
            {
                File.AppendAllText(ScriptPaths.PoliceHuntLog, "Не удалось заспавнить полицейскую машину.\n");
                Notifier.Show("Не удалось заспавнить полицейскую машину!");
                return Array.Empty<Ped>();
            }

            policeCar.IsSirenActive = true;
            policeCar.IsUndriveable = false;

            var cop1 = policeCar.CreatePedOnSeat(VehicleSeat.Driver, copModel);
            var cop2 = policeCar.CreatePedOnSeat(VehicleSeat.Passenger, copModel);

            var cops = new[] { cop1, cop2 }.Where(c => c != null && c.Exists()).ToArray();
            File.AppendAllText(ScriptPaths.PoliceHuntLog, $"Заспавнено копов: {cops.Length}\n");

            foreach (var cop in cops)
            {
                cop.Weapons.Give(WeaponHash.Pistol, 999, true, true);
                cop.Accuracy = 80;
                cop.Armor = 100;
                cop.Health = 200;
                cop.RelationshipGroup = "HuntingCops";
            }

            carModel.MarkAsNoLongerNeeded();
            copModel.MarkAsNoLongerNeeded();
            return cops;
        }

        private static void TryConfigureHuntRelationships()
        {
            try
            {
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 5, "HuntingCops", "TARGET_NPC");
                File.AppendAllText(ScriptPaths.PoliceHuntLog, "Отношения копов и NPC настроены.\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText(ScriptPaths.PoliceHuntLog, $"Ошибка при настройке отношений: {ex.Message}\n");
            }
        }

        private static float GetGroundZ(Vector3 position)
        {
            float groundZ;
            return GTA.World.GetGroundHeight(position + new Vector3(0f, 0f, 3f), out groundZ, GetGroundHeightMode.Normal)
                ? groundZ
                : position.Z;
        }
    }
}
