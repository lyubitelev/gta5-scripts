using System;
using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;

namespace gta.Player
{
    internal sealed class TelekinesisService
    {
        private Entity _heldEntity;
        private float _holdDistance = 12f;
        private const float MinHoldDistance = 3.0f;
        private const float MaxHoldDistance = 70.0f;
        private const float ThrowVelocity = 95.0f;
        private const float PedThrowForce = 180.0f;

        public bool IsHolding => _heldEntity != null && _heldEntity.Exists();
        public Entity HeldEntity => _heldEntity;

        public void Update()
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists() || player.IsDead)
            {
                Release(false);
                return;
            }

            bool isAiming = player.IsAiming 
                || Game.IsControlPressed(GTA.Control.Aim) 
                || Game.IsControlPressed(GTA.Control.AccurateAim)
                || Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING, Game.Player.Handle)
                || Function.Call<bool>(Hash.IS_AIM_CAM_ACTIVE);

            bool isContextJustPressed = Game.IsControlJustPressed(GTA.Control.Context);

            // If aiming with bare hands (unarmed), face crosshair and show hand energy spark
            if (isAiming && player.Weapons.Current.Hash == WeaponHash.Unarmed && player.CurrentVehicle == null)
            {
                player.Heading = GameplayCamera.Rotation.Z;
                Vector3 handPos = player.Bones[(Bone)57005].Position;
                Function.Call(Hash.DRAW_LIGHT_WITH_RANGE, handPos.X, handPos.Y, handPos.Z, 0, 200, 255, 0.9f, 2.0f);
            }

            // 1. Grab target on Aim + E, or Soft Release on E
            if (isContextJustPressed)
            {
                if (IsHolding)
                {
                    Release(false);
                    Notifier.Show("~y~[Телекинез]~s~ Объект мягко отпущен");
                    return;
                }
                else if (isAiming)
                {
                    TryGrabTarget(player);
                    return;
                }
            }

            // 2. Process active levitation
            if (IsHolding)
            {
                ProcessLevitation(player);
            }
        }

        private void TryGrabTarget(Ped player)
        {
            Vector3 camPos = GameplayCamera.Position;
            Vector3 camDir = GameplayCamera.Direction;
            Vector3 rayEnd = camPos + camDir * MaxHoldDistance;

            // 1. Raycast with IntersectFlags.Everything (includes ragdolls and dead bodies)
            var hit = GTA.World.Raycast(camPos, rayEnd, IntersectFlags.Everything, player);
            Entity target = (hit.DidHit && hit.HitEntity != null && hit.HitEntity.Exists() && hit.HitEntity != player)
                ? hit.HitEntity
                : null;

            // 2. Volumetric aim-ray scan for nearby peds (living or dead corpses) if raycast missed
            if (target == null)
            {
                float closestDistToRay = 3.5f;
                var nearbyPeds = GTA.World.GetNearbyPeds(player.Position, MaxHoldDistance);
                if (nearbyPeds != null)
                {
                    foreach (var p in nearbyPeds)
                    {
                        if (p == null || !p.Exists() || p == player) continue;
                        Vector3 toPed = p.Position - camPos;
                        float proj = Vector3.Dot(toPed, camDir);
                        if (proj > 1.2f && proj < MaxHoldDistance)
                        {
                            Vector3 closestPtOnRay = camPos + camDir * proj;
                            float distToRay = p.Position.DistanceTo(closestPtOnRay);
                            if (distToRay < closestDistToRay)
                            {
                                closestDistToRay = distToRay;
                                target = p;
                            }
                        }
                    }
                }
            }

            // 3. Volumetric aim-ray scan for nearby vehicles if still null
            if (target == null)
            {
                float closestDistToRay = 4.5f;
                var nearbyVehs = GTA.World.GetNearbyVehicles(player.Position, MaxHoldDistance);
                if (nearbyVehs != null)
                {
                    foreach (var v in nearbyVehs)
                    {
                        if (v == null || !v.Exists() || player.CurrentVehicle == v) continue;
                        Vector3 toVeh = v.Position - camPos;
                        float proj = Vector3.Dot(toVeh, camDir);
                        if (proj > 2.0f && proj < MaxHoldDistance)
                        {
                            Vector3 closestPtOnRay = camPos + camDir * proj;
                            float distToRay = v.Position.DistanceTo(closestPtOnRay);
                            if (distToRay < closestDistToRay)
                            {
                                closestDistToRay = distToRay;
                                target = v;
                            }
                        }
                    }
                }
            }

            if (target != null && target.Exists())
            {
                // Don't grab player's current vehicle
                if (target is Vehicle veh && player.CurrentVehicle == veh)
                {
                    return;
                }

                _heldEntity = target;
                _holdDistance = Math.Max(MinHoldDistance, Math.Min(MaxHoldDistance, camPos.DistanceTo(_heldEntity.Position)));

                if (_heldEntity is Ped ped)
                {
                    Function.Call(Hash.SET_PED_TO_RAGDOLL, ped.Handle, 2500, 2500, 0, false, false, false);
                }

                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET", 1);
                Notifier.Show("~b~[Телекинез]~s~ Цель захвачена! ~w~(ЛКМ - Бросок, E - Отпустить)");
            }
        }

        private void ProcessLevitation(Ped player)
        {
            if (_heldEntity == null || !_heldEntity.Exists())
            {
                Release(false);
                return;
            }

            // Adjust distance via Weapon Select / Mouse Scroll
            if (Game.IsControlPressed(GTA.Control.SelectPrevWeapon))
            {
                _holdDistance = Math.Min(MaxHoldDistance, _holdDistance + 0.6f);
            }
            else if (Game.IsControlPressed(GTA.Control.SelectNextWeapon))
            {
                _holdDistance = Math.Max(MinHoldDistance, _holdDistance - 0.6f);
            }

            // Throw / Launch on Left Mouse Button (Attack)
            if (Game.IsControlJustPressed(GTA.Control.Attack))
            {
                ThrowHeldEntity();
                return;
            }

            Vector3 camPos = GameplayCamera.Position;
            Vector3 camDir = GameplayCamera.Direction;
            Vector3 targetPos = camPos + camDir * _holdDistance;
            Vector3 currentPos = _heldEntity.Position;
            Vector3 diff = targetPos - currentPos;

            Vector3 targetVel = diff * 8.5f;
            if (targetVel.Length() > 35.0f)
            {
                targetVel = targetVel.Normalized * 35.0f;
            }

            // 1. Ped physics (supports living ragdolls and dead corpses)
            if (_heldEntity is Ped ped)
            {
                Function.Call(Hash.SET_PED_TO_RAGDOLL, ped.Handle, 2000, 2000, 0, false, false, false);
                Function.Call(Hash.SET_ENTITY_VELOCITY, ped.Handle, targetVel.X, targetVel.Y, targetVel.Z);
                ped.LocalRotationVelocity = Vector3.Zero;

                // Apply direct impulse to ragdoll center of mass so dead ragdoll skeletons glide smoothly
                Function.Call(Hash.APPLY_FORCE_TO_ENTITY, ped.Handle, 3, targetVel.X * 0.8f, targetVel.Y * 0.8f, targetVel.Z * 0.8f, 0f, 0f, 0f, 0, false, true, true, false, true);
            }
            // 2. Vehicle & Prop smooth velocity positioning (eliminates jitter and oscillations)
            else
            {
                Function.Call(Hash.SET_ENTITY_VELOCITY, _heldEntity.Handle, targetVel.X, targetVel.Y, targetVel.Z);
                _heldEntity.LocalRotationVelocity = Vector3.Zero;
            }

            // 3. Visuals: Energy Beam from right hand
            Vector3 handPos = player.Bones[(Bone)57005].Position;
            Function.Call(Hash.DRAW_LINE, handPos.X, handPos.Y, handPos.Z, currentPos.X, currentPos.Y, currentPos.Z, 0, 200, 255, 220);
            Function.Call(Hash.DRAW_LIGHT_WITH_RANGE, currentPos.X, currentPos.Y, currentPos.Z, 0, 200, 255, 2.0f, 1.5f);
        }

        private void ThrowHeldEntity()
        {
            if (_heldEntity == null || !_heldEntity.Exists()) return;

            Vector3 throwDir = GameplayCamera.Direction;

            if (_heldEntity is Ped ped)
            {
                Vector3 impulse = throwDir * PedThrowForce + new Vector3(0, 0, 5.0f);
                Function.Call(Hash.SET_PED_TO_RAGDOLL, ped.Handle, 8000, 8000, 0, false, false, false);
                Function.Call(Hash.SET_ENTITY_VELOCITY, ped.Handle, throwDir.X * ThrowVelocity, throwDir.Y * ThrowVelocity, throwDir.Z * ThrowVelocity);
                Function.Call(Hash.APPLY_FORCE_TO_ENTITY, ped.Handle, 3, impulse.X, impulse.Y, impulse.Z, 0f, 0f, 0f, 0, false, true, true, false, true);
            }
            else
            {
                Vector3 throwVel = throwDir * ThrowVelocity;
                Function.Call(Hash.SET_ENTITY_VELOCITY, _heldEntity.Handle, throwVel.X, throwVel.Y, throwVel.Z);
            }

            Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "SHOOT_HIT", "HUD_FRONTEND_DEFAULT_SOUNDSET", 1);
            Notifier.Show("~r~[Телекинез]~s~ Кинетический бросок!");

            _heldEntity = null;
        }

        public void Release(bool throwObject = false)
        {
            if (_heldEntity != null && _heldEntity.Exists())
            {
                if (throwObject)
                {
                    ThrowHeldEntity();
                }
                else
                {
                    // Gentle drop without leftover slingshot momentum
                    Function.Call(Hash.SET_ENTITY_VELOCITY, _heldEntity.Handle, 0f, 0f, -0.2f);
                    _heldEntity.LocalRotationVelocity = Vector3.Zero;

                    if (_heldEntity is Ped ped)
                    {
                        Function.Call(Hash.SET_PED_TO_RAGDOLL, ped.Handle, 2000, 2000, 0, false, false, false);
                        Function.Call(Hash.APPLY_FORCE_TO_ENTITY, ped.Handle, 3, 0f, 0f, -0.2f, 0f, 0f, 0f, 0, false, true, true, false, true);
                    }
                    _heldEntity = null;
                }
            }
            _heldEntity = null;
        }
    }
}
