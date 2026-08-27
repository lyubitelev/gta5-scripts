using System.Linq;
using GTA;
using GTA.Native;

namespace gta.Peds
{
    internal sealed class PedQueryService
    {
        public bool IsHostile(Ped ped)
        {
            if (ped.IsInCombatAgainst(Game.Player.Character))
            {
                return true;
            }

            if (ped.HasBeenDamagedBy(Game.Player.Character))
            {
                return true;
            }

            return ped.RelationshipGroup == Function.Call<int>(Hash.GET_HASH_KEY, "COP") ||
                   ped.RelationshipGroup == Function.Call<int>(Hash.GET_HASH_KEY, "GANG_1");
        }

        public Ped GetClosestPed(float radius)
        {
            var player = Game.Player.Character;
            var closest = GetNearbyPeds(radius, 1)
                .FirstOrDefault();

            return closest;
        }

        public Ped[] GetNearbyPeds(float radius, int maxCount)
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists()) return new Ped[0];

            return GTA.World.GetAllPeds()
                .Where(npc => npc != player && npc.Exists() && !npc.IsDead && player.Position.DistanceTo(npc.Position) <= radius)
                .OrderBy(npc => player.Position.DistanceTo(npc.Position))
                .Take(maxCount)
                .ToArray();
        }

        public Ped GetAimedPed(Ped player)
        {
            var ray = GTA.World.Raycast(player.Position, GameplayCamera.Direction, 50f, IntersectFlags.Peds);
            if (ray.DidHit && ray.HitEntity is Ped ped && ped.Exists() && !ped.IsDead && ped != player)
            {
                return ped;
            }

            return null;
        }
    }
}
