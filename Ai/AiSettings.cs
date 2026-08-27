using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace gta.Ai
{
    public class ProviderSettings
    {
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "";
        public string[] VoiceIds { get; set; } = new string[0];
        public string[] MaleVoiceIds { get; set; } = new string[0];
        public string[] FemaleVoiceIds { get; set; } = new string[0];
    }

    public class AiSettings
    {
        public string ActiveProvider { get; set; } = "OpenAI";

        // Проактивная речь (педы заговаривают сами). По умолчанию ВЫКЛ, чтобы не жечь токены в фоне.
        public bool ProactiveEnabled { get; set; } = false;

        public Dictionary<string, ProviderSettings> Providers { get; set; } = new Dictionary<string, ProviderSettings>();

        public static AiSettings Load(string path)
        {
            if (!File.Exists(path))
            {
                var fallbackPath = Path.GetFileName(path);
                if (File.Exists(fallbackPath))
                {
                    path = fallbackPath;
                }
                else
                {
                    return new AiSettings();
                }
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AiSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AiSettings();
            }
            catch (Exception ex)
            {
                AiLogger.Log("SETTINGS", $"Failed to load settings from {path}: {ex.Message}");
                return new AiSettings();
            }
        }
        
        public ProviderSettings GetProvider(string name)
        {
            if (Providers.TryGetValue(name, out var prov)) return prov;
            return new ProviderSettings();
        }
    }
}
