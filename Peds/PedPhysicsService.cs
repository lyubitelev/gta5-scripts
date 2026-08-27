using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;

namespace gta.Peds
{
    internal sealed class PedPhysicsService
    {
        private const int MaxQueuedSmashes = 24;
        private const int MaxActiveSmashes = 4;

        private static readonly TimeSpan StartInterval = TimeSpan.FromMilliseconds(120);
        private static readonly TimeSpan MaxRiseTime = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan MaxFallTime = TimeSpan.FromSeconds(2);

        private readonly Queue<SmashJob> _pendingSmashes = new Queue<SmashJob>();
        private readonly List<SmashJob> _activeSmashes = new List<SmashJob>();

        private DateTime _nextStartUtc = DateTime.MinValue;

        public void SmashWithBlood(Ped npc, float throwStrength, float smashStrength)
        {
            QueueSmashWithBlood(new[] { npc }, throwStrength, smashStrength);
        }

        public void QueueSmashWithBlood(IEnumerable<Ped> peds, float throwStrength, float smashStrength)
        {
            foreach (var ped in peds.Where(IsValidTarget))
            {
                if (_pendingSmashes.Count + _activeSmashes.Count >= MaxQueuedSmashes)
                {
                    return;
                }

                if (IsScheduled(ped))
                {
                    continue;
                }

                _pendingSmashes.Enqueue(new SmashJob(ped, throwStrength, smashStrength));
            }
        }

        public void Update()
        {
            var now = DateTime.UtcNow;
            StartPendingSmashes(now);

            for (var i = _activeSmashes.Count - 1; i >= 0; i--)
            {
                if (AdvanceSmash(_activeSmashes[i], now))
                {
                    _activeSmashes.RemoveAt(i);
                }
            }
        }

        public void Throw(Ped npc, Vector3 force, float strength)
        {
            if (npc == null || !npc.Exists() || npc.IsDead)
            {
                return;
            }

            npc.CanRagdoll = true;
            npc.Task.ClearAllImmediately();
            npc.Task.ReactAndFlee(Game.Player.Character);
            Script.Wait(100);

            Function.Call(Hash.APPLY_FORCE_TO_ENTITY, npc.Handle, 1,
                force.X * strength, force.Y * strength, force.Z * strength,
                0, 0, 0, 0, false, true, true, false, true);
        }

        public void Push(Ped ped, Vector3 force)
        {
            if (ped != null && ped.Exists())
            {
                Function.Call(Hash.SET_ENTITY_VELOCITY, ped.Handle, force.X, force.Y, force.Z);
            }
        }

        public void ApplyForce(Ped ped, Vector3 direction, float force)
        {
            if (ped == null || !ped.Exists())
            {
                return;
            }

            Function.Call(Hash.APPLY_FORCE_TO_ENTITY, ped.Handle, 1,
                direction.X * force, direction.Y * force, direction.Z * force,
                0, 0, 0, 0, false, true, true, false, true);
        }

        private static void ApplyBlood(Ped npc)
        {
            for (var i = 0; i < 5; i++)
            {
                Function.Call(Hash.APPLY_PED_DAMAGE_PACK, npc.Handle, "BigHitByVehicle", 1.0f, 1.0f);
                Function.Call(Hash.APPLY_PED_BLOOD_BY_ZONE, npc.Handle, 3, 1.0f, 1.0f, "BigHitByVehicle");
            }
        }

        private void StartPendingSmashes(DateTime now)
        {
            if (now < _nextStartUtc)
            {
                return;
            }

            while (_pendingSmashes.Count > 0 && _activeSmashes.Count < MaxActiveSmashes)
            {
                var job = _pendingSmashes.Dequeue();
                if (!IsValidTarget(job.Ped))
                {
                    continue;
                }

                Launch(job, now);
                _activeSmashes.Add(job);
                _nextStartUtc = now + StartInterval;
                return;
            }
        }

        private static void Launch(SmashJob job, DateTime now)
        {
            job.Ped.CanRagdoll = true;
            job.Ped.Task.ClearAllImmediately();
            job.Ped.Ragdoll(-1);
            job.Ped.Velocity = new Vector3(0f, 0f, job.ThrowStrength);
            job.Phase = SmashPhase.WaitingForApex;
            job.PhaseDeadlineUtc = now + MaxRiseTime;
        }

        private static bool AdvanceSmash(SmashJob job, DateTime now)
        {
            if (!IsValidTarget(job.Ped))
            {
                return true;
            }

            switch (job.Phase)
            {
                case SmashPhase.WaitingForApex:
                    if (job.Ped.Velocity.Z <= 0.1f || now >= job.PhaseDeadlineUtc)
                    {
                        SlamDown(job, now);
                    }

                    return false;

                case SmashPhase.WaitingForGround:
                    if (!Function.Call<bool>(Hash.IS_ENTITY_IN_AIR, job.Ped.Handle) || now >= job.PhaseDeadlineUtc)
                    {
                        FinishSmash(job.Ped);
                        return true;
                    }

                    return false;

                default:
                    return true;
            }
        }

        private static void SlamDown(SmashJob job, DateTime now)
        {
            job.Ped.Velocity = new Vector3(0f, 0f, -job.SmashStrength);
            Function.Call(
                Hash.APPLY_FORCE_TO_ENTITY,
                job.Ped.Handle,
                1,
                0f,
                0f,
                -job.SmashStrength,
                0f,
                0f,
                0f,
                0,
                false,
                true,
                true,
                false,
                true);

            job.Phase = SmashPhase.WaitingForGround;
            job.PhaseDeadlineUtc = now + MaxFallTime;
        }

        private static void FinishSmash(Ped ped)
        {
            if (ped.Exists() && !ped.IsDead)
            {
                ped.ApplyDamage(1000);
                ApplyBlood(ped);
            }
        }

        private bool IsScheduled(Ped ped)
        {
            return _activeSmashes.Any(job => job.Ped.Handle == ped.Handle) ||
                   _pendingSmashes.Any(job => job.Ped.Handle == ped.Handle);
        }

        private static bool IsValidTarget(Ped ped)
        {
            return ped != null && ped.Exists() && !ped.IsDead && ped != Game.Player.Character;
        }

        private enum SmashPhase
        {
            WaitingForApex,
            WaitingForGround
        }

        private sealed class SmashJob
        {
            public SmashJob(Ped ped, float throwStrength, float smashStrength)
            {
                Ped = ped;
                ThrowStrength = throwStrength;
                SmashStrength = smashStrength;
            }

            public Ped Ped { get; }

            public float ThrowStrength { get; }

            public float SmashStrength { get; }

            public SmashPhase Phase { get; set; }

            public DateTime PhaseDeadlineUtc { get; set; }
        }
    }
}
