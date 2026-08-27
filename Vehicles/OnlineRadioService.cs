using System;
using GTA;
using GTA.Native;
using gta.Core;

namespace gta.Vehicles
{
    internal sealed class OnlineRadioService
    {
        private static readonly string[] AudioFlags = new string[]
        {
            "AllowDLCKultFM",
            "AllowDLCMusicLocker",
            "AllowDLCMotomami",
            "AllowMediaStation",
            "AllowDLCiFruitFM",
            "AllowDLCRadio",
            "EnableMusicLockerStation",
            "EnableKultFMStation",
            "EnableMotomamiStation",
            "EnableMediaStation",
            "EnableiFruitRadio",
            "AllowDLCSkyline",
            "AllowDLCBeach"
        };

        private static readonly string[] DlcRadioStations = new string[]
        {
            "RADIO_19_USER",
            "RADIO_20_THELAB",
            "RADIO_21_DLC_XM17",
            "RADIO_22_DLC_BATTLE_MIX1_RADIO",
            "RADIO_23_DLC_XM19_RADIO",
            "RADIO_27_DLC_PRHEI4",
            "RADIO_27_DLC_PRRADIO",
            "RADIO_34_DLC_HEI4_KULT",
            "RADIO_35_DLC_HEI4_MLR",
            "RADIO_36_AUDIOPLAYER",
            "RADIO_37_MOTOMAMI"
        };

        private bool _isUnlocked;

        public bool IsUnlocked => _isUnlocked;

        public void UnlockAllOnlineRadioStations()
        {
            if (_isUnlocked) return;

            try
            {
                // Set audio flags enabling DLC stations in Singleplayer
                foreach (var flag in AudioFlags)
                {
                    Function.Call(Hash.SET_AUDIO_FLAG, flag, true);
                }

                // Unlock each DLC radio station so it appears in the wheel
                foreach (var station in DlcRadioStations)
                {
                    Function.Call(Hash.LOCK_RADIO_STATION, station, false);
                    Function.Call(Hash.SET_RADIO_STATION_MUSIC_ONLY, station, false);
                }

                _isUnlocked = true;
                Notifier.Show("Радиостанции GTA Online разблокированы!");
                ModLogger.Log("RADIO", "Successfully unlocked all GTA Online radio stations for SP");
            }
            catch (Exception ex)
            {
                ModLogger.Log("RADIO", $"Error unlocking GTA Online radio stations: {ex.Message}");
            }
        }
    }
}
