# Architecture Skill

Use this skill when a task changes module boundaries, service responsibilities, state ownership, lifecycle, or cross-cutting behavior.

## Goal

Keep the architecture simple enough for a GTA script while preventing accidental coupling between gameplay code, async AI work, persistence, audio, and input/UI.

## Existing direction

Prefer the current separation by responsibility:

- `Core/` — common script infrastructure and UI helpers;
- `Input/` — input routing;
- `Player/` — player-focused features;
- `Peds/` — ped/police/companion behavior;
- `Vehicles/` — vehicle features;
- `World/` — world/location behavior;
- `Ai/` — AI provider integration, NPC memory, recording/playback, and AI orchestration;
- `MainScript.cs` — composition root and script lifecycle.

Do not collapse these areas into one god service, and do not split them further without a concrete maintenance or correctness reason.

## Decision rules

1. Put behavior where its state naturally belongs.
2. Keep ScriptHookVDotNet mutations on the script thread.
3. Background work should exchange DTO-like/plain data with the script thread, not live GTA entities.
4. A resource must have exactly one owner at each lifecycle stage.
5. `Abort`/reload must be able to stop or invalidate every long-running operation.
6. Prefer explicit queues/state transitions over cross-thread callbacks mutating GTA state.
7. Avoid generic frameworks, DI containers, buses, or plugin abstractions unless the repository has a real need for them.
8. Extend an existing service before introducing a parallel competing service for the same responsibility.

## Before changing architecture

Answer these questions in code or notes:

- What concrete problem cannot be solved cleanly in the current boundary?
- Which object owns the new state?
- On which thread is each operation allowed to run?
- How is cancellation/abort handled?
- What happens if the target ped/vehicle/player state disappears before async work completes?
- Who disposes native/audio/file resources?

If those answers are unclear, simplify the design before implementing it.
