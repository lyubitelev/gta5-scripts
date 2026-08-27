using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using gta.Core;
using gta.Peds;

namespace gta.Ai
{
    internal class AiController
    {
        private readonly AiSettings _settings;
        private readonly VoiceRecordingService _recordingService;
        private readonly AiApiService _apiService;
        private readonly NpcManager _npcManager;
        private readonly AudioPlayService _audioPlayService;
        private readonly PedQueryService _pedQuery;
        
        private Ped _interactingPed;
        private bool _isProcessing;
        private bool _isKeyHandled;
        private DateTime _recordStartTime;
        private DateTime _lastCleanup = DateTime.MinValue;
        private CancellationTokenSource _currentCts;
        private volatile bool _isAborted;

        // Окно вовлечения в разговор: удерживаем собеседника лицом к игроку, пока оно активно
        private Ped _engagedPed;
        private bool _engagedShouldHold;
        private DateTime _engageUntil;
        private DateTime _nextEngageRefresh = DateTime.MinValue;

        // Проактив (только именные): кулдауны и состояние "встречи"
        private readonly Dictionary<string, DateTime> _proactiveCooldown = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, bool> _wasNear = new Dictionary<string, bool>();
        private DateTime _nextProactiveScan = DateTime.MinValue;
        private DateTime _nextGlobalProactive = DateTime.MinValue;
        private bool _prevPlayerArmed;
        private readonly Dictionary<string, DateTime> _threatCooldown = new Dictionary<string, DateTime>();
        private int _prevWanted;
        private float _prevVehBodyHealth = -1f;
        private readonly Random _random = new Random();

        // Последователи (FOLLOW): держим, чтобы сажать их в машину при «поехали кататься»
        private readonly HashSet<int> _followers = new HashSet<int>();
        private DateTime _nextFollowerTick = DateTime.MinValue;

        // Готовые фразы прощания (без LLM) — тривиальная реакция при уходе игрока
        private static readonly string[] FarewellLines =
        {
            "Бывай.", "Ещё увидимся.", "Давай, не пропадай.", "Удачи, приятель.", "Ладно, до встречи."
        };

        // Whisper отклоняет запись короче 0.1с; держим запас, чтобы не слать заведомо пустое
        private const double MinRecordingSeconds = 0.3;
        private const double CleanupIntervalSeconds = 30.0;
        private const int MaxVehicleSeatIndex = 14;
        private const int NoTurretSeat = int.MinValue;
        private const double EngageWindowSeconds = 15.0;   // сколько держим собеседника после последнего обращения
        private const double EngageRefreshSeconds = 1.5;   // как часто пере-выдаём "стой и смотри"
        private const float EngageReleaseDistance = 14f;   // отошёл дальше — отпускаем
        private const int MemoryFoldThreshold = 20;        // при стольких репликах сворачиваем старое в сводку
        private const int MemoryRecentKeep = 12;           // сколько последних реплик оставляем дословно
        private const double ProactiveScanSeconds = 1.5;            // как часто опрашиваем триггеры
        private const double ProactiveGlobalCooldownSeconds = 12.0; // не чаще раза на весь мир
        private const double ProactivePerCharCooldownSeconds = 50.0;// кулдаун на конкретного персонажа
        private const float ProactiveScanRadius = 12f;             // радиус поиска именных
        private const float ProactiveGreetRange = 6f;              // ближе — приветствие
        private const float ProactiveResetRange = 10f;             // дальше — "встреча" сбрасывается
        private const float ProactiveReactRange = 9f;              // реакция на действия игрока в этом радиусе
        private const float ProactiveCompanionRange = 8f;          // радиус комментария компаньона
        private const double ProactiveCompanionChance = 0.15;      // вероятность комментария компаньона за опрос
        private const double ProactiveThreatCooldownSeconds = 15.0;// короткий кулдаун на реакцию-угрозу

        public AiController(PedQueryService pedQuery)
        {
            _settings = AiSettings.Load("scripts/ai_settings.json"); // Assuming ScriptHookV path
            _recordingService = new VoiceRecordingService();
            _apiService = new AiApiService(_settings);
            _npcManager = new NpcManager(_settings);
            _audioPlayService = new AudioPlayService();
            _pedQuery = pedQuery;
        }

        public void Update()
        {
            if (_isAborted) return;

            _audioPlayService.Update();
            MaintainEngagement();
            MaybeProactive();
            ManageFollowers();

            if ((DateTime.Now - _lastCleanup).TotalSeconds >= CleanupIntervalSeconds)
            {
                _npcManager.CleanupDeadIdentities();
                _lastCleanup = DateTime.Now;
            }
        }

        private sealed class QueuedAiAction
        {
            public Action ExecuteAction { get; set; }
            public string AudioFilePath { get; set; }

            public void Execute()
            {
                ExecuteAction?.Invoke();
            }

            public void Cancel()
            {
                if (!string.IsNullOrEmpty(AudioFilePath))
                {
                    CleanupFile(AudioFilePath);
                    AudioFilePath = null;
                }
            }
        }

