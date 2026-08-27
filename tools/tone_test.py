# -*- coding: utf-8 -*-
"""
Play a test tone into a SPECIFIC output endpoint (no GTA needed).
Proves whether the physical headphone route works for app audio,
independent of GTA. Uses WASAPI shared mode (same path as VLC/browser).
"""
import sys
import time
import numpy as np
import sounddevice as sd

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass


def list_outputs():
    print("=" * 60)
    print("WASAPI OUTPUT DEVICES")
    print("=" * 60)
    apis = sd.query_hostapis()
    devs = sd.query_devices()
    wasapi_idx = None
    for i, a in enumerate(apis):
        if "WASAPI" in a["name"]:
            wasapi_idx = i
    out = []
    for i, d in enumerate(devs):
        if d["max_output_channels"] > 0 and d["hostapi"] == wasapi_idx:
            print("  [%d] %s  (%d ch, default-sr %.0f)" % (
                i, d["name"], d["max_output_channels"],
                d["default_samplerate"]))
            out.append(i)
    return out


def play_tone(dev_index, freq=440.0, secs=2.5, sr=48000):
    name = sd.query_devices(dev_index)["name"]
    print("\n>>> Playing %d Hz for %.1fs into  [%d] %s" %
          (freq, secs, dev_index, name))
    t = np.linspace(0, secs, int(sr * secs), endpoint=False)
    tone = (0.3 * np.sin(2 * np.pi * freq * t)).astype(np.float32)
    stereo = np.column_stack([tone, tone])
    try:
        sd.play(stereo, samplerate=sr, device=dev_index, blocking=True)
    except Exception as e:
        print("    FAILED: %s" % e)


if __name__ == "__main__":
    outs = list_outputs()
    print("\nUsage: python tools/tone_test.py <device_index> [freq]")
    if len(sys.argv) > 1:
        idx = int(sys.argv[1])
        freq = float(sys.argv[2]) if len(sys.argv) > 2 else 440.0
        play_tone(idx, freq)
