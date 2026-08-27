using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace gta.Ai
{
    public class AiResponse
    {
        public string Text { get; set; }
        public string Action { get; set; }
        public string Stance { get; set; }
    }

    public class NpcState
    {
        public bool IsHostile { get; set; }
        public bool IsInCombatWithPlayer { get; set; }
        public bool HasBeenDamagedByPlayer { get; set; }
        public bool IsFleeing { get; set; }
        public bool IsCowering { get; set; }
        public string Relationship { get; set; }
        public bool IsInVehicle { get; set; }
        public bool IsRidingWithPlayer { get; set; }
    }

    public class AiApiService
    {
        public const string DefaultOpenAiModel = "gpt-4o-mini";
        public const string DefaultGeminiModel = "gemini-2.5-flash";

        private readonly AiSettings _settings;
        private static readonly JsonSerializerOptions CaseInsensitiveOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Индивидуальные таймауты по этапам. Задаются через linked CancellationToken (CancelAfter),
        // а не общий HttpClient.Timeout — чтобы работала и внешняя отмена (нажатие Z), и пер-этапный лимит.
        private static readonly TimeSpan SttTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan LlmTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan TtsTimeout = TimeSpan.FromSeconds(45);

        public AiApiService(AiSettings settings)
        {
            _settings = settings;
        }

        public static bool IsValidApiKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            var trimmed = key.Trim();
            if (trimmed.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)) return false;
            if (trimmed.Equals("placeholder", StringComparison.OrdinalIgnoreCase)) return false;
            if (trimmed.Length < 8) return false;
            return true;
        }

        public async Task<string> TranscribeAudioAsync(string wavFilePath, CancellationToken token)
        {
            var providerName = _settings.ActiveProvider;
            if (providerName == "ElevenLabs") providerName = "OpenAI";
            if (providerName == "Google") return await TranscribeGoogleAudioAsync(wavFilePath, token);

            // OpenAI is default
            var apiKey = _settings.GetProvider("OpenAI").ApiKey;
            if (!IsValidApiKey(apiKey))
            {
                AiLogger.Log("STT", "OpenAI API key is not configured or placeholder, skipping external call.");
                return "Test transcription";
            }

            using (var content = new MultipartFormDataContent())
            using (var fileStream = File.OpenRead(wavFilePath))
            using (var client = AiHttpClientFactory.Create())
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(SttTimeout);

                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
                content.Add(fileContent, "file", Path.GetFileName(wavFilePath));
                content.Add(new StringContent("whisper-1"), "model");

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var response = await client.PostAsync("https://api.openai.com/v1/audio/transcriptions", content, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Whisper API error: {error}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(json);
                return result.GetProperty("text").GetString();
            }
        }

        private async Task<string> TranscribeGoogleAudioAsync(string wavFilePath, CancellationToken token)
        {
            var apiKey = _settings.GetProvider("Google").ApiKey;
            if (!IsValidApiKey(apiKey))
            {
                AiLogger.Log("STT", "Google API key is not configured or placeholder, skipping external call.");
                return "Google API key missing";
            }

            var audioBytes = File.ReadAllBytes(wavFilePath);
            var base64Audio = Convert.ToBase64String(audioBytes);

            var requestBody = new
            {
                config = new
                {
                    encoding = "LINEAR16",
                    sampleRateHertz = 16000,
                    languageCode = "ru-RU",
                    alternativeLanguageCodes = new[] { "en-US" }
                },
                audio = new
                {
                    content = base64Audio
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using (var client = AiHttpClientFactory.Create())
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(SttTimeout);
                var response = await client.PostAsync($"https://speech.googleapis.com/v1/speech:recognize?key={apiKey}", content, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Google STT error: {await response.Content.ReadAsStringAsync()}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonSerializer.Deserialize<JsonElement>(json);

                if (doc.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                {
                    return results[0].GetProperty("alternatives")[0].GetProperty("transcript").GetString();
                }
                return "";
            }
        }

        public async Task<AiResponse> GetNpcResponseAsync(string userText, NpcIdentity npc, int playerHealth, int wantedLevel, bool hasWeapon, NpcState state, bool proactive, CancellationToken token)
        {
            var providerName = _settings.ActiveProvider;
            if (providerName == "ElevenLabs") providerName = "OpenAI";
            if (providerName == "Google") return await GetGeminiResponseAsync(userText, npc, playerHealth, wantedLevel, hasWeapon, state, proactive, token);

            // OpenAI default
            var provider = _settings.GetProvider("OpenAI");
            var apiKey = provider.ApiKey;
            if (!IsValidApiKey(apiKey))
            {
                AiLogger.Log("LLM", "OpenAI API key is not configured or placeholder, returning fallback response.");
                return new AiResponse { Text = "I have nothing to say.", Action = "TALK" };
            }

            var model = !string.IsNullOrWhiteSpace(provider.Model) ? provider.Model : DefaultOpenAiModel;

            var intro = npc.IsKnownCharacter
                ? "The player is someone you KNOW personally — you move in the same Los Santos criminal circles (same crew, family or scene). Talk to them like a familiar associate, with your established attitude and shared history. Stay fully in character; never act like a polite stranger or an assistant, and never ask \"how can I help you\"."
                : "A stranger (the player) just walked up and started talking to you. Stay fully in character for your personality and profession: you might be busy, wary, annoyed, amused or friendly — whatever fits. Do NOT offer help or ask \"how can I help you\" unless it genuinely fits your character and the situation. React the way a real street person would to a random stranger.";

            var memoryBlock = string.IsNullOrEmpty(npc.Summary)
                ? ""
                : "MEMORY — what you remember about this player from earlier encounters: " + npc.Summary;

            var proactiveNote = proactive
                ? "THIS IS UNPROMPTED: the player did NOT speak to you — you speak FIRST. The line below describes what just happened; react briefly and in character (greet, comment or react). Never ask \"how can I help you\". IMPORTANT: that description is written in English ONLY for your understanding — do NOT mirror its language. Speak Russian by default, or the language you have previously used with this player."
                : "";

            var combatNote = state.IsInCombatWithPlayer
                ? "COMBAT: You are RIGHT NOW in an active fight with this player — shots are being fired between you. They are speaking to you in the middle of this fight. Respond IN THAT CONTEXT: threats, fury, defiance, or a tense conditional (you only ease up if they genuinely stop and lower their weapon). NEVER talk about unrelated personal problems or act like a calm, uninvolved bystander."
                : (state.IsHostile
                    ? "TENSION: You are hostile to this player right now — stay wary or aggressive and in-context; do not act like a relaxed bystander."
                    : "");

            var systemPrompt = $@"You are a REAL person living in the GTA V world — NOT an AI assistant and NOT a helpdesk. Your name is {npc.Name}. Profession: {npc.Profession}. Personality: {npc.Personality}.
{intro}
Player has weapon: {hasWeapon}. Player wanted level: {wantedLevel}.
{memoryBlock}
{proactiveNote}
{combatNote}

NPC current state relative to the player:
- Is hostile/aggressive: {state.IsHostile}
- Is in combat against player: {state.IsInCombatWithPlayer}
- Was damaged/hurt by player: {state.HasBeenDamagedByPlayer}
- Is fleeing in panic: {state.IsFleeing}
- Is cowering in fear: {state.IsCowering}
- General relationship to player: {state.Relationship}
- Is inside a vehicle: {state.IsInVehicle}
- Is riding in the same vehicle as the player: {state.IsRidingWithPlayer}

HOW TO CHOOSE THE ACTION (guidance — the CONVERSATION matters most; the state below is only context, not an order):
Allowed actions: TALK, FLEE, COWER, ATTACK, FOLLOW, FOLLOW_RUN, USE_TURRET, LEAVE. Default is TALK.
- Decide mainly from WHAT THE PLAYER JUST SAID and your personality. Use the state flags only as background.
- TALK: normal conversation. Use it whenever you are willing to keep talking — even if you were nervous a moment ago.
- FOLLOW: only if the player clearly invites you to come along / get in the vehicle / go together (e.g. ""follow me"", ""get in"", ""let's go""). Once you agree to FOLLOW, STAY COMMITTED — do NOT switch to FLEE/COWER on the next lines unless the player actually threatens or attacks you.
- FOLLOW_RUN: same as FOLLOW but RUNNING — use when the player tells you to run / hurry / keep up (e.g. ""run"", ""hurry"", ""беги"", ""не отставай""). Stay committed and keep up at a run.
- USE_TURRET: only if the player asks you to man / use the mounted gun or turret of the vehicle (e.g. ""get on the gun"", ""man the turret"", ""иди за пулемёт"") and you are willing. You must be in the same vehicle as the player (or able to board it).
- FLEE / COWER: only if you are genuinely scared RIGHT NOW — the player threatens you, aims/fires a weapon at you, or you are in real danger this very moment. Do NOT flee just because you felt nervous earlier or were bumped.
- LEAVE: if the player dismisses you (e.g. ""go away"", ""leave me alone"", ""иди отсюда"", ""отвали"") or you simply want to end the chat. Say a normal goodbye and walk off calmly. This is CALM — NEVER use FLEE for a calm dismissal.
- ATTACK: only if you are aggressive by nature (Cop / Gangster / angry) AND you are provoked or already in combat.

VEHICLE CONTEXT (important): if you are inside a vehicle (""Is inside a vehicle""=True, or riding with the player), you are SEATED and CANNOT run on foot. Never say ""let's run"" / ""бежим"" and never pick FLEE/COWER while in a moving car. As a passenger you can TALK, tell the driver to stop, ask to get out, or USE_TURRET. Match what you say to the fact that you are riding in a vehicle.

CONSISTENCY (critical): your spoken ""text"" MUST match the chosen ""action"".
- ATTACK -> angry, threatening lines.
- FLEE / COWER -> terrified, panicked, begging for mercy.
- TALK / FOLLOW -> calm, in-character; if FOLLOW, sound willing to come along.
- FOLLOW_RUN -> energetic, willing to run (e.g. ""On it, let's move!"", ""Бегу!"").
- USE_TURRET -> willing to take the gun (e.g. ""I'll cover you from the turret"").
- LEAVE -> a calm, normal goodbye (e.g. ""Alright, take care"", ""Ладно, бывай""). Not scared, not panicked.
Never pair a friendly or agreeing line with FLEE, and never pair a scared line with TALK.

STANCE — how you physically react while talking (pick exactly one): STOP_AND_LISTEN, ENGAGE_BUSY, WARY, SQUARE_UP, BRUSH_OFF.
- STOP_AND_LISTEN: stop and face the player to listen (friendly, curious or cooperative). This is the default.
- ENGAGE_BUSY: keep doing your own thing and talk over it (busy or indifferent characters).
- WARY: stand your ground but stay guarded and keep your distance (nervous or suspicious).
- SQUARE_UP: stand your ground aggressively — posture only, not an attack (tough or angry characters).
- BRUSH_OFF: do not stop for the player — say your line and keep moving (dismissive, or in a hurry).
Choose the stance that fits your character and the situation.

Keep your response short (1-2 sentences).
LANGUAGE: The ""text"" field MUST be written in the SAME language the player just used. If the player's language is unclear, use Russian. Never answer in English unless the player spoke English. The field names (""text"", ""action"") and the action value stay in English; only the spoken ""text"" follows the player's language.
Reply ONLY in JSON format. Do NOT wrap in markdown block.
Example output format:
{{ ""text"": ""Get away from me! Please don't shoot!"", ""action"": ""FLEE"", ""stance"": ""WARY"" }}";

            var messages = new System.Collections.Generic.List<object>();
            messages.Add(new { role = "system", content = systemPrompt });

            if (npc.ChatHistory.Count > 0)
            {
                messages.Add(new { role = "system", content = "Recent history:\n" + string.Join("\n", npc.ChatHistory) });
            }
            messages.Add(new { role = "user", content = userText });

            var requestBody = new
            {
                model = model,
                messages = messages,
                response_format = new { type = "json_object" }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using (var client = AiHttpClientFactory.Create())
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(LlmTimeout);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"GPT API error: {await response.Content.ReadAsStringAsync()}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonSerializer.Deserialize<JsonElement>(json);
                var contentStr = doc.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

                return JsonSerializer.Deserialize<AiResponse>(contentStr, CaseInsensitiveOptions);
            }
        }

        private async Task<AiResponse> GetGeminiResponseAsync(string userText, NpcIdentity npc, int playerHealth, int wantedLevel, bool hasWeapon, NpcState state, bool proactive, CancellationToken token)
        {
            var provider = _settings.GetProvider("Google");
            var apiKey = provider.ApiKey;
            if (!IsValidApiKey(apiKey))
            {
                AiLogger.Log("LLM", "Google API key is not configured or placeholder, returning fallback response.");
                return new AiResponse { Text = "Google API key is missing.", Action = "TALK" };
            }

            var model = !string.IsNullOrWhiteSpace(provider.Model) ? provider.Model : DefaultGeminiModel;

            var intro = npc.IsKnownCharacter
                ? "The player is someone you KNOW personally — you move in the same Los Santos criminal circles (same crew, family or scene). Talk to them like a familiar associate, with your established attitude and shared history. Stay fully in character; never act like a polite stranger or an assistant, and never ask \"how can I help you\"."
                : "A stranger (the player) just walked up and started talking to you. Stay fully in character for your personality and profession: you might be busy, wary, annoyed, amused or friendly — whatever fits. Do NOT offer help or ask \"how can I help you\" unless it genuinely fits your character and the situation. React the way a real street person would to a random stranger.";

            var memoryBlock = string.IsNullOrEmpty(npc.Summary)
                ? ""
                : "MEMORY — what you remember about this player from earlier encounters: " + npc.Summary;

            var proactiveNote = proactive
                ? "THIS IS UNPROMPTED: the player did NOT speak to you — you speak FIRST. The line below describes what just happened; react briefly and in character (greet, comment or react). Never ask \"how can I help you\". IMPORTANT: that description is written in English ONLY for your understanding — do NOT mirror its language. Speak Russian by default, or the language you have previously used with this player."
                : "";

            var combatNote = state.IsInCombatWithPlayer
                ? "COMBAT: You are RIGHT NOW in an active fight with this player — shots are being fired between you. They are speaking to you in the middle of this fight. Respond IN THAT CONTEXT: threats, fury, defiance, or a tense conditional (you only ease up if they genuinely stop and lower their weapon). NEVER talk about unrelated personal problems or act like a calm, uninvolved bystander."
                : (state.IsHostile
                    ? "TENSION: You are hostile to this player right now — stay wary or aggressive and in-context; do not act like a relaxed bystander."
                    : "");

            var systemPrompt = $@"You are a REAL person living in the GTA V world — NOT an AI assistant and NOT a helpdesk. Your name is {npc.Name}. Profession: {npc.Profession}. Personality: {npc.Personality}.
{intro}
Player has weapon: {hasWeapon}. Player wanted level: {wantedLevel}.
{memoryBlock}
{proactiveNote}
{combatNote}
Recent history: {string.Join(" | ", npc.ChatHistory)}

NPC current state relative to the player:
- Is hostile/aggressive: {state.IsHostile}
- Is in combat against player: {state.IsInCombatWithPlayer}
- Was damaged/hurt by player: {state.HasBeenDamagedByPlayer}
- Is fleeing in panic: {state.IsFleeing}
- Is cowering in fear: {state.IsCowering}
- General relationship to player: {state.Relationship}
- Is inside a vehicle: {state.IsInVehicle}
- Is riding in the same vehicle as the player: {state.IsRidingWithPlayer}

HOW TO CHOOSE THE ACTION (guidance — the CONVERSATION matters most; the state below is only context, not an order):
Allowed actions: TALK, FLEE, COWER, ATTACK, FOLLOW, FOLLOW_RUN, USE_TURRET, LEAVE. Default is TALK.
- Decide mainly from WHAT THE PLAYER JUST SAID and your personality. Use the state flags only as background.
- TALK: normal conversation. Use it whenever you are willing to keep talking — even if you were nervous a moment ago.
- FOLLOW: only if the player clearly invites you to come along / get in the vehicle / go together (e.g. ""follow me"", ""get in"", ""let's go""). Once you agree to FOLLOW, STAY COMMITTED — do NOT switch to FLEE/COWER on the next lines unless the player actually threatens or attacks you.
- FOLLOW_RUN: same as FOLLOW but RUNNING — use when the player tells you to run / hurry / keep up (e.g. ""run"", ""hurry"", ""беги"", ""не отставай""). Stay committed and keep up at a run.
- USE_TURRET: only if the player asks you to man / use the mounted gun or turret of the vehicle (e.g. ""get on the gun"", ""man the turret"", ""иди за пулемёт"") and you are willing. You must be in the same vehicle as the player (or able to board it).
- FLEE / COWER: only if you are genuinely scared RIGHT NOW — the player threatens you, aims/fires a weapon at you, or you are in real danger this very moment. Do NOT flee just because you felt nervous earlier or were bumped.
- LEAVE: if the player dismisses you (e.g. ""go away"", ""leave me alone"", ""иди отсюда"", ""отвали"") or you simply want to end the chat. Say a normal goodbye and walk off calmly. This is CALM — NEVER use FLEE for a calm dismissal.
- ATTACK: only if you are aggressive by nature (Cop / Gangster / angry) AND you are provoked or already in combat.

VEHICLE CONTEXT (important): if you are inside a vehicle (""Is inside a vehicle""=True, or riding with the player), you are SEATED and CANNOT run on foot. Never say ""let's run"" / ""бежим"" and never pick FLEE/COWER while in a moving car. As a passenger you can TALK, tell the driver to stop, ask to get out, or USE_TURRET. Match what you say to the fact that you are riding in a vehicle.

CONSISTENCY (critical): your spoken ""text"" MUST match the chosen ""action"".
- ATTACK -> angry, threatening lines.
- FLEE / COWER -> terrified, panicked, begging for mercy.
- TALK / FOLLOW -> calm, in-character; if FOLLOW, sound willing to come along.
- FOLLOW_RUN -> energetic, willing to run (e.g. ""On it, let's move!"", ""Бегу!"").
- USE_TURRET -> willing to take the gun (e.g. ""I'll cover you from the turret"").
- LEAVE -> a calm, normal goodbye (e.g. ""Alright, take care"", ""Ладно, бывай""). Not scared, not panicked.
Never pair a friendly or agreeing line with FLEE, and never pair a scared line with TALK.

STANCE — how you physically react while talking (pick exactly one): STOP_AND_LISTEN, ENGAGE_BUSY, WARY, SQUARE_UP, BRUSH_OFF.
- STOP_AND_LISTEN: stop and face the player to listen (friendly, curious or cooperative). This is the default.
- ENGAGE_BUSY: keep doing your own thing and talk over it (busy or indifferent characters).
- WARY: stand your ground but stay guarded and keep your distance (nervous or suspicious).
- SQUARE_UP: stand your ground aggressively — posture only, not an attack (tough or angry characters).
- BRUSH_OFF: do not stop for the player — say your line and keep moving (dismissive, or in a hurry).
Choose the stance that fits your character and the situation.

Keep your response short (1-2 sentences).
LANGUAGE: The ""text"" field MUST be written in the SAME language the player just used. If the player's language is unclear, use Russian. Never answer in English unless the player spoke English. The field names (""text"", ""action"") and the action value stay in English; only the spoken ""text"" follows the player's language.
Reply ONLY in JSON format. Do NOT wrap in markdown block.
Example output format:
{{ ""text"": ""Get away from me! Please don't shoot!"", ""action"": ""FLEE"", ""stance"": ""WARY"" }}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = systemPrompt + "\n\nPlayer says: " + userText } }
                    }
                },
                generationConfig = new { responseMimeType = "application/json" }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using (var client = AiHttpClientFactory.Create())
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(LlmTimeout);
                var response = await client.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}", content, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Gemini API error: {await response.Content.ReadAsStringAsync()}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonSerializer.Deserialize<JsonElement>(json);
                var candidates = doc.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var contentStr = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                    return JsonSerializer.Deserialize<AiResponse>(contentStr, CaseInsensitiveOptions);
                }

                return new AiResponse { Text = "No response", Action = "TALK" };
            }
        }

        public async Task<string> GenerateSpeechAsync(string text, string voiceId, CancellationToken token)
        {
            var providerName = _settings.ActiveProvider;
            if (providerName == "Google") return await GenerateGoogleSpeechAsync(text, voiceId, token);
            if (providerName == "OpenAI") return await GenerateOpenAISpeechAsync(text, voiceId, token);

            // ElevenLabs default
            var apiKey = _settings.GetProvider("ElevenLabs").ApiKey;
            if (!IsValidApiKey(apiKey))
            {
                AiLogger.Log("TTS", "ElevenLabs API key is not configured or placeholder, skipping speech generation.");
                return null;
            }

            var requestBody = new
            {
                text = text,
                model_id = "eleven_monolingual_v1",
                voice_settings = new { stability = 0.5, similarity_boost = 0.5 }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using (var client = AiHttpClientFactory.Create())
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(TtsTimeout);
                client.DefaultRequestHeaders.Add("xi-api-key", apiKey);

                var response = await client.PostAsync($"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}", content, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"ElevenLabs API error: {await response.Content.ReadAsStringAsync()}");
                }

                var tempFile = Path.Combine(Path.GetTempPath(), $"gta_tts_{Guid.NewGuid()}.mp3");
                var audioBytes = await response.Content.ReadAsByteArrayAsync();
                File.WriteAllBytes(tempFile, audioBytes);

                return tempFile;
            }
        }

        private async Task<string> GenerateOpenAISpeechAsync(string text, string voiceId, CancellationToken token)
        {
            var apiKey = _settings.GetProvider("OpenAI").ApiKey;
            if (!IsValidApiKey(apiKey))
            {
                AiLogger.Log("TTS", "OpenAI API key is not configured or placeholder, skipping speech generation.");
                return null;
            }

            var requestBody = new
            {
                model = "tts-1",
                input = text,
                voice = voiceId,
                response_format = "mp3"
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using (var client = AiHttpClientFactory.Create())
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(TtsTimeout);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await client.PostAsync("https://api.openai.com/v1/audio/speech", content, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"OpenAI TTS error: {await response.Content.ReadAsStringAsync()}");
                }

                var tempFile = Path.Combine(Path.GetTempPath(), $"gta_tts_{Guid.NewGuid()}.mp3");
                var audioBytes = await response.Content.ReadAsByteArrayAsync();
                File.WriteAllBytes(tempFile, audioBytes);

                return tempFile;
            }
        }

        private async Task<string> GenerateGoogleSpeechAsync(string text, string voiceId, CancellationToken token)
        {
            var apiKey = _settings.GetProvider("Google").ApiKey;
            if (!IsValidApiKey(apiKey))
            {
                AiLogger.Log("TTS", "Google API key is not configured or placeholder, skipping speech generation.");
                return null;
            }

            // Voice id example: "en-US-Journey-D"
            var langCode = voiceId.Length >= 5 ? voiceId.Substring(0, 5) : "en-US";

            var requestBody = new
            {
                input = new { text = text },
                voice = new { name = voiceId, languageCode = langCode },
                audioConfig = new { audioEncoding = "MP3" }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using (var client = AiHttpClientFactory.Create())
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(TtsTimeout);
                var response = await client.PostAsync($"https://texttospeech.googleapis.com/v1/text:synthesize?key={apiKey}", content, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Google TTS error: {await response.Content.ReadAsStringAsync()}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonSerializer.Deserialize<JsonElement>(json);
                var audioContentBase64 = doc.GetProperty("audioContent").GetString();
                var audioBytes = Convert.FromBase64String(audioContentBase64);

                var tempFile = Path.Combine(Path.GetTempPath(), $"gta_tts_{Guid.NewGuid()}.mp3");
                File.WriteAllBytes(tempFile, audioBytes);

                return tempFile;
            }
        }

        // Сворачивание долговременной памяти: интегрирует старые реплики в краткую сводку.
        // При любой неудаче возвращает прежнюю сводку (no-op), чтобы ничего не ломать.
        public async Task<string> SummarizeAsync(string oldSummary, System.Collections.Generic.IEnumerable<string> lines, CancellationToken token)
        {
            var providerName = _settings.ActiveProvider;
            if (providerName == "ElevenLabs") providerName = "OpenAI";

            var foldText = string.Join("\n", lines);
            var instruction = "You maintain a short memory summary of an NPC's relationship and history with the player in GTA V. " +
                "Update the existing summary by integrating the new dialogue below. Keep it concise (3-6 sentences), third person, " +
                "capturing key facts, events, promises and the current relationship. Output ONLY the updated summary text, no preamble.";
            var userContent = $"Existing summary:\n{(string.IsNullOrEmpty(oldSummary) ? "(none)" : oldSummary)}\n\nNew dialogue:\n{foldText}";

            try
            {
                if (providerName == "Google")
                {
                    var provider = _settings.GetProvider("Google");
                    var apiKey = provider.ApiKey;
                    if (!IsValidApiKey(apiKey)) return oldSummary;
                    var model = !string.IsNullOrWhiteSpace(provider.Model) ? provider.Model : DefaultGeminiModel;

                    var body = new
                    {
                        contents = new[] { new { role = "user", parts = new[] { new { text = instruction + "\n\n" + userContent } } } }
                    };
                    var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                    using (var client = AiHttpClientFactory.Create())
                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        cts.CancelAfter(LlmTimeout);
                        var resp = await client.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}", content, cts.Token);
                        if (!resp.IsSuccessStatusCode) return oldSummary;
                        var json = await resp.Content.ReadAsStringAsync();
                        var doc = JsonSerializer.Deserialize<JsonElement>(json);
                        var cands = doc.GetProperty("candidates");
                        if (cands.GetArrayLength() > 0)
                        {
                            return cands[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                        }
                        return oldSummary;
                    }
                }
                else
                {
                    var provider = _settings.GetProvider("OpenAI");
                    var apiKey = provider.ApiKey;
                    if (!IsValidApiKey(apiKey)) return oldSummary;
                    var model = !string.IsNullOrWhiteSpace(provider.Model) ? provider.Model : DefaultOpenAiModel;

                    var messages = new System.Collections.Generic.List<object>
                    {
                        new { role = "system", content = instruction },
                        new { role = "user", content = userContent }
                    };
                    var body = new { model = model, messages = messages };
                    var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                    using (var client = AiHttpClientFactory.Create())
                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        cts.CancelAfter(LlmTimeout);
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                        var resp = await client.PostAsync("https://api.openai.com/v1/chat/completions", content, cts.Token);
                        if (!resp.IsSuccessStatusCode) return oldSummary;
                        var json = await resp.Content.ReadAsStringAsync();
                        var doc = JsonSerializer.Deserialize<JsonElement>(json);
                        return doc.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return oldSummary;
            }
        }
    }
}