        public void HandleKeyDown(KeyEventArgs e)
        {
            if (_isAborted) return;

            if (e.KeyCode == Keys.Z)
            {
                if (_isKeyHandled) return;
                _isKeyHandled = true;

                AiLogger.Log("INPUT", $"KeyDown Z. Processing: {_isProcessing}, Recording: {_recordingService.IsRecording}, Busy: {_recordingService.IsBusy}");

                // Z во время обработки = "хочу говорить сейчас": отменяем висящий запрос и начинаем заново тем же нажатием
                if (_isProcessing)
                {
                    AiLogger.Log("INPUT", "Cancelling in-flight request (Z pressed).");
                    try { _currentCts?.Cancel(); } catch { }
                    _isProcessing = false;
                    Notifier.Show("Отменено");
                }

                if (!_recordingService.IsBusy)
                {
                    var targetPed = _pedQuery.GetClosestPed(10.0f);
                    if (targetPed == null)
                    {
                        AiLogger.Log("INPUT", "No ped found within 10.0 meters.");
                        Notifier.Show("Поблизости нет прохожих (подойдите ближе)");
                        return;
                    }
                    if (!targetPed.Exists())
                    {
                        AiLogger.Log("INPUT", "Target ped exists is false.");
                        Notifier.Show("Поблизости нет прохожих (подойдите ближе)");
                        return;
                    }

                    _interactingPed = targetPed;
                    var identity = _npcManager.GetOrCreateIdentity(targetPed);
                    
                    var relInt = Function.Call<int>(Hash.GET_RELATIONSHIP_BETWEEN_PEDS, targetPed.Handle, Game.Player.Character.Handle);
                    // Липкий флаг HasBeenDamagedBy намеренно НЕ учитываем — иначе пед "навсегда боится"
                    bool isHostileOrPanicked = targetPed.IsInCombatAgainst(Game.Player.Character) ||
                                                relInt == 4 || // Dislike
                                                relInt == 5 || // Hate
                                                Function.Call<bool>(Hash.IS_PED_FLEEING, targetPed.Handle) ||
                                                Function.Call<int>(Hash.GET_PED_ALERTNESS, targetPed.Handle) == 3;

                    // Мгновенно вовлекаем мирного педа: разворачиваем к игроку и удерживаем окном вовлечения,
                    // чтобы он не уходил, пока крутится сеть и идёт разговор. Не трогаем садящегося в машину,
                    // союзника (им рулит CompanionService) и враждебного/паникующего (их решает матрица).
                    if (!isHostileOrPanicked && !targetPed.IsEnteringVehicle && !IsAlly(targetPed))
                    {
                        BeginEngagement(targetPed);
                    }

                    AiLogger.Log("RECORD", $"Start recording voice for NPC {identity.Name} (Handle: {targetPed.Handle})");
                    Notifier.Show($"[{identity.Name}] Говорите...");
                    _recordingService.StartRecording();
                    _recordStartTime = DateTime.Now;
                }
            }
        }

        public void HandleKeyUp(KeyEventArgs e)
        {
            if (_isAborted) return;

            if (e.KeyCode == Keys.Z)
            {
                _isKeyHandled = false;
                AiLogger.Log("INPUT", $"KeyUp Z. Recording: {_recordingService.IsRecording}");
                if (_recordingService.IsRecording)
                {
                    var recordedSeconds = (DateTime.Now - _recordStartTime).TotalSeconds;
                    var stopRecordTask = _recordingService.StopRecordingAsync();

                    if (recordedSeconds < MinRecordingSeconds)
                    {
                        AiLogger.Log("RECORD", $"Recording too short ({recordedSeconds:F2}s < {MinRecordingSeconds}s), ignoring.");
                        Notifier.Show("Слишком коротко — удерживайте Z дольше");
                        Task.Run(async () =>
                        {
                            try
                            {
                                var wav = await stopRecordTask;
                                CleanupFile(wav);
                            }
                            catch { }
                        });
                        return;
                    }

                    Notifier.Show("Обработка голоса...");
                    _isProcessing = true;
                    RefreshEngagement(); // держим собеседника, пока крутится сеть

                    // Безопасный сбор данных в основном потоке во избежание краша GTA5 (Access Violation)
                    if (_interactingPed != null && _interactingPed.Exists() && !_interactingPed.IsDead)
                    {
                        var identity = _npcManager.GetOrCreateIdentity(_interactingPed);
                        var player = Game.Player.Character;
                        int playerHealth = player.Exists() ? player.Health : 100;
                        int wantedLevel = Game.Player.Wanted.WantedLevel;
                        bool hasWeapon = player.Exists() && player.Weapons.Current.Hash != GTA.WeaponHash.Unarmed;
                        var state = GetNpcState(_interactingPed);
                        int pedHandle = _interactingPed.Handle;

                        // Один CancellationTokenSource на взаимодействие: его отменяет повторное нажатие Z
                        var cts = new CancellationTokenSource();
                        _currentCts = cts;

                        Task.Run(async () =>
                        {
                            string wavFile = null;
                            try
                            {
                                wavFile = await stopRecordTask;
                            }
                            catch (Exception ex)
                            {
                                AiLogger.Log("RECORD", $"Failed to stop/finalize recording: {ex.Message}");
                                _actionQueue.Enqueue(new QueuedAiAction
                                {
                                    ExecuteAction = () =>
                                    {
                                        if (_isAborted || cts != _currentCts) return;
                                        Notifier.Show($"Ошибка записи: {ex.Message}");
                                        _isProcessing = false;
                                    }
                                });
                                return;
                            }

                            if (string.IsNullOrEmpty(wavFile) || !File.Exists(wavFile))
                            {
                                AiLogger.Log("RECORD", "Recording file is null or does not exist, aborting.");
                                _actionQueue.Enqueue(new QueuedAiAction
                                {
                                    ExecuteAction = () =>
                                    {
                                        if (_isAborted || cts != _currentCts) return;
                                        _isProcessing = false;
                                    }
                                });
                                return;
                            }

                            await ProcessInteractionAsync(wavFile, null, false, null, pedHandle, identity, playerHealth, wantedLevel, hasWeapon, state, cts);
                        });
                    }
                    else
                    {
                        AiLogger.Log("PROCESS", "Interacting ped no longer exists at KeyUp, aborting.");
                        _isProcessing = false;
                        Task.Run(async () =>
                        {
                            try
                            {
                                var wav = await stopRecordTask;
                                CleanupFile(wav);
                            }
                            catch { }
                        });
                    }
                }
            }
        }

