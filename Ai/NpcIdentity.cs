using System.Collections.Generic;

namespace gta.Ai
{
    public class NpcIdentity
    {
        public int Handle { get; set; }
        public string Name { get; set; }
        public string Profession { get; set; }
        public string Personality { get; set; }
        public string VoiceId { get; set; }
        public bool IsKnownCharacter { get; set; }

        // Долговременная память (для именных): ключ по модели + сжатая сводка отношений.
        public string ModelKey { get; set; }
        public string Summary { get; set; } = "";

        // Жёсткий потолок "Recent" в памяти. По умолчанию 10 (толпа, память в пределах сессии);
        // у именных выше — реальное подрезание делает сворачивание в AiController.
        public int MaxRecent { get; set; } = 10;

        public List<string> ChatHistory { get; set; } = new List<string>();

        public void AddUserMessage(string msg)
        {
            ChatHistory.Add($"Player: {msg}");
            if (ChatHistory.Count > MaxRecent) ChatHistory.RemoveAt(0);
        }

        public void AddNpcMessage(string msg)
        {
            ChatHistory.Add($"{Name}: {msg}");
            if (ChatHistory.Count > MaxRecent) ChatHistory.RemoveAt(0);
        }

        public string GetHistoryString()
        {
            return string.Join("\n", ChatHistory);
        }
    }
}
