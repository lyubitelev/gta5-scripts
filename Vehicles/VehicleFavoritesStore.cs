using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace gta.Vehicles
{
    internal sealed class VehicleFavoritesStore
    {
        private readonly string _path;
        private readonly List<string> _vehicles;

        public VehicleFavoritesStore(string path)
        {
            _path = path;
            EnsureFileExists(path);
            _vehicles = File.ReadAllLines(path)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        public IReadOnlyList<string> Vehicles
        {
            get { return _vehicles; }
        }

        public bool Add(string vehicleName)
        {
            if (_vehicles.Contains(vehicleName))
            {
                return false;
            }

            _vehicles.Add(vehicleName);
            File.AppendAllLines(_path, new[] { vehicleName });
            return true;
        }

        public bool Remove(string vehicleName)
        {
            if (!_vehicles.Remove(vehicleName))
            {
                return false;
            }

            File.WriteAllLines(_path, _vehicles);
            return true;
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