        private async Task ProcessInteractionAsync(string wavFile, string presetUserText, bool proactive, string cannedText, int pedHandle, NpcIdentity identity, int playerHealth, int wantedLevel, bool hasWeapon, NpcState state, CancellationTokenSource cts)
        {
            var token = cts.Token;
            string audioFile = null;
            QueuedAiAction queuedAction = null;
            try
            {
                AiLogger.Log("PROCESS", $"Starting {(proactive ? "PROACTIVE " : "")}interaction with {identity.Name} (Handle: {pedHandle})");

                AiResponse response;
                if (cannedText != null)
                {
                    // Готовая фраза без LLM (мгновенно/бесплатно) — тривиальные реакции вроде прощания.
                    response = new AiResponse { Text = cannedText, Action = "TALK", Stance = "STOP_AND_LISTEN" };
                    AiLogger.Log("PROACTIVE", $"Canned: \"{cannedText}\"");
                }
                else
                {
                    string userText;
                    if (proactive)
                    {
                        // Проактив: вместо речи игрока — описание события; STT не нужен.
                        userText = presetUserText;
                        AiLogger.Log("PROACTIVE", $"Event: \"{userText}\"");
                    }
                    else
                    {
                        // 1. STT
                        AiLogger.Log("STT", $"Transcribing audio {wavFile}");
                        userText = await _apiService.TranscribeAudioAsync(wavFile, token);
                        AiLogger.Log("STT", $"Result: \"{userText}\"");
                        identity.AddUserMessage(userText);
                    }

                    // 2. LLM
                    AiLogger.Log("LLM", $"Sending request to LLM (Provider: {_settings.ActiveProvider})");
                    response = await _apiService.GetNpcResponseAsync(
                        userText,
                        identity,
                        playerHealth,
                        wantedLevel,
                        hasWeapon,
                        state,
                        proactive,
                        token
                    );
                    AiLogger.Log("LLM", $"Result text: \"{response.Text}\", action: {response.Action}");
                }

                identity.AddNpcMessage(response.Text);

                // 3. TTS
                AiLogger.Log("TTS", $"Generating speech for text: \"{response.Text}\" using VoiceId: {identity.VoiceId}");
                audioFile = await _apiService.GenerateSpeechAsync(response.Text, identity.VoiceId, token);
                AiLogger.Log("TTS", $"Result audio file: {audioFile ?? "NULL"}");

                // Долговременная память известных персонажей: свернуть старое в сводку при превышении и сохранить на диск.
                if (!string.IsNullOrEmpty(identity.ModelKey))
                {
                    if (identity.ChatHistory.Count > MemoryFoldThreshold)
                    {
                        try
                        {
                            int foldCount = identity.ChatHistory.Count - MemoryRecentKeep;
                            var toFold = identity.ChatHistory.GetRange(0, foldCount);
                            var newSummary = await _apiService.SummarizeAsync(identity.Summary, toFold, token);
                            if (!string.IsNullOrEmpty(newSummary)) identity.Summary = newSummary;
                            identity.ChatHistory.RemoveRange(0, foldCount);
                            AiLogger.Log("MEMORY", $"Folded {foldCount} lines into summary for {identity.ModelKey}.");
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception mex) { AiLogger.Log("MEMORY", "Fold failed: " + mex.Message); }
                    }
                    _npcManager.PersistKnownCharacter(identity);
                }

                // Создаем queuedAction, который владеет файлом audioFile
                queuedAction = new QueuedAiAction
                {
                    AudioFilePath = audioFile
                };

                queuedAction.ExecuteAction = () =>
                {
                    // Защита от устаревших ответов: игрок мог отменить и начать новое взаимодействие или скрипт сброшен
                    if (_isAborted || cts != _currentCts || token.IsCancellationRequested)
                    {
                        AiLogger.Log("PROCESS", "Stale/cancelled result, ignoring (not applied).");
                        queuedAction.Cancel();
                        return;
                    }

                    var ped = Entity.FromHandle(pedHandle) as Ped;
                    if (ped != null && ped.Exists() && !ped.IsDead)
                    {
                        Notifier.Show($"[{identity.Name}]: {response.Text}");
                        if (!string.IsNullOrEmpty(queuedAction.AudioFilePath))
                        {
                            var fileToPlay = queuedAction.AudioFilePath;
                            queuedAction.AudioFilePath = null; // ownership передан в AudioPlayService
                            AiLogger.Log("PLAY", $"Playing audio file {fileToPlay} for {identity.Name}");
                            _audioPlayService.PlayAudioForPed(fileToPlay, ped);
                        }
                        AiLogger.Log("ACTION", $"Applying action: {response.Action} on {identity.Name}");
                        // Состояние снимаем заново на момент применения: между записью и ответом прошло несколько секунд
                        var freshState = GetNpcState(ped);
                        ApplyAction(ped, response.Action, response.Stance, freshState);
                    }
                    else
                    {
                        AiLogger.Log("PROCESS", $"Ped with handle {pedHandle} no longer exists, action not applied.");
                        queuedAction.Cancel();
                    }
                    _isProcessing = false;
                };

                // Queue to main thread
                _actionQueue.Enqueue(queuedAction);
            }
            catch (OperationCanceledException)
            {
                // Отличаем ручную отмену (игрок нажал Z → _currentCts.Cancel()) от таймаута этапа (сработал linked CancelAfter)
                bool userCancelled = cts.IsCancellationRequested;
                AiLogger.Log(userCancelled ? "CANCEL" : "TIMEOUT", userCancelled ? "User cancelled interaction." : "Stage timed out.");
                _actionQueue.Enqueue(new QueuedAiAction
                {
                    ExecuteAction = () =>
                    {
                        if (_isAborted || cts != _currentCts) return; // уже идёт новое взаимодействие — его состояние не трогаем
                        if (!userCancelled) Notifier.Show("Таймаут запроса");
                        _isProcessing = false;
                    }
                });
            }
            catch (Exception ex)
            {
                AiLogger.Log("ERROR", ex.ToString());
                _actionQueue.Enqueue(new QueuedAiAction
                {
                    ExecuteAction = () =>
                    {
                        if (_isAborted || cts != _currentCts) return; // устаревший запрос — игнорируем
                        Notifier.Show($"AI Error: {ex.Message}");
                        var ped = Entity.FromHandle(pedHandle) as Ped;
                        if (ped != null && ped.Exists())
                        {
                            bool wasHostileOrPanicked = state != null && (state.IsHostile || state.IsFleeing || state.IsCowering);
                            if (!wasHostileOrPanicked)
                            {
                                ped.Task.ClearAll();
                            }
                        }
                        _isProcessing = false;
                    }
                });
            }
            finally
            {
                // Cleanup WAV temp file
                CleanupFile(wavFile);

                // Если упало до создания queuedAction, а audioFile сгенерирован — очищаем
                if (queuedAction == null && !string.IsNullOrEmpty(audioFile))
                {
                    CleanupFile(audioFile);
                }
            }
        }

