using System;
using System.IO;

namespace gta.Core
{
    internal static class ModLogger
    {
        private const string LogPath = "scripts/gta_mod.log";

        public static void Log(string section, string message)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{section}] {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, line);
            }
            catch
            {
                // Logging must never break gameplay.
            }
        }
    }
}
