using GTA;
using GTA.Math;

namespace gta.Worlds
{
    internal sealed class NorthYanktonAmbientPedDefinition
    {
        public NorthYanktonAmbientPedDefinition(
            string id,
            PedHash model,
            Vector3 position,
            float heading,
            string scenario)
        {
            Id = id;
            Model = model;
            Position = position;
            Heading = heading;
            Scenario = scenario;
        }

        public string Id { get; }

        public PedHash Model { get; }

        public Vector3 Position { get; }

        public float Heading { get; }

        public string Scenario { get; }
    }
}