        private readonly System.Collections.Concurrent.ConcurrentQueue<QueuedAiAction> _actionQueue = new System.Collections.Concurrent.ConcurrentQueue<QueuedAiAction>();

        public void ProcessQueue()
        {
            while (_actionQueue.TryDequeue(out var action))
            {
                if (_isAborted)
                {
                    action?.Cancel();
                }
                else
                {
                    action?.Execute();
                }
            }
        }

        public void Abort()
        {
            _isAborted = true;

            try
            {
                _currentCts?.Cancel();
                _currentCts?.Dispose();
            }
            catch { }
            finally
            {
                _currentCts = null;
            }

            try
            {
                _recordingService?.Abort();
            }
            catch { }

            try
            {
                _audioPlayService?.StopAudio();
            }
            catch { }

            while (_actionQueue.TryDequeue(out var action))
            {
                action?.Cancel();
            }

            _isProcessing = false;
            _isKeyHandled = false;
            _interactingPed = null;
            _engagedPed = null;
        }

        private static void CleanupFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                AiLogger.Log("CLEANUP", $"Failed to delete temp file '{path}': {ex.Message}");
            }
        }

        private void ApplyAction(Ped ped, string action, string stance, NpcState state)
        {
            if (ped == null || !ped.Exists()) return;

            var isInVehicle = ped.IsInVehicle();
            var act = action?.ToUpper();
            bool isTalk = act == "TALK" || string.IsNullOrEmpty(act);

            // Направленное действие (FOLLOW/FLEE/...): прекращаем удержание разговора, чтобы оно не мешало действию.
            if (!isTalk)
            {
                StopEngagementTracking();
            }

            // TALK (или пусто): мирное общение. Снимаем залипшую панику/бегство и удерживаем педа
            // лицом к игроку через окно вовлечения, чтобы он не уходил во время и после разговора.
            if (isTalk)
            {
                // Союзников (компаньоны/гварды) НЕ глушим и не паркуем — иначе они перестают
                // защищать игрока. Их поведением продолжает рулить CompanionService.
                if (!IsAlly(ped))
                {
                    PacifyPed(ped);
                    ApplyStance(ped, stance);
                }
                return;
            }

            // FOLLOW: согласился идти/сесть — фиксируем поведение, иначе ванильный AI
            // (вооружённый игрок / паника) тут же угонит его в бегство.
            if (act == "FOLLOW" || act == "FOLLOW_RUN")
            {
                // Союзников не глушим (они и так не убегают); прочих фиксируем, чтобы не сбегали при посадке.
                if (!IsAlly(ped))
                {
                    PacifyPed(ped);
                }
                ped.Task.ClearAll();
                _followers.Add(ped.Handle); // ManageFollowers потом сам сажает их в машину, когда игрок за рулём

                var player = Game.Player.Character;
                if (player != null && player.Exists() && player.IsInVehicle())
                {
                    var vehicle = player.CurrentVehicle;
                    if (vehicle != null && vehicle.PassengerCount < vehicle.PassengerCapacity && !ped.IsInVehicle(vehicle))
                    {
                        ped.Task.EnterVehicle(
                            vehicle,
                            speed: 2.0f,
                            flag: EnterVehicleFlags.WarpIfDoorIsBlocked |
                                  EnterVehicleFlags.WarpIfShuffleLinkIsBlocked |
                                  EnterVehicleFlags.BlockSeatShuffling
                        );
                    }
                }
                else
                {
                    // Малый stoppingRange: иначе пед, стоя в пределах 10 м, считает что уже "дошёл" и не двигается.
                    float followSpeed = act == "FOLLOW_RUN" ? 3.0f : 1.0f;
                    ped.Task.FollowToOffsetFromEntity(player, new GTA.Math.Vector3(0, 2, 0), followSpeed, -1, 3f, true);
                }
                return;
            }

            // Враждебные/панические/направленные действия — снимаем удержание разговора и возвращаем реактивность.
            Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, false);
            ped.BlockPermanentEvents = false;
            _followers.Remove(ped.Handle); // перестал быть последователем
            if (!isInVehicle)
            {
                ped.Task.ClearAll();
            }

            switch (act)
            {
                case "FLEE":
                    ped.Task.FleeFrom(Game.Player.Character);
                    break;
                case "COWER":
                    if (!isInVehicle)
                    {
                        ped.Task.Cower(-1);
                    }
                    break;
                case "ATTACK":
                    ped.Task.Combat(Game.Player.Character);
                    break;
                case "USE_TURRET":
                    {
                        var player = Game.Player.Character;
                        var vehicle = ped.IsInVehicle()
                            ? ped.CurrentVehicle
                            : (player != null && player.Exists() && player.IsInVehicle() ? player.CurrentVehicle : null);

                        if (vehicle == null || !vehicle.Exists())
                        {
                            AiLogger.Log("ACTION", "USE_TURRET: нет транспорта с турелью рядом.");
                            ped.Task.LookAt(Game.Player.Character, 8000);
                            break;
                        }

                        var turretSeat = FindFreeTurretSeat(vehicle);
                        if (turretSeat == NoTurretSeat)
                        {
                            AiLogger.Log("ACTION", "USE_TURRET: свободного турельного места нет.");
                            ped.Task.LookAt(Game.Player.Character, 8000);
                            break;
                        }

                        Function.Call(Hash.SET_PED_INTO_VEHICLE, ped.Handle, vehicle.Handle, turretSeat);
                        AiLogger.Log("ACTION", $"USE_TURRET: посажен на турельное место {turretSeat}.");
                        break;
                    }
                case "LEAVE":
                    // Спокойный уход (НЕ паника): возвращаем реактивность и уходим обычной походкой.
                    Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, false);
                    ped.BlockPermanentEvents = false;
                    ped.Task.Wander();
                    break;
                default:
                    if (!isInVehicle)
                    {
                        ped.Task.StandStill(15000);
                    }
                    ped.Task.LookAt(Game.Player.Character, 15000);
                    break;
            }
        }

        // Снимает залипшую панику/бегство и сбрасывает память об уроне, чтобы пед мог
        // нормально общаться/следовать и не убегал от вооружённого игрока.
        // ВНИМАНИЕ: блокирует реакцию на события боя — применять только к обычным прохожим,
        // НЕ к союзникам (иначе гварды/компаньоны перестают защищать игрока).
        private static void PacifyPed(Ped ped)
        {
            if (ped == null || !ped.Exists()) return;

            ped.BlockPermanentEvents = true;
            Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, true);
            Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped.Handle, 0, false);
            Function.Call(Hash.SET_PED_KEEP_TASK, ped.Handle, true);
            Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, ped.Handle);
        }

        // Сажает последователей в машину игрока (для «поехали кататься»); чистит мёртвых/исчезнувших.
        private void ManageFollowers()
        {
            if (_followers.Count == 0) return;

            var now = DateTime.Now;
            if (now < _nextFollowerTick) return;
            _nextFollowerTick = now.AddSeconds(1.0);

            var player = Game.Player.Character;
            if (player == null || !player.Exists()) return;

            var vehicle = player.IsInVehicle() ? player.CurrentVehicle : null;

            List<int> gone = null;
            foreach (var handle in _followers)
            {
                var ped = Entity.FromHandle(handle) as Ped;
                if (ped == null || !ped.Exists() || ped.IsDead)
                {
                    (gone ?? (gone = new List<int>())).Add(handle);
                    continue;
                }

                if (vehicle != null && vehicle.Exists() && !ped.IsInVehicle(vehicle))
                {
                    if (vehicle.PassengerCount < vehicle.PassengerCapacity && !ped.IsEnteringVehicle)
                    {
                        ped.Task.EnterVehicle(
                            vehicle,
                            speed: 2.0f,
                            flag: EnterVehicleFlags.WarpIfDoorIsBlocked |
                                  EnterVehicleFlags.WarpIfShuffleLinkIsBlocked |
                                  EnterVehicleFlags.BlockSeatShuffling
                        );
                    }
                }
            }

            if (gone != null)
            {
                foreach (var handle in gone) _followers.Remove(handle);
            }
        }

        // Рядом с педом опасность (бой/стрельба) — повод прервать разговор ради выживания.
        private static bool IsDangerNear(Ped ped)
        {
            if (ped == null || !ped.Exists()) return false;
            if (ped.IsInCombat) return true;

            var pos = ped.Position;
            if (Function.Call<bool>(Hash.IS_BULLET_IN_AREA, pos.X, pos.Y, pos.Z, 12f, true)) return true;

            var player = Game.Player.Character;
            if (player != null && player.Exists() && player.IsShooting && player.Position.DistanceTo(pos) < 20f) return true;

            return false;
        }

        // Союзник игрока (компаньон/гвард) — в той же relationship-группе, что и игрок.
        // Таких НЕ глушим PacifyPed-ом и не паркуем, чтобы они продолжали защищать игрока.
        private static bool IsAlly(Ped ped)
        {
            if (ped == null || !ped.Exists()) return false;
            var player = Game.Player.Character;
            return player != null && player.Exists() && ped.RelationshipGroup == player.RelationshipGroup;
        }

        // Ищет свободное турельное (пулемётное) сиденье транспорта; NoTurretSeat если нет.
        private static int FindFreeTurretSeat(Vehicle vehicle)
        {
            for (int seat = 0; seat <= MaxVehicleSeatIndex; seat++)
            {
                if (!Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, vehicle.Handle, seat, false))
                {
                    continue;
                }
                if (Function.Call<bool>(Hash.IS_TURRET_SEAT, vehicle.Handle, seat))
                {
                    return seat;
                }
            }
            return NoTurretSeat;
        }

        // --- Вовлечение в разговор: удерживаем мирного педа лицом к игроку, пока окно активно,
        // и возвращаем к жизни при выходе. Лечит "пед уходит, когда с ним говоришь". ---

        private void BeginEngagement(Ped ped)
        {
            if (ped == null || !ped.Exists()) return;

            // Если уже держим другого — вернём его к жизни, прежде чем переключиться
            if (_engagedPed != null && _engagedPed.Exists() && _engagedPed.Handle != ped.Handle)
            {
                ReleaseEngagement();
            }

            _engagedPed = ped;
            _engagedShouldHold = true;
            _engageUntil = DateTime.Now.AddSeconds(EngageWindowSeconds);
            _nextEngageRefresh = DateTime.MinValue;

            var player = Game.Player.Character;
            Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, true);
            if (!ped.IsInVehicle())
            {
                ped.Task.StandStill((int)(EngageWindowSeconds * 1000));
            }
            if (player != null && player.Exists())
            {
                // В транспорте НЕ разворачиваем корпус — иначе пед вылезет из машины. Оставляем только взгляд.
                if (!ped.IsInVehicle())
                {
                    Function.Call(Hash.TASK_TURN_PED_TO_FACE_ENTITY, ped.Handle, player.Handle, 1000);
                }
                ped.Task.LookAt(player, (int)(EngageWindowSeconds * 1000));
            }
        }

        // Поза вовлечения из ответа LLM: держим лицом (STOP_AND_LISTEN/WARY/SQUARE_UP)
        // либо отпускаем заниматься своим (ENGAGE_BUSY/BRUSH_OFF).
        private void ApplyStance(Ped ped, string stance)
        {
            if (ped == null || !ped.Exists()) return;

            var s = stance?.ToUpperInvariant();
            bool keepMoving = s == "BRUSH_OFF" || s == "ENGAGE_BUSY";

            if (keepMoving)
            {
                // Не удерживаем: вернуть реактивность, дать заниматься своим, лишь коротко взглянуть на игрока.
                StopEngagementTracking();
                Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, false);
                ped.BlockPermanentEvents = false;
                var player = Game.Player.Character;
                if (player != null && player.Exists())
                {
                    ped.Task.LookAt(player, 2500);
                }
                return;
            }

            // STOP_AND_LISTEN / WARY / SQUARE_UP (и дефолт) — держим лицом к игроку.
            BeginEngagement(ped);
        }

        private void RefreshEngagement()
        {
            if (_engagedPed != null)
            {
                _engageUntil = DateTime.Now.AddSeconds(EngageWindowSeconds);
            }
        }

        // Пед получил направленное действие (FOLLOW/FLEE/...): прекращаем удержание, но НЕ гоним бродить —
        // поведение задаст само действие.
        private void StopEngagementTracking()
        {
            _engagedPed = null;
            _engagedShouldHold = false;
        }

        // Окно закончилось / игрок ушёл: снять блокировки и вернуть педа к обычной жизни.
        private void ReleaseEngagement()
        {
            var ped = _engagedPed;
            _engagedPed = null;
            _engagedShouldHold = false;

            if (ped != null && ped.Exists() && !IsAlly(ped))
            {
                Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, false);
                ped.BlockPermanentEvents = false;
                ped.Task.ClearAll();
                if (!ped.IsInVehicle())
                {
                    ped.Task.Wander();
                }
            }
        }

        // Каждый кадр: держим собеседника лицом к игроку; отпускаем по тайм-ауту или если игрок ушёл далеко.
        private void MaintainEngagement()
        {
            if (_engagedPed == null) return;

            var now = DateTime.Now;
            var player = Game.Player.Character;

            if (!_engagedPed.Exists() || _engagedPed.IsDead || now >= _engageUntil ||
                (player != null && player.Exists() && _engagedPed.Position.DistanceTo(player.Position) > EngageReleaseDistance))
            {
                ReleaseEngagement();
                return;
            }

            // Прерывание по опасности: рядом стрельба/бой → отпускаем, пусть реагирует (выживание важнее разговора).
            if (IsDangerNear(_engagedPed))
            {
                AiLogger.Log("ENGAGE", "Danger near engaged ped — releasing to react.");
                ReleaseEngagement();
                return;
            }

            if (_engagedShouldHold && now >= _nextEngageRefresh && player != null && player.Exists())
            {
                if (!_engagedPed.IsInVehicle())
                {
                    _engagedPed.Task.StandStill(3000);
                }
                _engagedPed.Task.LookAt(player, 3000);
                _nextEngageRefresh = now.AddSeconds(EngageRefreshSeconds);
            }
        }

        // Проактив: именные персонажи сами заговаривают (приветствие/реакция), без Z. Только для IsKnownCharacter.
        private void MaybeProactive()
        {
            if (!_settings.ProactiveEnabled) return;            // выключено в конфиге (по умолчанию)
            if (_isProcessing || _engagedPed != null) return; // занято разговором — не лезем

            var now = DateTime.Now;
            if (now < _nextProactiveScan) return;
            _nextProactiveScan = now.AddSeconds(ProactiveScanSeconds);

            var player = Game.Player.Character;
            if (player == null || !player.Exists() || player.IsDead) return;

            bool playerArmed = player.Weapons.Current.Hash != GTA.WeaponHash.Unarmed;
            bool justDrew = playerArmed && !_prevPlayerArmed;
            bool playerShooting = player.IsShooting;
            _prevPlayerArmed = playerArmed;

            // Глобальные события игрока (привяжем к ближнему именному): розыск, угон, авария.
            int wanted = Game.Player.Wanted.WantedLevel;
            bool wantedRose = wanted > _prevWanted;
            _prevWanted = wanted;

            bool jacking = player.IsJacking;

            bool crashed = false;
            if (player.IsInVehicle())
            {
                var veh = player.CurrentVehicle;
                if (veh != null && veh.Exists())
                {
                    float body = veh.BodyHealth;
                    if (_prevVehBodyHealth >= 0f && _prevVehBodyHealth - body > 150f) crashed = true;
                    _prevVehBodyHealth = body;
                }
            }
            else
            {
                _prevVehBodyHealth = -1f;
            }

            string globalEvent = null;
            if (wantedRose && wanted > 0) globalEvent = "The player just attracted the police — their wanted level went up.";
            else if (jacking) globalEvent = "The player just carjacked a vehicle near you.";
            else if (crashed) globalEvent = "The player just crashed their vehicle near you.";

            bool globalReady = now >= _nextGlobalProactive;

            foreach (var ped in _pedQuery.GetNearbyPeds(ProactiveScanRadius, 6))
            {
                if (ped == null || !ped.Exists() || ped.IsDead) continue;

                var identity = _npcManager.GetOrCreateIdentity(ped);
                if (!identity.IsKnownCharacter || string.IsNullOrEmpty(identity.ModelKey)) continue;

                var key = identity.ModelKey;
                var dist = player.Position.DistanceTo(ped.Position);
                bool wasNear = _wasNear.TryGetValue(key, out var wn) && wn;

                // (0) УГРОЗА (приоритет, направлено на педа): целишься в него или ударил/ранил. Глобальный кулдаун не ждёт.
                bool aiming = Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING_AT_ENTITY, Game.Player.Handle, ped.Handle);
                bool damaged = ped.HasBeenDamagedBy(player);
                if (aiming || damaged)
                {
                    if (!_threatCooldown.TryGetValue(key, out var tu) || now >= tu)
                    {
                        _threatCooldown[key] = now.AddSeconds(ProactiveThreatCooldownSeconds);
                        if (damaged) Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, ped.Handle); // пере-взвести детектор
                        var threat = damaged
                            ? "The player just attacked you — hit or shot you!"
                            : "The player is aiming a weapon straight at you right now.";
                        TriggerProactive(ped, identity, threat);
                        return;
                    }
                    continue; // угроза на кулдауне — пропускаем этого педа
                }

                if (!globalReady) continue; // прочие (не-угроза) триггеры ждут глобальный кулдаун

                // (3a) Прощание вслед: был рядом, теперь отходит — готовая фраза без LLM.
                if (wasNear && dist > ProactiveReactRange && dist <= ProactiveScanRadius)
                {
                    _wasNear[key] = false;
                    TriggerCanned(ped, identity, FarewellLines[_random.Next(FarewellLines.Length)]);
                    return;
                }

                if (dist > ProactiveResetRange) _wasNear[key] = false;

                if (_proactiveCooldown.TryGetValue(key, out var until) && now < until) continue;

                string eventText = null;

                if (globalEvent != null && dist <= ProactiveReactRange)
                {
                    // розыск / угон / авария — приоритетнее приветствия
                    eventText = globalEvent;
                }
                else if (dist <= ProactiveGreetRange && !wasNear)
                {
                    // (1) Приветствие по приближению — один раз за встречу
                    _wasNear[key] = true;
                    eventText = "The player just walked up to you.";
                }
                else if (dist <= ProactiveReactRange && playerShooting)
                {
                    // (2) Реакция на стрельбу рядом
                    eventText = "The player just fired a gun right next to you.";
                }
                else if (dist <= ProactiveReactRange && justDrew)
                {
                    eventText = "The player just pulled out a weapon near you.";
                }
                else if (IsAlly(ped) && dist <= ProactiveCompanionRange && _random.NextDouble() < ProactiveCompanionChance)
                {
                    // (3b) Комментарий компаньона-именного об обстановке
                    eventText = "You are travelling together with the player. Make a brief, in-character remark about the current situation or surroundings.";
                }

                if (eventText != null)
                {
                    TriggerProactive(ped, identity, eventText);
                    return; // один проактив зараз
                }
            }
        }

        private void TriggerProactive(Ped ped, NpcIdentity identity, string eventText)
        {
            var now = DateTime.Now;
            _nextGlobalProactive = now.AddSeconds(ProactiveGlobalCooldownSeconds);
            _proactiveCooldown[identity.ModelKey] = now.AddSeconds(ProactivePerCharCooldownSeconds);

            var player = Game.Player.Character;
            int playerHealth = player != null && player.Exists() ? player.Health : 100;
            int wantedLevel = Game.Player.Wanted.WantedLevel;
            bool hasWeapon = player != null && player.Exists() && player.Weapons.Current.Hash != GTA.WeaponHash.Unarmed;
            var state = GetNpcState(ped);
            int pedHandle = ped.Handle;

            _interactingPed = ped;

            // Повернуться к игроку при обращении (без жёсткого удержания — поза задастся ответом).
            // В транспорте НЕ разворачиваем корпус — иначе пед вылезет из машины.
            if (player != null && player.Exists() && !ped.IsInVehicle())
            {
                Function.Call(Hash.TASK_TURN_PED_TO_FACE_ENTITY, ped.Handle, player.Handle, 1500);
            }

            _isProcessing = true;
            var cts = new CancellationTokenSource();
            _currentCts = cts;

            AiLogger.Log("PROACTIVE", $"Trigger for {identity.Name}: {eventText}");
            Task.Run(async () => await ProcessInteractionAsync(null, eventText, true, null, pedHandle, identity, playerHealth, wantedLevel, hasWeapon, state, cts));
        }

        private void TriggerCanned(Ped ped, NpcIdentity identity, string line)
        {
            var now = DateTime.Now;
            _nextGlobalProactive = now.AddSeconds(ProactiveGlobalCooldownSeconds);
            _proactiveCooldown[identity.ModelKey] = now.AddSeconds(ProactivePerCharCooldownSeconds);

            var player = Game.Player.Character;
            int playerHealth = player != null && player.Exists() ? player.Health : 100;
            int wantedLevel = Game.Player.Wanted.WantedLevel;
            bool hasWeapon = player != null && player.Exists() && player.Weapons.Current.Hash != GTA.WeaponHash.Unarmed;
            var state = GetNpcState(ped);
            int pedHandle = ped.Handle;

            _interactingPed = ped;
            // В транспорте НЕ разворачиваем корпус — иначе пед вылезет из машины.
            if (player != null && player.Exists() && !ped.IsInVehicle())
            {
                Function.Call(Hash.TASK_TURN_PED_TO_FACE_ENTITY, ped.Handle, player.Handle, 1500);
            }

            _isProcessing = true;
            var cts = new CancellationTokenSource();
            _currentCts = cts;

            AiLogger.Log("PROACTIVE", $"Canned for {identity.Name}: {line}");
            Task.Run(async () => await ProcessInteractionAsync(null, null, true, line, pedHandle, identity, playerHealth, wantedLevel, hasWeapon, state, cts));
        }

        private NpcState GetNpcState(Ped ped)
        {
            var player = Game.Player.Character;
            
            int relInt = Function.Call<int>(Hash.GET_RELATIONSHIP_BETWEEN_PEDS, ped.Handle, player.Handle);
            string relationship = "Neutral";
            if (relInt == 0) relationship = "Companion";
            else if (relInt == 1) relationship = "Respect";
            else if (relInt == 2) relationship = "Like";
            else if (relInt == 3) relationship = "Neutral";
            else if (relInt == 4) relationship = "Dislike";
            else if (relInt == 5) relationship = "Hate";

            bool isCombat = ped.IsInCombatAgainst(player);
            bool hasBeenDamaged = ped.HasBeenDamagedBy(player);
            // Враждебность — по СВЕЖЕМУ бою и отношению, а не по липкому флагу урона
            // (иначе пед, раз задетый, навсегда считается враждебным и не переубеждается).
            bool isHostile = isCombat || relInt == 4 || relInt == 5;
            
            bool isFleeing = Function.Call<bool>(Hash.IS_PED_FLEEING, ped.Handle);
            
            int alertness = Function.Call<int>(Hash.GET_PED_ALERTNESS, ped.Handle);
            bool isCowering = (alertness == 3 && !isFleeing) || Function.Call<bool>(Hash.IS_PED_IN_COVER, ped.Handle, false);

            var playerVehicle = player.CurrentVehicle;
            bool ridingWithPlayer = playerVehicle != null && playerVehicle.Exists() && ped.IsInVehicle(playerVehicle);

            return new NpcState
            {
                IsHostile = isHostile,
                IsInCombatWithPlayer = isCombat,
                HasBeenDamagedByPlayer = hasBeenDamaged,
                IsFleeing = isFleeing,
                IsCowering = isCowering,
                Relationship = relationship,
                IsInVehicle = ped.IsInVehicle(),
                IsRidingWithPlayer = ridingWithPlayer
            };
        }
    }
}
