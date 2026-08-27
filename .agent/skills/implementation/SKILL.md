# Implementation Skill

Use this skill when implementing or fixing code in this repository.

## Workflow

1. Read the target code and adjacent lifecycle/call sites first.
2. Identify the smallest set of files required.
3. Preserve public behavior outside the requested change.
4. Implement the fix without unrelated cleanup.
5. Verify ownership, cancellation, GTA-thread access, and failure paths.
6. Restore from declared dependencies and build from a clean state when possible.
7. Review the final diff before commit.

## C# / .NET rules

- Target remains .NET Framework 4.8 unless explicitly changed.
- Match the language/features supported by the current project toolchain.
- Prefer clear types and explicit state over clever abstractions.
- Avoid `.Wait()` and `.Result()` on async operations that can block the script thread.
- Do not introduce fire-and-forget work without a cancellation/lifecycle story.
- Dispose `IDisposable`/audio/native/file resources deterministically.
- A task that produces a temp file must either transfer ownership explicitly or clean it in failure/cancel paths.

## GTA-specific implementation rules

- Read GTA entity/world state on the script thread.
- Convert required state to primitive/plain values before background work.
- Run HTTP/AI/file processing off-thread where appropriate.
- Queue entity/world/task/notification mutations back to the script thread.
- Revalidate an entity when applying delayed work; handles can become stale and entities can die/despawn.
- Script abort/reload must cancel or neutralize outstanding async work.

## Configuration/dependencies

- No absolute local machine paths in tracked files.
- No secrets in tracked config.
- Every NuGet assembly reference must be backed by declared package metadata.
- External game dependencies such as ScriptHookVDotNet must be configurable and validated with a clear build error.

## Done criteria

A change is not done until:

- requested behavior is implemented;
- error/cancel/abort paths are safe;
- no obvious resource leak remains;
- no ScriptHookVDotNet background-thread access was introduced;
- clean dependency restore/build is reproducible or any unavoidable external limitation is documented;
- the diff contains no unrelated changes.
