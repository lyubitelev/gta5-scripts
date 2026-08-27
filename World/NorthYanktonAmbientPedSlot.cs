using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace gta.Worlds
{
    internal sealed class NorthYanktonAmbientPedSlot
    {
        private static readonly Vector3 GroundProbeOffset = new Vector3(0f, 0f, 3f);

        private readonly NorthYanktonAmbientPedDefinition _definition;

        private Ped _ped;
        private DateTime _nextSpawnAttemptUtc = DateTime.MinValue;

        public NorthYanktonAmbientPedSlot(NorthYanktonAmbientPedDefinition definition)
        {
            _definition = definition;
        }

        public bool IsAlive => _ped != null && _ped.Exists() && !_ped.IsDead;

        public bool Update(Vector3 playerPosition, bool zoneActive, DateTime now, int activeCount, int maxActiveCount)
        {
            if (IsAlive)
            {
                if (!zoneActive || playerPosition.DistanceTo(_definition.Position) > NorthYanktonAmbientService.DespawnDistance)
                {
                    DeletePed();
                }

                return IsAlive;
            }

            if (!zoneActive ||
                activeCount >= maxActiveCount ||
                playerPosition.DistanceTo(_definition.Position) > NorthYanktonAmbientService.SpawnDistance ||
                now < _nextSpawnAttemptUtc)
            {
                return false;
            }

            _nextSpawnAttemptUtc = now.AddSeconds(15);
            return TrySpawn();
        }

        public void Clear()
        {
            DeletePed();
            _nextSpawnAttemptUtc = DateTime.MinValue;
        }

        private bool TrySpawn()
        {
            Vector3 spawnPosition;
            if (!TryResolveSafePosition(out spawnPosition))
            {
                return false;
            }

            var model = new Model(_definition.Model);
            if (!model.IsInCdImage || !model.IsPed || !model.Request(1000))
            {
                return false;
            }

            _ped = GTA.World.CreatePed(model, spawnPosition, _definition.Heading);
            model.MarkAsNoLongerNeeded();

            if (!IsAlive)
            {
                return false;
            }

            ConfigurePed(_ped);
            StartScenario(_ped, spawnPosition);
            return true;
        }

        private bool TryResolveSafePosition(out Vector3 position)
        {
            foreach (var candidate in GetCandidatePositions())
            {
                position = candidate;

                if (TryMoveToNativeSafeCoord(ref position) && IsSafeForAmbientPed(position))
                {
                    return true;
                }

                position = candidate;
                if (TryMoveToRoadSide(ref position) && IsSafeForAmbientPed(position))
                {
                    return true;
                }

                position = candidate;
                if (IsSafeForAmbientPed(position))
                {
                    return true;
                }
            }

            position = _definition.Position;
            return false;
        }

        private Vector3[] GetCandidatePositions()
        {
            var origin = _definition.Position;
            return new[]
            {
                origin,
                origin + new Vector3(0f, 10f, 0f),
                origin + new Vector3(0f, -10f, 0f),
                origin + new Vector3(10f, 0f, 0f),
                origin + new Vector3(-10f, 0f, 0f),
                origin + new Vector3(10f, 10f, 0f),
                origin + new Vector3(-10f, -10f, 0f),
                origin + new Vector3(14f, -14f, 0f),
                origin + new Vector3(-14f, 14f, 0f)
            };
        }

        private static bool TryMoveToNativeSafeCoord(ref Vector3 position)
        {
            var safePosition = new OutputArgument();
            var found = Function.Call<bool>(
                Hash.GET_SAFE_COORD_FOR_PED,
                position.X,
                position.Y,
                position.Z,
                true,
                safePosition,
                16);

            if (!found)
            {
                return false;
            }

            position = safePosition.GetResult<Vector3>();
            return true;
        }

        private static bool TryMoveToRoadSide(ref Vector3 position)
        {
            for (var side = 0; side <= 1; side++)
            {
                var sidePosition = new OutputArgument();
                var found = Function.Call<bool>(
                    Hash.GET_POSITION_BY_SIDE_OF_ROAD,
                    position.X,
                    position.Y,
                    position.Z,
                    side,
                    sidePosition);

                if (!found)
                {
                    continue;
                }

                position = sidePosition.GetResult<Vector3>();
                return true;
            }

            return false;
        }

        private static bool IsSafeForAmbientPed(Vector3 position)
        {
            float groundZ;
            if (GTA.World.GetGroundHeight(position + GroundProbeOffset, out groundZ, GetGroundHeightMode.Normal))
            {
                position.Z = groundZ;
            }

            if (NorthYanktonRoadGuard.IsLikelyOnRoad(position))
            {
                return false;
            }

            var isRoad = Function.Call<bool>(
                Hash.IS_POINT_ON_ROAD,
                position.X,
                position.Y,
                position.Z,
                0);

            return !isRoad;
        }

        private static void ConfigurePed(Ped ped)
        {
            ped.IsPersistent = true;
            ped.BlockPermanentEvents = true;

            Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, true);
            Function.Call(Hash.SET_PED_CAN_BE_TARGETTED, ped.Handle, false);
            Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped.Handle, 0, false);
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, 5, false);
            Function.Call(Hash.SET_PED_KEEP_TASK, ped.Handle, true);
        }

        private void StartScenario(Ped ped, Vector3 position)
        {
            if (string.IsNullOrEmpty(_definition.Scenario))
            {
                ped.Task.StandStill(-1);
                return;
            }

            ped.Task.StartScenarioAtPosition(
                _definition.Scenario,
                position,
                _definition.Heading,
                -1,
                true,
                false);
        }

        private void DeletePed()
        {
            if (_ped == null || !_ped.Exists())
            {
                _ped = null;
                return;
            }

            _ped.Task.ClearAllImmediately();
            _ped.MarkAsNoLongerNeeded();
            _ped.Delete();
            _ped = null;
        }
    }
}
