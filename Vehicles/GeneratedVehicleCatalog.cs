using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace gta.Vehicles
{
    internal sealed class GeneratedVehicleCatalog
    {
        private readonly string[] _vehicles;

        public GeneratedVehicleCatalog(string path)
        {
            EnsureFileExists(path);
            _vehicles = File.ReadAllLines(path)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Where(x => !x.StartsWith("#", StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<string> Vehicles
        {
            get { return _vehicles; }
        }

        private static void EnsureFileExists(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(path))
            {
                File.WriteAllText(path, string.Empty);
            }
        }
    }
}
