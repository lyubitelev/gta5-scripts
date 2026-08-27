# Code overview

Документ описывает текущее состояние `develop` и нужен как карта для человека или coding agent перед изменениями.

## Runtime composition

`MainScript.cs` — composition root ScriptHookVDotNet script.

В конструкторе он создаёт сервисы и связывает зависимости. Снаружи контейнера/DI нет: lifecycle намеренно остаётся явным.

Основные события:

```text
ScriptHookVDotNet
    |
    +-- KeyDown -> InputRouter + AiController.HandleKeyDown
    +-- KeyUp   -> AiController.HandleKeyUp
    +-- Tick    -> gameplay service Update/Draw + AiController.Update/ProcessQueue
    +-- Aborted -> cleanup/Abort stateful services
```

## Main thread rule

GTA/ScriptHookVDotNet state принадлежит script thread:

- `Game`, `World`, `Entity`, `Ped`, `Vehicle`;
- natives (`GTA.Native.Function.Call`);
- player/wanted/weapon state;
- entity tasks and mutations;
- ScriptHookVDotNet UI/notifications.

Долгие network/STT/LLM/TTS/file операции могут работать вне script thread. Перед выходом в background код должен снять простой snapshot (handle, числа, строки, bool/DTO), а результат вернуть через очередь на script thread.

Для AI это реализовано через `AiController` и `ProcessQueue()`.

## AI pipeline

Ключевые классы:

- `AiController` — orchestration, input, cancellation, engagement, proactive reactions and applying results;
- `AiApiService` — STT/LLM/TTS/provider HTTP integration;
- `AiSettings` — runtime config;
- `VoiceRecordingService` — NAudio microphone lifecycle and WAV ownership;
- `AudioPlayService` — playback and MP3 ownership;
- `NpcManager` — runtime identities and named NPC resolution;
- `NpcIdentity` — conversation/identity state;
- `NpcMemoryStore` — persisted known-character memory;
- `AiLogger` — AI diagnostics.

Normal voice flow:

```text
hold Z
  -> choose nearest ped on script thread
  -> record WAV
release Z
  -> wait until NAudio finalizes WAV
  -> STT
  -> LLM
  -> TTS
  -> enqueue QueuedAiAction
  -> script thread revalidates ped
  -> notification / playback / GTA action
```

### Cancellation and ownership

Повторное `Z` отменяет текущий interaction.

Temporary-file ownership должен быть однозначным:

```text
RecordingSession -> AiController -> WAV cleanup after STT
TTS producer -> QueuedAiAction -> AudioPlayService -> MP3 cleanup
```

`Abort()` синхронизирован с enqueue, поэтому новый queued resource не должен пережить shutdown без owner.

## Gameplay services

### `Vehicles/`

- `VehicleService` — базовые действия с текущей машиной;
- `VehicleSpawner` — создание выбранной машины;
- `VehicleMenuController` / `VehicleMenuRenderer` — vehicle UI;
- `VehicleFavoritesStore` — favorites;
- `GeneratedVehicleCatalog` — generated/online vehicle catalog data;
- `VehicleUpgradeService` / `VehicleTuningConfigStore` — tuning;
- `VehicleIndicatorService` — turn signals;
- `VehicleNitroService` — nitro;
- `VehicleSirenService` — sirens;
- `SpeedLimiterService` — speed limit;
- `OnlineRadioService` — online radio stations;
- `OnlineTrafficService` — online traffic;
- `WorldVehicleStore` — persisted world vehicles;
- `InflatableBoatService` — inflatable boat mechanic.

### `Player/`

- cheats/player-state helpers;
- NoClip;
- camera lock;
- clothing/outfits;
- weapon menu/config;
- animal morph;
- bong interaction;
- Bullet Time;
- telekinesis;
- player interaction menu.

### `Peds/`

- nearest/nearby ped queries;
- companions and chauffeur behavior;
- police/wanted behavior;
- police-officer interaction;
- ped physics effects.

### `World/`

North Yankton loading, road guards, ambient population slots/zones and alive-world orchestration.

## Input routing

`InputRouter` обрабатывает глобальные hotkeys и передаёт управление открытым меню с приоритетом:

```text
Help
 -> Police quick menu
 -> Player interaction menu
 -> Vehicle upgrades
 -> Weapons
 -> Clothing
 -> Vehicle menu
 -> global hotkeys
```

AI `Z` не находится в `InputRouter`; он обрабатывается отдельно в `AiController` на `KeyDown/KeyUp`.

## Persistent/runtime files

Пути формируются через `Core/ScriptPaths.cs` и соответствующие stores.

AI config:

- tracked: `ai_settings.example.json`;
- runtime/untracked: `scripts/ai_settings.json`;
- API keys нельзя коммитить.

Временные voice/TTS файлы создаются в OS temp и должны удаляться владельцем на success, error, cancellation и abort.

## Build model

Проект — classic `.csproj`, target `.NET Framework 4.8`, platform x64.

NuGet packages декларируются в `packages.config`; `nuget restore gta.sln` должен полностью восстанавливать `packages/` на clean checkout.

`ScriptHookVDotNet3.dll` — внешняя binary dependency. Она задаётся через:

- `/p:ScriptHookVDotNetPath=...` — предпочтительно для build/CI;
- `/p:Gta5Path=...` — удобно локально;
- untracked `gta.csproj.user`.

CI pinned на `Chiheb-Bacha/ScriptHookVDotNetEnhanced v1.1.0.6` и проверяет checksum перед сборкой.

## Documentation status

- `README.md` — актуальное пользовательское/разработческое описание;
- `docs/CODE_OVERVIEW.md` — архитектурная карта текущего кода;
- `done/` — планы, уже воплощённые в коде;
- `research/` — исследования;
- `not_impl/` — идеи/планы, которые нельзя считать реализованными только по наличию markdown;
- `AGENTS.md` + `.agent/skills/` — обязательные инструкции coding agents.

## Known maintenance backlog

Текущая Enhanced SDK помечает ряд старых SHVDN API как obsolete (`World.CreatePed/CreateVehicle/CreateProp`, старые `Game.IsControl*`, `Game.DisableControlThisFrame`). Это не compile blocker, но при изменении затронутого кода следует переходить на рекомендованные Enhanced API вместо добавления новых вызовов deprecated методов.
