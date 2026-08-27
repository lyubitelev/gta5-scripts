# GTA / ScriptHookVDotNet Skill

Use this skill for gameplay, entities, natives, input, world state, ScriptHookVDotNet integration, and any async code that interacts with GTA state.

## Core rule: GTA state belongs to the script thread

Treat these as script-thread-only unless proven otherwise by the library documentation:

- `Game`, `World`, `Entity`, `Ped`, `Vehicle`;
- `Game.Player` and wanted/player/weapon state;
- `GTA.Native.Function.Call`;
- ped/vehicle task APIs;
- entity creation/deletion/mutation;
- notifications and other ScriptHookVDotNet UI/game calls.

Do not call them from `Task.Run`, HTTP continuations, NAudio callbacks, timers, or arbitrary worker threads.

## Safe async pattern

1. On the script thread, validate the target entity.
2. Capture only primitive/plain state needed by background work: handles, strings, numbers, booleans, DTO-like snapshots.
3. Perform network/STT/LLM/TTS/file work in the background.
4. Queue a result back to the script thread.
5. Before applying, resolve/revalidate the entity again and verify it still exists and is in a valid state.
6. Reject stale work when cancellation/version/current-interaction checks fail.

Do not hold a live GTA entity object as a justification to access it later from a worker thread.

## Script lifecycle

`MainScript.OnAborted()` is a real shutdown boundary.

Subsystems that own background/native/audio work must expose a cleanup path that can:

- cancel active requests;
- stop recording;
- stop playback;
- invalidate queued stale work;
- release NAudio/native resources;
- clean temp files still owned by the subsystem.

Callbacks arriving after abort must be harmless and must not enqueue leaking resources or mutate GTA state.

## Entity lifetime

A handle captured before an HTTP/AI request may be invalid by the time the response returns.

Before delayed application:

- resolve the entity from the handle;
- check non-null;
- check `Exists()`;
- check death/despawn/state constraints required by the feature.

Never assume a ped or vehicle remains valid because it was valid before awaiting network work.

## Audio recording/playback

NAudio callbacks are not GTA script callbacks. They must not touch GTA APIs directly.

For recording:

- the WAV is not considered complete until recording has stopped and the writer is disposed/finalized;
- isolate per-recording state so callbacks from an old session cannot dispose or complete a new session;
- prevent overlapping start/stop lifecycle unless deliberately implemented;
- clean partial/orphan WAV files on start failure, stop failure, abort, and cancellation.

For playback:

- make MP3 ownership explicit from producer to queue to playback service;
- delete the file after playback, stop, error, stale action, or abort;
- do not mark ownership transferred before the receiving component actually owns the file.

## ScriptHookVDotNet dependency

The project must not assume one developer's GTA installation path.

Use configurable MSBuild properties/local ignored settings for `ScriptHookVDotNet3.dll`; prefer `/p:ScriptHookVDotNetPath=...` for builds and CI.

The supported build target is GTA V Enhanced. CI is intentionally pinned to `Chiheb-Bacha/ScriptHookVDotNetEnhanced v1.1.0.6` and verifies the published release archive checksum before compiling. Do not silently replace it with the Legacy/upstream SHVDN nightly just because both expose a `ScriptHookVDotNet3.dll` with a compatible compile-time surface.

Do not commit ScriptHookVDotNet binaries into this repository. If the supported runtime dependency changes, update CI, README and this skill together.
