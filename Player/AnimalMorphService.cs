using System;
using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;

namespace gta.Player
{
    internal sealed class AnimalMorphService
    {
        private const float FlyingSpawnHeight = 24f;
        private const float FlyingForwardVelocity = 9f;
        private const float FlyingUpVelocity = 2.5f;
        private const float WaterSubmergeDepth = 2f;
        private const float WaterProbeHeight = 1000f;
        private const int WaterSearchDirections = 16;

        private static readonly float[] WaterSearchRadii =
        {
            0f,
            80f,
            160f,
            320f,
            640f,
            1200f,
            2200f,
            3600f
        };

        private static readonly Vector3[] WaterFallbackPositions =
        {
            new Vector3(-1845f, -1220f, 20f),
            new Vector3(1280f, 4240f, 60f),
            new Vector3(-3420f, 960f, 40f),
            new Vector3(3900f, -1800f, 40f),
            new Vector3(-1200f, 6700f, 40f),
            new Vector3(1700f, 6900f, 40f),
            new Vector3(-1800f, -3000f, 40f)
        };

        private const string MichaelOnlineModelName = "mp_m_freemode_01";
        private static readonly int MichaelStoryModelHash = new Model("player_zero").Hash;

        private readonly RelationshipGroup _playerGroup;

        private bool _isOnlineSeniorActive;
        private int? _originalModelHash;
        private int? _michaelOriginalModelHash;

        public AnimalMorphService(RelationshipGroup playerGroup)
        {
            _playerGroup = playerGroup;
        }

        public void Cycle()
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                return;
            }

            if (player.IsInVehicle())
            {
                Notifier.Show("Выйди из транспорта перед сменой модели");
                return;
            }

            if (!IsSwitchableMichaelModel(player))
            {
                Notifier.Show("Подмена доступна только для Майкла");
                return;
            }

            if (!_originalModelHash.HasValue)
            {
                _originalModelHash = player.Model.Hash;
            }

            if (!_michaelOriginalModelHash.HasValue && IsSwitchableMichaelModel(player))
            {
                _michaelOriginalModelHash = player.Model.Hash;
            }

            if (_isOnlineSeniorActive)
            {
                RestoreOriginalModel();
                return;
            }

