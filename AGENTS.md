# AGENTS.md

This repository is developed with multiple coding agents (Codex, Claude, Gemini, and others). These instructions are tool-agnostic and apply to every agent working in the repository.

## Repository purpose

`gta5-scripts` contains GTA V scripts built on .NET Framework 4.8 and ScriptHookVDotNet.

The codebase includes gameplay services, input/menu code, vehicle/player/ped/world features, and AI-assisted NPC interaction.

## Working rules

1. Read the task and the relevant code before editing.
2. Keep changes inside the requested scope. Do not perform broad refactors unless the task explicitly requires them.
3. Prefer the smallest reliable change over a new abstraction layer.
4. Preserve existing behavior unless the task explicitly changes it.
5. Do not hardcode machine-specific absolute paths, credentials, API keys, or local environment details.
6. Do not commit generated binaries, build artifacts, local `.user` files, runtime secrets, or GTA installation files.
7. Keep configuration explicit and overridable through tracked example config, MSBuild properties, environment variables, or ignored local files as appropriate.
8. Do not add a second configuration mechanism when an existing one can be extended safely.
9. Treat cancellation, script reload, and abort/unload as normal lifecycle paths, not edge cases.
10. Temporary files and unmanaged/native/audio resources must have clear ownership and deterministic cleanup.

## GTA / ScriptHookVDotNet threading rule

GTA entities, natives, world state, player state, peds, vehicles, tasks, notifications, and other ScriptHookVDotNet APIs must be accessed from the GTA/script thread unless the API is explicitly documented as thread-safe.

Network, AI, file, and long-running work may run asynchronously in the background, but capture only plain immutable data before leaving the game thread and queue GTA mutations back to the game thread.

Never move GTA entity access into `Task.Run` just to make code look asynchronous.

## Build expectations

Before finishing an implementation:

- restore declared dependencies from a clean checkout;
- build Debug and Release when practical;
- ensure CI can reproduce the build without relying on an existing local `packages/` directory;
- verify that every assembly referenced from `gta.csproj` is either part of the framework, declared in package metadata, or supplied through an explicit external dependency path;
- do not hide build failures with skips or `continue-on-error`.

The project currently targets .NET Framework 4.8. Do not migrate it to SDK-style or another target framework unless explicitly requested.

## Security

- Never commit API keys or tokens.
- Runtime AI settings belong in ignored local config; tracked files must contain examples/placeholders only.
- Placeholder values must not be treated as configured credentials.

## Review discipline

For any non-trivial change, self-review for:

- regressions in gameplay behavior;
- ScriptHookVDotNet thread violations;
- cancellation/abort races;
- resource ownership and cleanup leaks;
- stale entity handles or entity lifetime issues;
- hidden local dependencies;
- secrets or machine-specific paths;
- missing package declarations;
- CI/build reproducibility.

## Repository skills

Use the repository-local skills under `.agent/skills/` when relevant:

- `.agent/skills/architecture/SKILL.md`
- `.agent/skills/implementation/SKILL.md`
- `.agent/skills/code-review/SKILL.md`
- `.agent/skills/gta-scripthook/SKILL.md`

These skills provide focused guidance. `AGENTS.md` remains the authoritative repository-wide instruction set.
