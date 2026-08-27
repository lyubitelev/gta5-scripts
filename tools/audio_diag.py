# -*- coding: utf-8 -*-
"""
Audio diagnostic for the GTA5-Enhanced "no sound in headphones" case.

Goal: stop guessing about drivers and MEASURE where the signal dies.
Decisive question: do samples actually reach the headphone endpoint / the
GTA session, or is the session "active" but empty?

Run while GTA is playing a LOUD, CONTINUOUS sound (radio / gunfire).
"""

import sys
import time
import ctypes
from ctypes import POINTER, cast

# Force UTF-8 console so Cyrillic device names are readable
try:
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
except Exception:
    pass

import comtypes
from comtypes import CLSCTX_ALL, GUID
from comtypes.automation import VARIANT
from ctypes import wintypes

from pycaw.pycaw import (
    AudioUtilities,
    IAudioEndpointVolume,
    IAudioMeterInformation,
    IAudioSessionManager2,
    IAudioSessionControl2,
    ISimpleAudioVolume,
)

# IMMDeviceEnumerator / IMMDevice live in the api module
from pycaw.api.audioclient import IAudioClient

# ---- constants ----
eRender, eCapture, eAll = 0, 1, 2
DEVICE_STATE_ACTIVE = 0x1
DEVICE_STATE_DISABLED = 0x2
DEVICE_STATE_NOTPRESENT = 0x4
DEVICE_STATE_UNPLUGGED = 0x8
DEVICE_STATEMASK_ALL = 0xF

STATE_NAMES = {
    DEVICE_STATE_ACTIVE: "ACTIVE",
    DEVICE_STATE_DISABLED: "DISABLED",
    DEVICE_STATE_NOTPRESENT: "NOTPRESENT",
    DEVICE_STATE_UNPLUGGED: "UNPLUGGED",
}

ERole_eConsole, ERole_eMultimedia, ERole_eCommunications = 0, 1, 2


def sep(t=""):
    print("\n" + "=" * 70)
    if t:
        print(t)
        print("=" * 70)


FORM_FACTOR = {
    0: "RemoteNetwork", 1: "Speakers", 2: "LineLevel", 3: "Headphones",
    4: "Microphone", 5: "Headset", 6: "Handset", 7: "DigitalPassthrough",
    8: "SPDIF", 9: "HDMI/DisplayAudio", 10: "Unknown",
}

# PKEY_AudioEndpoint_FormFactor : {1DA5D803-D492-4EDD-8C23-E0C0FFEE7F0E} pid 0
_FMTID_FORMFACTOR = GUID("{1DA5D803-D492-4EDD-8C23-E0C0FFEE7F0E}")
# PKEY_AudioEndpoint_GUID : {1DA5D803-D492-4EDD-8C23-E0C0FFEE7F0E} pid 4
STGM_READ = 0x0


def friendly(dev):
    try:
        d = AudioUtilities.CreateDevice(dev)
        return d.FriendlyName
    except Exception as e:
        return "<name?: %s>" % e


def form_factor(dev):
    try:
        from pycaw.api.mmdeviceapi.depend import PROPERTYKEY
        store = dev.OpenPropertyStore(STGM_READ)
        pk = PROPERTYKEY()
        pk.fmtid = _FMTID_FORMFACTOR
        pk.pid = 0
        val = store.GetValue(pk)
        # PROPVARIANT union; UI4 lives in .union.ulVal for VT_UI4(19)
        try:
            ff = int(val.GetValue())
        except Exception:
            ff = int(val.union.ulVal)
        return FORM_FACTOR.get(ff, "code=%s" % ff)
    except Exception as e:
        return "<ff?: %s>" % e


def get_mix_format(dev):
    try:
        iface = dev.Activate(IAudioClient._iid_, CLSCTX_ALL, None)
        ac = cast(iface, POINTER(IAudioClient))
        wf = ac.GetMixFormat()  # POINTER(WAVEFORMATEX)
        f = wf.contents
        return "%d ch, %d Hz, %d-bit (tag=%d)" % (
            f.nChannels, f.nSamplesPerSec, f.wBitsPerSample, f.wFormatTag
        )
    except Exception as e:
        return "<format?: %s>" % e


