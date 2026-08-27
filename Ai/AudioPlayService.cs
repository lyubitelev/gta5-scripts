using System;
using GTA;
using NAudio.Wave;

namespace gta.Ai
{
    public class AudioPlayService
    {
        private WaveOutEvent _outputDevice;
        private AudioFileReader _audioFile;
        private Ped _speakingPed;

        public void PlayAudioForPed(string audioFilePath, Ped ped)
        {
            StopAudio();

            try
            {
                _outputDevice = new WaveOutEvent();
                _audioFile = new AudioFileReader(audioFilePath);
                _outputDevice.Init(_audioFile);
                _outputDevice.Play();
                _speakingPed = ped;

                if (ped != null && ped.Exists() && !ped.IsDead)
                {
                    GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, "mp_facial");
                    GTA.Native.Function.Call(GTA.Native.Hash.PLAY_FACIAL_ANIM, ped.Handle, "mic_chatter", "mp_facial");
                }
            }
            catch (Exception ex)
            {
                Core.Notifier.Show($"Ошибка аудио: {ex.Message}");
            }
        }

        public void StopAudio()
        {
            if (_outputDevice != null)
            {
                _outputDevice.Stop();
                _outputDevice.Dispose();
                _outputDevice = null;
            }
            if (_audioFile != null)
            {
                _audioFile.Dispose();
                _audioFile = null;
            }

            if (_speakingPed != null && _speakingPed.Exists())
            {
                GTA.Native.Function.Call(GTA.Native.Hash.PLAY_FACIAL_ANIM, _speakingPed.Handle, "mood_normal_1", "mp_facial");
                _speakingPed = null;
            }
        }

        public void Update()
        {
            // Труп не разговаривает: если говорящий пед умер во время озвучки — глушим.
            if (_speakingPed != null && _speakingPed.Exists() && _speakingPed.IsDead)
            {
                StopAudio();
                return;
            }

            if (_outputDevice != null && _outputDevice.PlaybackState == PlaybackState.Playing)
            {
                if (_speakingPed != null && _speakingPed.Exists())
                {
                    var dist = Game.Player.Character.Position.DistanceTo(_speakingPed.Position);
                    var maxDist = 20f;
                    var volume = 1f - (dist / maxDist);
                    if (volume < 0) volume = 0;
                    if (volume > 1) volume = 1;

                    if (_audioFile != null)
                    {
                        _audioFile.Volume = volume;
                    }
                }
            }
            else if (_outputDevice != null && _outputDevice.PlaybackState == PlaybackState.Stopped)
            {
                StopAudio();
            }
        }
    }
}
