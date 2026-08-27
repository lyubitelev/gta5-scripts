using System;
using System.IO;

namespace gta.Ai
{
    public static class AiLogger
    {
        private const string LogPath = "scripts/ai_npc.log";

        public static void Log(string section, string message)
        {
            try
            {
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{section}] {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, logMessage);
            }
            catch
            {
                // Ignore log write failures to prevent game crashes
            }
        }
    }
}
