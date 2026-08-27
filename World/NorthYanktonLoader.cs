using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;

namespace gta.Worlds
{
    internal sealed class NorthYanktonLoader
    {
        private static readonly string[] BaseIpls =
        {
            "prologue01",
            "prologue01c",
            "prologue01d",
            "prologue01e",
            "prologue01f",
            "prologue01g",
            "prologue01h",
            "prologue01i",
            "prologue01j",
            "prologue01k",
            "prologue01z",
            "prologue02",
            "prologue03",
            "prologue03b",
            "prologue04",
            "prologue04b",
            "prologue05",
            "prologue05b",
            "prologue06",
            "prologue06b"
        };

        private static readonly string[] ExtraIpls =
        {
            "plg_rd",
            "prologuerd",
            "prologuerdb",
            "prologue06_int",
            "prologue06_pannel",
            "prologue06b_int",
            "prologue06b_pannel",
            "prologue_michael",
            "prologue03_grv_cov",
            "prologue03_grv_fun",
            "prologue_grv_torch",
            "prologue04_grv_cov",
            "prologue04_grv_fun",
            "prologue04_grv_amb",
            "prologue04_grv_torch"
        };

        public void Load()
        {
            var yanktonCoords = new Vector3(3217.69f, -4834.51f, 111.81f);
            Game.Player.Character.Position = yanktonCoords;

            foreach (var ipl in BaseIpls)
            {
                Function.Call(Hash.REMOVE_IPL, ipl);
            }

            foreach (var ipl in BaseIpls)
            {
                Function.Call(Hash.REQUEST_IPL, ipl);
            }

            foreach (var ipl in ExtraIpls)
            {
                Function.Call(Hash.REQUEST_IPL, ipl);
            }

            Function.Call(Hash.NEW_LOAD_SCENE_START, yanktonCoords.X, yanktonCoords.Y, yanktonCoords.Z, 0f, 0f, 0f, 50f, 0);

            var timeout = 0;
            while (!Function.Call<bool>(Hash.IS_NEW_LOAD_SCENE_LOADED) && timeout < 50)
            {
                Script.Wait(100);
                timeout++;
            }

            Notifier.Show(Function.Call<bool>(Hash.IS_NEW_LOAD_SCENE_LOADED)
                ? "Северный Янктон загружен."
                : "Ошибка загрузки Северного Янктона.");
        }
    }
}