def get_vol_mute(dev):
    try:
        iface = dev.Activate(IAudioEndpointVolume._iid_, CLSCTX_ALL, None)
        vol = cast(iface, POINTER(IAudioEndpointVolume))
        scalar = vol.GetMasterVolumeLevelScalar()
        mute = vol.GetMute()
        return scalar, bool(mute)
    except Exception as e:
        return None, "<%s>" % e


def get_meter(dev):
    try:
        iface = dev.Activate(IAudioMeterInformation._iid_, CLSCTX_ALL, None)
        return cast(iface, POINTER(IAudioMeterInformation))
    except Exception:
        return None


def enum_all_render(enumerator):
    coll = enumerator.EnumAudioEndpoints(eRender, DEVICE_STATEMASK_ALL)
    count = coll.GetCount()
    out = []
    for i in range(count):
        out.append(coll.Item(i))
    return out


def default_id(enumerator, role):
    try:
        d = enumerator.GetDefaultAudioEndpoint(eRender, role)
        return d.GetId()
    except Exception:
        return None


def list_endpoints(enumerator):
    sep("ALL RENDER ENDPOINTS (active + disabled + unplugged + notpresent)")
    def_console = default_id(enumerator, ERole_eConsole)
    def_multi = default_id(enumerator, ERole_eMultimedia)
    def_comm = default_id(enumerator, ERole_eCommunications)
    print("Default (Console/Game):   %s" % def_console)
    print("Default (Multimedia):     %s" % def_multi)
    print("Default (Communications): %s" % def_comm)
    print()

    devs = enum_all_render(enumerator)
    for dev in devs:
        did = dev.GetId()
        state = dev.GetState()
        name = friendly(dev)
        flags = []
        if did == def_console:
            flags.append("DEFAULT-GAME")
        if did == def_multi:
            flags.append("DEFAULT-MM")
        if did == def_comm:
            flags.append("DEFAULT-COMM")
        print("- %s  [%s] %s" % (name, STATE_NAMES.get(state, state),
                                  (" ".join(flags))))
        print("    id: %s" % did)
        print("    type: %s" % form_factor(dev))
        if state == DEVICE_STATE_ACTIVE:
            scalar, mute = get_vol_mute(dev)
            print("    fmt: %s" % get_mix_format(dev))
            if scalar is not None:
                print("    master vol: %d%%   muted: %s" %
                      (round(scalar * 100), mute))
    return devs


def list_sessions_per_device(enumerator, devs):
    sep("AUDIO SESSIONS PER ACTIVE ENDPOINT (process / state / vol / mute)")
    for dev in devs:
        if dev.GetState() != DEVICE_STATE_ACTIVE:
            continue
        name = friendly(dev)
        try:
            iface = dev.Activate(IAudioSessionManager2._iid_, CLSCTX_ALL, None)
            mgr = cast(iface, POINTER(IAudioSessionManager2))
            senum = mgr.GetSessionEnumerator()
            n = senum.GetCount()
        except Exception as e:
            print("- %s : <session enum failed: %s>" % (name, e))
            continue
        print("- %s : %d session(s)" % (name, n))
        for i in range(n):
            try:
                ctl = senum.GetSession(i)
                ctl2 = ctl.QueryInterface(IAudioSessionControl2)
                pid = ctl2.GetProcessId()
                disp = ""
                try:
                    disp = ctl2.GetDisplayName()
                except Exception:
                    pass
                state = ctl2.GetState()
                pname = pid_to_name(pid)
                # per-session volume / mute
                sv = ctl.QueryInterface(ISimpleAudioVolume)
                svol = sv.GetMasterVolume()
                smute = sv.GetMute()
                # per-session peak meter
                try:
                    smeter = ctl.QueryInterface(IAudioMeterInformation)
                    peak = smeter.GetPeakValue()
                except Exception:
                    peak = None
                print("    pid=%s %-22s state=%s vol=%d%% mute=%s peak=%s %s" % (
                    pid, pname, state, round(svol * 100), bool(smute),
                    ("%.4f" % peak) if peak is not None else "n/a", disp))
            except Exception as e:
                print("    <session %d read failed: %s>" % (i, e))


def pid_to_name(pid):
    try:
        import psutil
        return psutil.Process(pid).name()
    except Exception:
        return "pid:%d" % pid


