using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace gta.Ai
{
    // Долговременная память известных персонажей: ключ = имя модели (стабильный и уникальный).
    // Хранит сжатую сводку (Summary) и последние реплики (Recent) в scripts/npc_memory.json.
    public class NpcMemoryStore
    {
        private const string PathOnDisk = "scripts/npc_memory.json";

        public class Entry
        {
            public string Summary { get; set; } = "";
            public List<string> Recent { get; set; } = new List<string>();
        }

        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions { WriteIndented = true };

        private readonly object _lock = new object();
        private Dictionary<string, Entry> _data;

        public NpcMemoryStore()
        {
            Load();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(PathOnDisk))
                {
                    var json = File.ReadAllText(PathOnDisk);
                    _data = JsonSerializer.Deserialize<Dictionary<string, Entry>>(json, ReadOptions) ?? new Dictionary<string, Entry>();
                }
                else
                {
                    _data = new Dictionary<string, Entry>();
                }
            }
            catch
            {
                _data = new Dictionary<string, Entry>();
            }
        }

        // Возвращает копию сохранённой памяти по ключу модели (или пустую, если нет).
        public Entry Get(string modelKey)
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(modelKey) && _data.TryGetValue(modelKey, out var e) && e != null)
                {
                    return new Entry
                    {
                        Summary = e.Summary ?? "",
                        Recent = new List<string>(e.Recent ?? new List<string>())
                    };
                }
                return new Entry();
            }
        }

        public void Save(string modelKey, string summary, IEnumerable<string> recent)
        {
            if (string.IsNullOrEmpty(modelKey)) return;

            lock (_lock)
            {
                _data[modelKey] = new Entry
                {
                    Summary = summary ?? "",
                    Recent = new List<string>(recent ?? new List<string>())
                };

                try
                {
                    var json = JsonSerializer.Serialize(_data, WriteOptions);
                    File.WriteAllText(PathOnDisk, json);
                }
                catch
                {
                    // Сохранение памяти не должно ронять игру — молча игнорируем ошибки записи.
                }
            }
        }
    }
}
