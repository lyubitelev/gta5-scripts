using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace gta.Ai
{
    public class VoiceRecordingService
    {
        private sealed class RecordingSession
        {
            public WaveInEvent WaveIn { get; set; }
            public WaveFileWriter Writer { get; set; }
            public string FilePath { get; }
            public TaskCompletionSource<string> Completion { get; }

            private bool _isHandedOver;
            private bool _isCleanedUp;
            private readonly object _sessionLock = new object();

            public RecordingSession(string filePath)
            {
                FilePath = filePath;
                Completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public bool TryCompleteWithSuccess()
            {
                lock (_sessionLock)
                {
                    if (_isCleanedUp) return false;

                    if (Completion.TrySetResult(FilePath))
                    {
                        _isHandedOver = true;
                        return true;
                    }
                    else
                    {
                        CleanupFileInternal();
                        return false;
                    }
                }
            }

            public void CompleteWithError(Exception ex)
            {
                lock (_sessionLock)
                {
                    CleanupFileInternal();
                    Completion.TrySetException(ex ?? new Exception("Unknown recording error"));
                }
            }

            public void CancelAndCleanup()
            {
                lock (_sessionLock)
                {
                    Completion.TrySetCanceled();
                    if (!_isHandedOver)
                    {
                        CleanupFileInternal();
                    }
                }
            }

            public void CleanupIfNotHandedOver()
            {
                lock (_sessionLock)
                {
                    if (!_isHandedOver)
                    {
                        CleanupFileInternal();
                    }
                }
            }

            private void CleanupFileInternal()
            {
                if (_isCleanedUp) return;
                _isCleanedUp = true;

                if (!string.IsNullOrEmpty(FilePath))
                {
                    try
                    {
                        if (File.Exists(FilePath)) File.Delete(FilePath);
                    }
                    catch (Exception ex)
                    {
                        AiLogger.Log("RECORD", $"Failed to delete session temp file '{FilePath}': {ex.Message}");
                    }
                }
            }
        }

        private readonly object _lock = new object();
        private RecordingSession _currentSession;
        private RecordingSession _stoppingSession;

        public bool IsRecording
        {
            get
            {
                lock (_lock)
                {
                    return _currentSession != null;
                }
            }
        }

        public bool IsBusy
        {
            get
            {
                lock (_lock)
                {
                    return _currentSession != null || _stoppingSession != null;
                }
            }
        }

        public void StartRecording()
        {
            lock (_lock)
            {
                if (_currentSession != null || _stoppingSession != null)
                {
                    AiLogger.Log("RECORD", "Cannot start recording: previous recording session is still active or finalizing.");
                    return;
                }

                var filePath = Path.Combine(Path.GetTempPath(), $"gta_voice_{Guid.NewGuid()}.wav");
                var session = new RecordingSession(filePath);
                _currentSession = session;

                try
                {
                    var waveIn = new WaveInEvent
                    {
                        WaveFormat = new WaveFormat(16000, 1) // 16kHz Mono is optimal for Whisper
                    };

                    var writer = new WaveFileWriter(filePath, waveIn.WaveFormat);

                    session.WaveIn = waveIn;
                    session.Writer = writer;

                    waveIn.DataAvailable += (s, a) =>
                    {
                        lock (session)
                        {
                            try
                            {
                                session.Writer?.Write(a.Buffer, 0, a.BytesRecorded);
                            }
                            catch (Exception ex)
                            {
                                AiLogger.Log("RECORD", $"Error writing audio buffer: {ex.Message}");
                            }
                        }
                    };

                    waveIn.RecordingStopped += (s, a) =>
                    {
                        var ex = a.Exception;

                        lock (session)
                        {
                            try
                            {
                                session.Writer?.Flush();
                            }
                            catch { }

                            try
                            {
                                session.Writer?.Dispose();
                            }
                            catch (Exception dex)
                            {
                                AiLogger.Log("RECORD", $"Error disposing writer: {dex.Message}");
                                if (ex == null) ex = dex;
                            }
                            finally
                            {
                                session.Writer = null;
                            }

                            try
                            {
                                session.WaveIn?.Dispose();
                            }
                            catch (Exception wex)
                            {
                                AiLogger.Log("RECORD", $"Error disposing waveIn: {wex.Message}");
                            }
                            finally
                            {
                                session.WaveIn = null;
                            }
                        }

                        lock (_lock)
                        {
                            if (_stoppingSession == session) _stoppingSession = null;
                            if (_currentSession == session) _currentSession = null;
                        }

                        if (ex != null)
                        {
                            AiLogger.Log("RECORD", $"Recording stopped with error: {ex.Message}");
                            session.CompleteWithError(ex);
                        }
                        else
                        {
                            session.TryCompleteWithSuccess();
                        }
                    };

                    waveIn.StartRecording();
                }
                catch (Exception ex)
                {
                    AiLogger.Log("RECORD", $"Failed to start recording: {ex.Message}");
                    Core.Notifier.Show($"Ошибка микрофона: {ex.Message}");

                    lock (session)
                    {
                        try { session.Writer?.Dispose(); } catch { }
                        session.Writer = null;
                        try { session.WaveIn?.Dispose(); } catch { }
                        session.WaveIn = null;
                    }

                    session.CompleteWithError(ex);

                    if (_currentSession == session) _currentSession = null;
                    if (_stoppingSession == session) _stoppingSession = null;
                }
            }
        }

        public Task<string> StopRecordingAsync()
        {
            RecordingSession sessionToStop = null;
            lock (_lock)
            {
                if (_currentSession == null)
                {
                    return Task.FromResult<string>(null);
                }

                sessionToStop = _currentSession;
                _stoppingSession = sessionToStop;
                _currentSession = null;
            }

            try
            {
                sessionToStop.WaveIn?.StopRecording();
            }
            catch (Exception ex)
            {
                AiLogger.Log("RECORD", $"Error calling StopRecording: {ex.Message}");
                lock (sessionToStop)
                {
                    try { sessionToStop.Writer?.Dispose(); } catch { }
                    sessionToStop.Writer = null;
                    try { sessionToStop.WaveIn?.Dispose(); } catch { }
                    sessionToStop.WaveIn = null;
                }
                sessionToStop.CompleteWithError(ex);

                lock (_lock)
                {
                    if (_stoppingSession == sessionToStop) _stoppingSession = null;
                }
            }

            return sessionToStop.Completion.Task;
        }

        public void Abort()
        {
            RecordingSession sessionA = null;
            RecordingSession sessionB = null;

            lock (_lock)
            {
                sessionA = _currentSession;
                sessionB = _stoppingSession;
                _currentSession = null;
                _stoppingSession = null;
            }

            foreach (var session in new[] { sessionA, sessionB })
            {
                if (session == null) continue;

                try
                {
                    session.WaveIn?.StopRecording();
                }
                catch { }

                lock (session)
                {
                    try { session.Writer?.Dispose(); } catch { }
                    session.Writer = null;
                    try { session.WaveIn?.Dispose(); } catch { }
                    session.WaveIn = null;
                }

                session.CancelAndCleanup();
            }
        }
    }
}
