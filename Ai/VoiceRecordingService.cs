using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace gta.Ai
{
    public class VoiceRecordingService
    {
        private WaveInEvent _waveIn;
        private WaveFileWriter _writer;
        private string _tempFilePath;
        private TaskCompletionSource<string> _recordingTcs;
        private readonly object _lock = new object();

        public bool IsRecording { get; private set; }

        public void StartRecording()
        {
            lock (_lock)
            {
                if (IsRecording) return;

                _tempFilePath = Path.Combine(Path.GetTempPath(), $"gta_voice_{Guid.NewGuid()}.wav");
                _recordingTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

                try
                {
                    _waveIn = new WaveInEvent
                    {
                        WaveFormat = new WaveFormat(16000, 1) // 16kHz Mono is good for Whisper
                    };

                    _writer = new WaveFileWriter(_tempFilePath, _waveIn.WaveFormat);

                    _waveIn.DataAvailable += (s, a) =>
                    {
                        lock (_lock)
                        {
                            try
                            {
                                _writer?.Write(a.Buffer, 0, a.BytesRecorded);
                            }
                            catch (Exception ex)
                            {
                                AiLogger.Log("RECORD", $"Error writing audio buffer: {ex.Message}");
                            }
                        }
                    };

                    _waveIn.RecordingStopped += (s, a) =>
                    {
                        var tcs = _recordingTcs;
                        var filePath = _tempFilePath;
                        var ex = a.Exception;

                        lock (_lock)
                        {
                            try
                            {
                                _writer?.Flush();
                            }
                            catch { }

                            try
                            {
                                _writer?.Dispose();
                            }
                            catch (Exception dex)
                            {
                                AiLogger.Log("RECORD", $"Error disposing writer: {dex.Message}");
                                if (ex == null) ex = dex;
                            }
                            finally
                            {
                                _writer = null;
                            }

                            try
                            {
                                _waveIn?.Dispose();
                            }
                            catch (Exception wex)
                            {
                                AiLogger.Log("RECORD", $"Error disposing waveIn: {wex.Message}");
                            }
                            finally
                            {
                                _waveIn = null;
                            }
                        }

                        if (ex != null)
                        {
                            AiLogger.Log("RECORD", $"Recording stopped with error: {ex.Message}");
                            tcs?.TrySetException(ex);
                        }
                        else
                        {
                            tcs?.TrySetResult(filePath);
                        }
                    };

                    _waveIn.StartRecording();
                    IsRecording = true;
                }
                catch (Exception ex)
                {
                    AiLogger.Log("RECORD", $"Failed to start recording: {ex.Message}");
                    Core.Notifier.Show($"Ошибка микрофона: {ex.Message}");
                    IsRecording = false;

                    try { _writer?.Dispose(); } catch { }
                    _writer = null;
                    try { _waveIn?.Dispose(); } catch { }
                    _waveIn = null;

                    _recordingTcs?.TrySetException(ex);
                }
            }
        }

        public Task<string> StopRecordingAsync()
        {
            lock (_lock)
            {
                if (!IsRecording || _waveIn == null)
                {
                    return Task.FromResult<string>(null);
                }

                IsRecording = false;
                var task = _recordingTcs != null ? _recordingTcs.Task : Task.FromResult<string>(_tempFilePath);

                try
                {
                    _waveIn.StopRecording();
                }
                catch (Exception ex)
                {
                    AiLogger.Log("RECORD", $"Error calling StopRecording: {ex.Message}");
                    _recordingTcs?.TrySetException(ex);
                }

                return task;
            }
        }

        public void Abort()
        {
            string fileToDelete = null;
            lock (_lock)
            {
                IsRecording = false;
                fileToDelete = _tempFilePath;
                _tempFilePath = null;
                _recordingTcs?.TrySetCanceled();

                try
                {
                    _waveIn?.StopRecording();
                }
                catch { }

                try
                {
                    _writer?.Dispose();
                }
                catch { }
                finally
                {
                    _writer = null;
                }

                try
                {
                    _waveIn?.Dispose();
                }
                catch { }
                finally
                {
                    _waveIn = null;
                }
            }

            if (!string.IsNullOrEmpty(fileToDelete))
            {
                try
                {
                    if (File.Exists(fileToDelete)) File.Delete(fileToDelete);
                }
                catch { }
            }
        }
    }
}
