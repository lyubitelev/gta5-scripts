using System;
using System.IO;
using GTA;
using NAudio.Wave;

namespace gta.Ai
{
    public class AudioPlayService
    {
        private WaveOutEvent _outputDevice;
        private AudioFileReader _audioFile;
        private Ped _speakingPed;
        private string _currentAudioFilePath;

        public void PlayAudioForPed(string audioFilePath, Ped ped)
        {
            StopAudio();

            try
            {
                _currentAudioFilePath = audioFilePath;
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
                AiLogger.Log("AUDIO", $"Ошибка воспроизведения: {ex.Message}");
                Core.Notifier.Show($"Ошибка аудио: {ex.Message}");
                StopAudio();
            }
        }

        public void StopAudio()
        {
            if (_outputDevice != null)
            {
                try { _outputDevice.Stop(); } catch { }
                try { _outputDevice.Dispose(); } catch { }
                _outputDevice = null;
            }

            if (_audioFile != null)
            {
                try { _audioFile.Dispose(); } catch { }
                _audioFile = null;
            }

            CleanupCurrentAudioFile();

            if (_speakingPed != null && _speakingPed.Exists())
            {
                try
                {
                    GTA.Native.Function.Call(GTA.Native.Hash.PLAY_FACIAL_ANIM, _speakingPed.Handle, "mood_normal_1", "mp_facial");
                }
                catch { }
                _speakingPed = null;
            }
        }

        private void CleanupCurrentAudioFile()
        {
            if (!string.IsNullOrEmpty(_currentAudioFilePath))
            {
                try
                {
                    if (File.Exists(_currentAudioFilePath))
                    {
                        File.Delete(_currentAudioFilePath);
                    }
                }
                catch (Exception ex)
                {
                    AiLogger.Log("AUDIO", $"Failed to delete temp audio file '{_currentAudioFilePath}': {ex.Message}");
                }
                _currentAudioFilePath = null;
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
