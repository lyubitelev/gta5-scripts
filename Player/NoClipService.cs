using System;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;

namespace gta.Player
{
    internal sealed class NoClipService
    {
        private const float NormalSpeed = 32f;
        private const float FastSpeed = 110f;
        private const float SlowSpeed = 8f;

        private bool _isEnabled;
        private Entity _activeEntity;
        private DateTime _lastUpdateUtc = DateTime.MinValue;

        public bool IsEnabled => _isEnabled;

        public void Toggle()
        {
            if (_isEnabled)
            {
                Disable();
                return;
            }

            Enable();
        }

        public void Update()
        {
            if (!_isEnabled)
            {
                return;
            }

            var target = GetTargetEntity();
            if (target == null || !target.Exists())
            {
                return;
            }

            ActivateEntity(target);
            KeepNoClipState(target);

            var movement = GetMovementDirection();
            if (movement.Length() <= 0.001f)
            {
                _lastUpdateUtc = DateTime.UtcNow;
                return;
            }

            var speed = GetSpeed();
            var deltaSeconds = GetDeltaSeconds();
            var position = target.Position + Normalize(movement) * speed * deltaSeconds;

            Function.Call(
                Hash.SET_ENTITY_COORDS_NO_OFFSET,
                target.Handle,
                position.X,
                position.Y,
                position.Z,
                true,
                true,
                true);
        }

        private void Enable()
        {
            _isEnabled = true;
            _lastUpdateUtc = DateTime.UtcNow;
            Notifier.Show("NoClip включен");
        }

        private void Disable()
        {
            _isEnabled = false;
            _lastUpdateUtc = DateTime.MinValue;
            RestoreActiveEntity();
            Notifier.Show("NoClip выключен");
        }

        private void ActivateEntity(Entity entity)
        {
            if (_activeEntity != null && _activeEntity.Exists() && _activeEntity.Handle == entity.Handle)
            {
                return;
            }

            RestoreActiveEntity();
            _activeEntity = entity;
            KeepNoClipState(entity);
        }

        private void RestoreActiveEntity()
        {
            if (_activeEntity == null || !_activeEntity.Exists())
            {
                _activeEntity = null;
                return;
            }

            Function.Call(Hash.SET_ENTITY_COLLISION, _activeEntity.Handle, true, true);
            Function.Call(Hash.FREEZE_ENTITY_POSITION, _activeEntity.Handle, false);
            Function.Call(Hash.SET_ENTITY_HAS_GRAVITY, _activeEntity.Handle, true);
            Function.Call(Hash.SET_ENTITY_VELOCITY, _activeEntity.Handle, 0f, 0f, 0f);

            var player = Game.Player.Character;
            if (player != null && player.Exists())
            {
                Function.Call(Hash.SET_PED_CAN_RAGDOLL, player.Handle, true);
            }

            _activeEntity = null;
        }

        private static void KeepNoClipState(Entity entity)
        {
            Function.Call(Hash.SET_ENTITY_COLLISION, entity.Handle, false, false);
            Function.Call(Hash.FREEZE_ENTITY_POSITION, entity.Handle, true);
            Function.Call(Hash.SET_ENTITY_HAS_GRAVITY, entity.Handle, false);
            Function.Call(Hash.SET_ENTITY_VELOCITY, entity.Handle, 0f, 0f, 0f);

            var player = Game.Player.Character;
            if (player != null && player.Exists())
            {
                Function.Call(Hash.SET_PED_CAN_RAGDOLL, player.Handle, false);
            }
        }

        private static Entity GetTargetEntity()
        {
            var player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                return null;
            }

            var vehicle = player.CurrentVehicle;
            return vehicle != null && vehicle.Exists()
                ? (Entity)vehicle
                : player;
        }

        private static Vector3 GetMovementDirection()
        {
            var direction = new Vector3(0f, 0f, 0f);
            var forward = GameplayCamera.Direction;
            var right = Normalize(new Vector3(forward.Y, -forward.X, 0f));

            if (Game.IsKeyPressed(Keys.W))
            {
                direction += forward;
            }

            if (Game.IsKeyPressed(Keys.S))
            {
                direction -= forward;
            }

            if (Game.IsKeyPressed(Keys.D))
            {
                direction += right;
            }

            if (Game.IsKeyPressed(Keys.A))
            {
                direction -= right;
            }

            if (Game.IsKeyPressed(Keys.Space))
            {
                direction += new Vector3(0f, 0f, 1f);
            }

            if (Game.IsKeyPressed(Keys.ControlKey))
            {
                direction -= new Vector3(0f, 0f, 1f);
            }

            return direction;
        }

        private static float GetSpeed()
        {
            if (Game.IsKeyPressed(Keys.ShiftKey))
            {
                return FastSpeed;
            }

            if (Game.IsKeyPressed(Keys.Menu))
            {
                return SlowSpeed;
            }

            return NormalSpeed;
        }

        private float GetDeltaSeconds()
        {
            var now = DateTime.UtcNow;
            if (_lastUpdateUtc == DateTime.MinValue)
            {
                _lastUpdateUtc = now;
                return 0.016f;
            }

            var delta = (float)(now - _lastUpdateUtc).TotalSeconds;
            _lastUpdateUtc = now;

            if (delta < 0.001f)
            {
                return 0.001f;
            }

            return delta > 0.1f ? 0.1f : delta;
        }

        private static Vector3 Normalize(Vector3 value)
        {
            var length = value.Length();
            if (length <= 0.001f)
            {
                return new Vector3(0f, 0f, 0f);
            }

            return new Vector3(value.X / length, value.Y / length, value.Z / length);
        }
    }
}
