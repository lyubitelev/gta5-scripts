using GTA.Math;

namespace gta.Worlds
{
    internal static class NorthYanktonRoadGuard
    {
        private static readonly RoadSegment[] KnownRoads =
        {
            new RoadSegment(new Vector2(3135f, -4910f), new Vector2(3260f, -4810f), 10f),
            new RoadSegment(new Vector2(3210f, -4845f), new Vector2(3415f, -5025f), 10f),
            new RoadSegment(new Vector2(3080f, -4665f), new Vector2(3210f, -4785f), 9f)
        };

        public static bool IsLikelyOnRoad(Vector3 position)
        {
            var point = new Vector2(position.X, position.Y);

            foreach (var road in KnownRoads)
            {
                if (road.Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        private struct RoadSegment
        {
            private readonly Vector2 _start;
            private readonly Vector2 _end;
            private readonly float _halfWidth;

            public RoadSegment(Vector2 start, Vector2 end, float halfWidth)
            {
                _start = start;
                _end = end;
                _halfWidth = halfWidth;
            }

            public bool Contains(Vector2 point)
            {
                var segment = _end - _start;
                var lengthSquared = segment.LengthSquared();

                if (lengthSquared <= 0.001f)
                {
                    return false;
                }

                var t = Dot(point - _start, segment) / lengthSquared;
                if (t < 0f || t > 1f)
                {
                    return false;
                }

                var closest = _start + segment * t;
                return point.DistanceTo(closest) <= _halfWidth;
            }

            private static float Dot(Vector2 left, Vector2 right)
            {
                return left.X * right.X + left.Y * right.Y;
            }
        }
    }
}
