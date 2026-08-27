using System.Collections.Generic;
using GTA.Math;

namespace gta.Worlds
{
    internal sealed class NorthYanktonAmbientZone
    {
        public NorthYanktonAmbientZone(
            string name,
            Vector3 center,
            float radius,
            IReadOnlyList<NorthYanktonAmbientPedDefinition> pedestrians)
        {
            Name = name;
            Center = center;
            Radius = radius;
            Pedestrians = pedestrians;
        }

        public string Name { get; }

        public Vector3 Center { get; }

        public float Radius { get; }

        public IReadOnlyList<NorthYanktonAmbientPedDefinition> Pedestrians { get; }

        public bool Contains(Vector3 position)
        {
            return position.DistanceTo(Center) <= Radius;
        }
    }
}
