using System;
using System.Collections.Generic;
using System.Drawing;
using gta.Core;

namespace gta.Vehicles
{
    internal struct VehicleMenuEntry
    {
        public readonly string VehicleName;
        public readonly string DisplayName;
        public readonly bool IsSelectable;
        public readonly int ModelHash;
        public readonly bool HasModelHash;
        public readonly string FavoriteName;

        public VehicleMenuEntry(string vehicleName, string displayName, bool isSelectable)
            : this(vehicleName, displayName, isSelectable, 0, false, vehicleName)
        {
        }

        private VehicleMenuEntry(string vehicleName, string displayName, bool isSelectable, int modelHash, bool hasModelHash, string favoriteName)
        {
            VehicleName = vehicleName;
            DisplayName = displayName;
            IsSelectable = isSelectable;
            ModelHash = modelHash;
            HasModelHash = hasModelHash;
            FavoriteName = favoriteName;
        }

        public static VehicleMenuEntry Vehicle(string vehicleName)
        {
            return new VehicleMenuEntry(vehicleName, vehicleName, true);
        }

        public static VehicleMenuEntry Vehicle(string vehicleName, int modelHash)
        {
            return new VehicleMenuEntry(
                vehicleName,
                vehicleName,
                true,
                modelHash,
                true,
                "hash:" + modelHash + "|" + vehicleName);
        }

        public static VehicleMenuEntry Favorite(string favoriteName)
        {
            if (!favoriteName.StartsWith("hash:", StringComparison.Ordinal))
            {
                return Vehicle(favoriteName);
            }

            var separatorIndex = favoriteName.IndexOf('|');
            int modelHash;
            if (separatorIndex <= 5 || !int.TryParse(favoriteName.Substring(5, separatorIndex - 5), out modelHash))
            {
                return Vehicle(favoriteName);
            }

            var vehicleName = favoriteName.Substring(separatorIndex + 1);
            return new VehicleMenuEntry(vehicleName, vehicleName, true, modelHash, true, favoriteName);
        }

        public static VehicleMenuEntry Category(string name)
        {
            return new VehicleMenuEntry(null, name, true);
        }

        public static VehicleMenuEntry Header(string name)
        {
            return new VehicleMenuEntry(null, "-- " + name + " --", false);
        }
    }

    internal sealed class VehicleMenuRenderer
    {
        public void Draw(string title, IReadOnlyList<VehicleMenuEntry> vehicles, int currentPage, int currentIndex, int itemsPerPage)
        {
            Draw(title, vehicles, currentPage, currentIndex, itemsPerPage, "8/2 - выбор  7/9 - страницы  5 - создать  1 - избранное");
        }

        public void Draw(string title, IReadOnlyList<VehicleMenuEntry> vehicles, int currentPage, int currentIndex, int itemsPerPage, string controlsText)
        {
            var menuText = title + "\n";
            var startIndex = currentPage * itemsPerPage;
            var endIndex = Math.Min(startIndex + itemsPerPage, vehicles.Count);

            for (var index = startIndex; index < endIndex; index++)
            {
                var entry = vehicles[index];
                if (!entry.IsSelectable)
                {
                    menuText += entry.DisplayName + "\n";
                    continue;
                }

                menuText += index == currentIndex
                    ? "> " + entry.DisplayName + " <\n"
                    : entry.DisplayName + "\n";
            }

            var pageCount = Math.Max(1, (vehicles.Count + itemsPerPage - 1) / itemsPerPage);
            if (pageCount > 1)
            {
                menuText += $"\nСтраница {currentPage + 1}/{pageCount}";
            }

            menuText += "\n" + controlsText;

            MenuPanelRenderer.Draw(menuText, new PointF(10, 10), 0.42f);
        }
    }
}
