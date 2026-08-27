using System;
using GTA;
using GTA.Native;
using gta.Core;

namespace gta.Player
{
    internal sealed class BulletTimeService
    {
        private readonly float[] _speeds = new float[] { 0.30f, 0.12f, 0.04f };
        private int _currentSpeedIndex = -1; // -1 = Disabled

        public bool IsActive => _currentSpeedIndex >= 0;

        public void Toggle()
        {
            _currentSpeedIndex++;
            if (_currentSpeedIndex >= _speeds.Length)
            {
                _currentSpeedIndex = -1;
            }

            ApplyState();
        }

        public void Disable()
        {
            if (_currentSpeedIndex == -1) return;
            _currentSpeedIndex = -1;
            ApplyState();
        }

        private void ApplyState()
        {
            if (_currentSpeedIndex == -1)
            {
                Game.TimeScale = 1.0f;
                // Re-enable dynamic chase music
                Function.Call(Hash.SET_AUDIO_FLAG, "WantedMusicDisabled", false);
                Function.Call(Hash.CANCEL_MUSIC_EVENT, "GLOBAL_KILL_MUSIC");
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "Slow_Mo_End", "SPECIAL_ABILITY_SOUNDSET", true);
                Notifier.Show("Замедление времени: Выкл");
                return;
            }

            float targetScale = _speeds[_currentSpeedIndex];
            Game.TimeScale = targetScale;

            // Silence action/chase music so weapon and environment sounds are loud and clear
            Function.Call(Hash.SET_AUDIO_FLAG, "WantedMusicDisabled", true);
            Function.Call(Hash.TRIGGER_MUSIC_EVENT, "GLOBAL_KILL_MUSIC");
            Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "Slow_Mo_Start", "SPECIAL_ABILITY_SOUNDSET", true);

            string modeName;
            switch (_currentSpeedIndex)
            {
                case 0:
                    modeName = "0.30x (Max Payne)";
                    break;
                case 1:
                    modeName = "0.12x (Matrix)";
                    break;
                case 2:
                    modeName = "0.04x (Freeze)";
                    break;
                default:
                    modeName = $"{targetScale:0.00}x";
                    break;
            }

            Notifier.Show($"Замедление: {modeName}");
        }

        public void Update()
        {
            if (!IsActive)
            {
                return;
            }

            var player = Game.Player.Character;
            if (player == null || !player.Exists() || player.IsDead)
            {
                Disable();
                return;
            }

            // Enforce time scale
            float currentTargetScale = _speeds[_currentSpeedIndex];
            if (Math.Abs(Game.TimeScale - currentTargetScale) > 0.01f)
            {
                Game.TimeScale = currentTargetScale;
            }

            // Enforce maximum accuracy and reduce weapon recoil/camera shake during slow motion
            player.Accuracy = 100;
            Function.Call(Hash.SET_PED_ACCURACY, player.Handle, 100);
            Function.Call(Hash.SET_GAMEPLAY_CAM_SHAKE_AMPLITUDE, 0.0f);

            // Keep police chase music suppressed while bullet time is running
            Function.Call(Hash.SET_AUDIO_FLAG, "WantedMusicDisabled", true);
        }

        public void Abort()
        {
            Disable();
        }
    }
}
