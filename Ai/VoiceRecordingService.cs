using System;
using System.IO;
using NAudio.Wave;

namespace gta.Ai
{
    public class VoiceRecordingService
    {
        private WaveInEvent _waveIn;
        private WaveFileWriter _writer;
        private string _tempFilePath;

        public bool IsRecording { get; private set; }

        public void StartRecording()
        {
            if (IsRecording) return;

            _tempFilePath = Path.Combine(Path.GetTempPath(), $"gta_voice_{Guid.NewGuid()}.wav");
            
            try
            {
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 1) // 16kHz Mono is good for Whisper
                };

                _writer = new WaveFileWriter(_tempFilePath, _waveIn.WaveFormat);

                _waveIn.DataAvailable += (s, a) =>
                {
                    _writer.Write(a.Buffer, 0, a.BytesRecorded);
                };

                _waveIn.RecordingStopped += (s, a) =>
                {
                    _writer?.Dispose();
                    _writer = null;
                    _waveIn?.Dispose();
                    _waveIn = null;
                };

                _waveIn.StartRecording();
                IsRecording = true;
            }
            catch (Exception ex)
            {
                Core.Notifier.Show($"Ошибка микрофона: {ex.Message}");
                IsRecording = false;
            }
        }

        public string StopRecording()
        {
            if (!IsRecording) return null;

            IsRecording = false;
            _waveIn?.StopRecording();
            return _tempFilePath;
        }
    }
}
