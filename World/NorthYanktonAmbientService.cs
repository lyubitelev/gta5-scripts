using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;

namespace gta.Worlds
{
    internal sealed class NorthYanktonAmbientService
    {
        public const float SpawnDistance = 240f;
        public const float DespawnDistance = 360f;

        private const int MaxActivePeds = 10;
        private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(3);

        private readonly IReadOnlyList<NorthYanktonAmbientZone> _zones;
        private readonly Dictionary<string, IReadOnlyList<NorthYanktonAmbientPedSlot>> _slotsByZone;

        private bool _isEnabled;
        private DateTime _nextUpdateUtc = DateTime.MinValue;

        public NorthYanktonAmbientService()
        {
            _zones = CreateZones();
            _slotsByZone = _zones.ToDictionary(
                zone => zone.Name,
                zone => (IReadOnlyList<NorthYanktonAmbientPedSlot>)zone.Pedestrians
                    .Select(definition => new NorthYanktonAmbientPedSlot(definition))
                    .ToArray());
        }

        public void Enable()
        {
            _isEnabled = true;
            _nextUpdateUtc = DateTime.MinValue;
        }

        public void Disable()
        {
            _isEnabled = false;
            Clear();
        }

        public void Clear()
        {
            foreach (var slot in _slotsByZone.Values.SelectMany(slots => slots))
            {
                slot.Clear();
            }
        }

        public void Update(DateTime now)
        {
            if (!_isEnabled || now < _nextUpdateUtc)
            {
                return;
            }

            _nextUpdateUtc = now + UpdateInterval;

            var playerPosition = Game.Player.Character.Position;
            var activeCount = _slotsByZone.Values.SelectMany(slots => slots).Count(slot => slot.IsAlive);

            foreach (var zone in _zones)
            {
                var zoneActive = zone.Contains(playerPosition);
                foreach (var slot in _slotsByZone[zone.Name])
                {
                    var wasAlive = slot.IsAlive;
                    var isAlive = slot.Update(playerPosition, zoneActive, now, activeCount, MaxActivePeds);

                    if (!wasAlive && isAlive)
                    {
                        activeCount++;
                    }
                    else if (wasAlive && !isAlive)
                    {
                        activeCount--;
                    }
                }
            }
        }

        private static IReadOnlyList<NorthYanktonAmbientZone> CreateZones()
        {
            return new[]
            {
                new NorthYanktonAmbientZone(
                    "BankBlock",
                    new Vector3(3217.69f, -4834.51f, 111.81f),
                    300f,
                    new[]
                    {
                        new NorthYanktonAmbientPedDefinition("bank_guard", PedHash.PrologueSec01, new Vector3(3198.6f, -4838.4f, 111.7f), 275f, "WORLD_HUMAN_GUARD_STAND"),
                        new NorthYanktonAmbientPedDefinition("bank_smoker", PedHash.Business01AMM, new Vector3(3211.4f, -4850.1f, 111.8f), 35f, "WORLD_HUMAN_SMOKING"),
                        new NorthYanktonAmbientPedDefinition("depot_clipboard", PedHash.Autoshop01SMM, new Vector3(3234.2f, -4820.5f, 111.9f), 170f, "WORLD_HUMAN_CLIPBOARD"),
                        new NorthYanktonAmbientPedDefinition("sidewalk_phone", PedHash.Business02AFY, new Vector3(3183.7f, -4861.5f, 111.8f), 95f, "WORLD_HUMAN_STAND_MOBILE")
                    }),
                new NorthYanktonAmbientZone(
                    "MainStreet",
                    new Vector3(3290f, -4895f, 111.5f),
                    330f,
                    new[]
                    {
                        new NorthYanktonAmbientPedDefinition("street_lean", PedHash.Genstreet02AMY, new Vector3(3295.8f, -4886.7f, 111.6f), 210f, "WORLD_HUMAN_LEANING"),
                        new NorthYanktonAmbientPedDefinition("street_coffee", PedHash.Business01AFY, new Vector3(3317.1f, -4910.6f, 111.4f), 25f, "WORLD_HUMAN_DRINKING"),
                        new NorthYanktonAmbientPedDefinition("street_hangout", PedHash.Hillbilly02AMM, new Vector3(3264.4f, -4921.2f, 111.5f), 330f, "WORLD_HUMAN_HANG_OUT_STREET")
                    }),
                new NorthYanktonAmbientZone(
                    "ChurchRoad",
                    new Vector3(3370f, -4965f, 112f),
                    360f,
                    new[]
                    {
                        new NorthYanktonAmbientPedDefinition("church_mourn_female", PedHash.PrologueMournFemale01, new Vector3(3377.2f, -4977.8f, 112.1f), 185f, "WORLD_HUMAN_STAND_IMPATIENT"),
                        new NorthYanktonAmbientPedDefinition("church_mourn_male", PedHash.PrologueMournMale01, new Vector3(3392.5f, -4991.4f, 112.0f), 140f, "WORLD_HUMAN_STAND_IMPATIENT"),
                        new NorthYanktonAmbientPedDefinition("church_smoker", PedHash.Tramp01AMM, new Vector3(3349.3f, -4946.6f, 111.8f), 15f, "WORLD_HUMAN_SMOKING")
                    }),
                new NorthYanktonAmbientZone(
                    "FarmHouses",
                    new Vector3(3155f, -4740f, 111f),
                    320f,
                    new[]
                    {
                        new NorthYanktonAmbientPedDefinition("porch_sitter", PedHash.Farmer01AMM, new Vector3(3141.4f, -4742.7f, 111.3f), 90f, "WORLD_HUMAN_SIT_BENCH"),
                        new NorthYanktonAmbientPedDefinition("porch_smoker", PedHash.Rurmeth01AFY, new Vector3(3168.8f, -4727.2f, 111.1f), 250f, "WORLD_HUMAN_SMOKING"),
                        new NorthYanktonAmbientPedDefinition("yard_worker", PedHash.Hillbilly01AMM, new Vector3(3186.9f, -4759.4f, 111.2f), 180f, "WORLD_HUMAN_GARDENER_LEAF_BLOWER")
                    })
            };
        }
    }
}