def poll_peaks(enumerator, devs, seconds):
    sep("LIVE PEAK POLL  (PLAY LOUD GTA AUDIO NOW — radio/gunfire)")
    print("Polling %d s. Watching peak of every ACTIVE endpoint AND the GTA "
          "session.\n" % seconds)

    # endpoint meters
    ep_meters = []
    for dev in devs:
        if dev.GetState() == DEVICE_STATE_ACTIVE:
            m = get_meter(dev)
            if m is not None:
                ep_meters.append((friendly(dev), dev.GetId(), m))

    # GTA session meters across all active devices
    gta_meters = []
    for dev in devs:
        if dev.GetState() != DEVICE_STATE_ACTIVE:
            continue
        try:
            iface = dev.Activate(IAudioSessionManager2._iid_, CLSCTX_ALL, None)
            mgr = cast(iface, POINTER(IAudioSessionManager2))
            senum = mgr.GetSessionEnumerator()
            for i in range(senum.GetCount()):
                ctl = senum.GetSession(i)
                ctl2 = ctl.QueryInterface(IAudioSessionControl2)
                pname = pid_to_name(ctl2.GetProcessId()).lower()
                if "gta" in pname:
                    sm = ctl.QueryInterface(IAudioMeterInformation)
                    gta_meters.append(("%s@%s" % (pname, friendly(dev)), sm))
        except Exception:
            pass

    ep_max = {n: 0.0 for (n, _id, _m) in ep_meters}
    gta_max = {n: 0.0 for (n, _m) in gta_meters}

    t_end = time.time() + seconds
    while time.time() < t_end:
        line = []
        for (n, _id, m) in ep_meters:
            try:
                p = m.GetPeakValue()
            except Exception:
                p = 0.0
            ep_max[n] = max(ep_max[n], p)
            line.append("%s=%.3f" % (n[:14], p))
        for (n, m) in gta_meters:
            try:
                p = m.GetPeakValue()
            except Exception:
                p = 0.0
            gta_max[n] = max(gta_max[n], p)
        print("  " + " | ".join(line), end="\r")
        time.sleep(0.2)

    sep("PEAK RESULTS (max over the poll window)")
    print("Per ENDPOINT (is ANY signal reaching the device's mixer?):")
    for n, v in ep_max.items():
        verdict = "SIGNAL" if v > 0.0005 else "SILENT"
        print("   %-28s max-peak=%.4f  -> %s" % (n, v, verdict))
    print("\nPer GTA SESSION (is the game feeding samples at all?):")
    if not gta_max:
        print("   <no GTA session found — is the game running?>")
    for n, v in gta_max.items():
        verdict = "FEEDING" if v > 0.0005 else "NOT-FEEDING (empty session)"
        print("   %-40s max-peak=%.4f -> %s" % (n, v, verdict))

    sep("INTERPRETATION")
    print("""\
 GTA session FEEDING (peak>0) but its ENDPOINT meter SILENT (peak~0)
     -> game produces audio into the correct endpoint, but it never reaches
        the device output. Loss is in the WASAPI/APO layer of THIS endpoint:
        audio enhancements / spatial sound / exclusive mode / broken downmix.
        NOT drivers, NOT device selection, NOT mute. <-- THIS IS THE CASE.
 GTA session FEEDING + its endpoint SIGNAL + headphones SILENT
     -> signal reaches the endpoint mixer but the HW/route (Realtek SST mux)
        does not output it. Driver/hardware layer. (Driver swap relevant.)
 GTA session NOT-FEEDING (peak ~0 while 'active')
     -> game is NOT producing audio into that endpoint. Application layer:
        game bound to a different/stale device GUID, channel-config mismatch,
        or the NAudio mod re-inited the output. Drivers are irrelevant here.
 GTA session feeding on endpoint X, but headphones are endpoint Y
     -> game is playing into the WRONG endpoint (hidden/2nd device).""")


def main():
    seconds = int(sys.argv[1]) if len(sys.argv) > 1 else 30
    comtypes.CoInitialize()
    try:
        enumerator = AudioUtilities.GetDeviceEnumerator()
        devs = list_endpoints(enumerator)
        list_sessions_per_device(enumerator, devs)
        poll_peaks(enumerator, devs, seconds)
    finally:
        comtypes.CoUninitialize()


if __name__ == "__main__":
    main()
