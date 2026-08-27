# Code Review Skill

Use this skill when reviewing a PR, commit, or implementation before merge.

## Review order

Review correctness and runtime risk before style.

### P1 / merge blockers

Look first for:

- compile/build failures;
- clean-checkout dependency failures;
- ScriptHookVDotNet/GTA API calls from background threads;
- race conditions that can corrupt state or break subsequent interactions;
- secrets committed to the repository;
- machine-specific paths required for normal build/run;
- resource lifecycle bugs that can leave microphone/audio/native resources active after reload;
- cancellation/abort paths that allow stale async results to mutate the game;
- invalid or unsupported external API/model configuration that breaks a feature by default.

### P2

Then check:

- temp-file/resource leaks;
- stale entity handles and missing existence/death revalidation;
- poor ownership transfer between background work, queued actions, and playback;
- silent exception swallowing that hides broken user-visible behavior;
- config duplication or hidden local prerequisites;
- missing CI coverage for the changed build path.

### P3

Finally consider:

- naming/readability;
- small duplication;
- documentation drift;
- maintainability improvements that do not justify blocking merge.

## GTA concurrency checklist

For each async path verify:

1. What data is captured on the script thread?
2. Does background code touch `Ped`, `Vehicle`, `Entity`, `Game`, `World`, `Function.Call`, tasks, notifications, or other GTA APIs?
3. How is the result returned to the script thread?
4. Is the target entity revalidated before applying the result?
5. What happens when a newer interaction supersedes the old one?
6. What happens on script abort/reload?

## Resource ownership checklist

For WAV/MP3/temp/native/audio resources, trace every transition:

`producer -> background operation -> queued action -> consumer -> cleanup`

At every point there must be exactly one owner. Check success, exception, timeout, cancellation, stale result, vanished entity, abort, and playback failure.

## Build review

Do not accept "works on my machine" when tracked project files reference dependencies absent from package metadata.

Verify:

- `packages.config` matches assembly references;
- CI restores from a clean checkout;
- external SHVDN dependency is obtained/configured explicitly;
- Debug/Release build commands are meaningful;
- failing CI is understood before merge.

## Review output

Report findings ordered by severity, with concrete file/path and failure scenario. Avoid speculative architecture advice when the current code is adequate.