            if (ApplyModel(MichaelOnlineModelName, AnimalEnvironment.Ground))
            {
                _isOnlineSeniorActive = true;
                Notifier.Show("Смена: Онлайн Майкл");
            }
        }

        public void RestoreOriginalModel()
        {
            var targetHash = _michaelOriginalModelHash ?? _originalModelHash;
            if (targetHash.HasValue && ApplyModel(targetHash.Value, AnimalEnvironment.Ground))
            {
                Notifier.Show("Смена: Обычный персонаж");
            }

            _originalModelHash = null;
            _michaelOriginalModelHash = null;
            _isOnlineSeniorActive = false;
        }

        private static bool IsSwitchableMichaelModel(Ped player)
        {
            if (player == null || !player.Exists())
            {
                return false;
            }

            var hash = player.Model.Hash;
            return hash == MichaelStoryModelHash ||
                   hash == new Model("ig_michael").Hash ||
                   hash == new Model(MichaelOnlineModelName).Hash;
        }

        private bool ApplyModel(int modelHash, AnimalEnvironment environment)
        {
            return ApplyModel(new Model(modelHash), environment);
        }

        private bool ApplyModel(string modelName, AnimalEnvironment environment)
        {
            return ApplyModel(new Model(modelName), environment);
        }

        private bool ApplyModel(Model model, AnimalEnvironment environment)
        {
            var player = Game.Player.Character;
            var position = player.Position;
            var heading = player.Heading;

            if (!model.IsInCdImage || !model.IsValid)
            {
                Notifier.Show("Модель недоступна");
                return false;
            }

            if (!model.Request(1000))
            {
                Notifier.Show("Модель не загрузилась");
                return false;
            }

            Function.Call(Hash.SET_PLAYER_MODEL, Game.Player.Handle, model.Hash);

            var newPlayer = Game.Player.Character;
            if (newPlayer != null && newPlayer.Exists())
            {
                Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, newPlayer.Handle);
                newPlayer.RelationshipGroup = _playerGroup;
                newPlayer.Heading = heading;
                ApplyEnvironment(newPlayer, ResolveEnvironmentPosition(position, environment), environment);
            }

            model.MarkAsNoLongerNeeded();
            return true;
        }

        private static Vector3 ResolveEnvironmentPosition(Vector3 origin, AnimalEnvironment environment)
        {
            switch (environment)
            {
                case AnimalEnvironment.Water:
                    Vector3 waterPosition;
                    return TryResolveWaterPosition(origin, out waterPosition)
                        ? waterPosition
                        : origin;

                case AnimalEnvironment.Air:
                    return ResolveAirPosition(origin);

                default:
                    return origin;
            }
        }

        private static void ApplyEnvironment(Ped player, Vector3 position, AnimalEnvironment environment)
        {
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, position.X, position.Y, position.Z);
            Function.Call(
                Hash.SET_ENTITY_COORDS_NO_OFFSET,
                player.Handle,
                position.X,
                position.Y,
                position.Z,
                true,
                true,
                true);

            if (environment == AnimalEnvironment.Water)
            {
                Function.Call(Hash.SET_PED_DIES_IN_WATER, player.Handle, false);
                Function.Call(Hash.SET_PED_DIES_INSTANTLY_IN_WATER, player.Handle, false);
                Function.Call(Hash.SET_ENTITY_VELOCITY, player.Handle, 0f, 0f, 0f);
                return;
            }

            if (environment == AnimalEnvironment.Air)
            {
                var forward = player.ForwardVector;
                Function.Call(
                    Hash.SET_ENTITY_VELOCITY,
                    player.Handle,
                    forward.X * FlyingForwardVelocity,
                    forward.Y * FlyingForwardVelocity,
                    FlyingUpVelocity);
            }
        }

        private static Vector3 ResolveAirPosition(Vector3 origin)
        {
            var position = origin;
            float groundZ;

            if (GTA.World.GetGroundHeight(origin + new Vector3(0f, 0f, 3f), out groundZ, GetGroundHeightMode.Normal))
            {
                var minimumZ = groundZ + FlyingSpawnHeight;
                if (position.Z < minimumZ)
                {
                    position.Z = minimumZ;
                }

                return position;
            }

            position.Z += FlyingSpawnHeight;
            return position;
        }

        private static bool TryResolveWaterPosition(Vector3 origin, out Vector3 position)
        {
            for (var radiusIndex = 0; radiusIndex < WaterSearchRadii.Length; radiusIndex++)
            {
                var radius = WaterSearchRadii[radiusIndex];
                if (radius <= 0f)
                {
                    if (TryGetWaterPosition(origin, out position))
                    {
                        return true;
                    }

                    continue;
                }

                for (var direction = 0; direction < WaterSearchDirections; direction++)
                {
                    var angle = Math.PI * 2.0 * direction / WaterSearchDirections;
                    var candidate = origin + new Vector3(
                        (float)Math.Cos(angle) * radius,
                        (float)Math.Sin(angle) * radius,
                        0f);

                    if (TryGetWaterPosition(candidate, out position))
                    {
                        return true;
                    }
                }
            }

            return TryResolveFallbackWaterPosition(origin, out position);
        }

        private static bool TryResolveFallbackWaterPosition(Vector3 origin, out Vector3 position)
        {
            var found = false;
            var bestDistance = float.MaxValue;
            position = origin;

            for (var i = 0; i < WaterFallbackPositions.Length; i++)
            {
                Vector3 waterPosition;
                if (!TryGetWaterPosition(WaterFallbackPositions[i], out waterPosition))
                {
                    continue;
                }

                var distance = origin.DistanceTo(WaterFallbackPositions[i]);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                position = waterPosition;
                found = true;
            }

            return found;
        }

        private static bool TryGetWaterPosition(Vector3 candidate, out Vector3 position)
        {
            float waterZ;
            if (!TryGetWaterHeight(candidate, out waterZ))
            {
                position = candidate;
                return false;
            }

            position = new Vector3(candidate.X, candidate.Y, waterZ - WaterSubmergeDepth);
            return true;
        }

        private static bool TryGetWaterHeight(Vector3 position, out float waterZ)
        {
            var output = new OutputArgument();
            if (Function.Call<bool>(
                Hash.GET_WATER_HEIGHT_NO_WAVES,
                position.X,
                position.Y,
                WaterProbeHeight,
                output))
            {
                waterZ = output.GetResult<float>();
                return true;
            }

            output = new OutputArgument();
            if (Function.Call<bool>(
                Hash.GET_WATER_HEIGHT,
                position.X,
                position.Y,
                WaterProbeHeight,
                output))
            {
                waterZ = output.GetResult<float>();
                return true;
            }

            waterZ = position.Z;
            return false;
        }

        private enum AnimalEnvironment
        {
            Ground,
            Water,
            Air
        }

    }
}
