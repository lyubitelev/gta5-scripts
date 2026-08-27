# gta5-scripts

Набор GTA V скриптов на C# / .NET Framework 4.8 для ScriptHookVDotNet. Репозиторий объединяет транспортные, игровые, NPC/world-механики и AI-диалоги с NPC.

Основная поддерживаемая среда сейчас — GTA V Enhanced. CI компилирует проект против закреплённого `ScriptHookVDotNetEnhanced v1.1.0.6` и не хранит DLL в репозитории.

## Что уже есть

### AI / NPC

- голосовой диалог с ближайшим NPC по удержанию `Z`;
- STT -> LLM -> TTS pipeline;
- OpenAI и Google Gemini как LLM-провайдеры, ElevenLabs как дополнительный TTS provider;
- именованные NPC, история диалога и сворачивание длинной истории в summary;
- сохранение памяти известных персонажей;
- proactive speech для именованных NPC (по умолчанию выключен);
- действия NPC по результату диалога, engagement и follower-сценарии;
- отмена активного AI-запроса повторным нажатием `Z`;
- корректный cleanup WAV/MP3, записи, playback и очереди при abort/reload.

### Транспорт

- стандартное, online и favorites меню транспорта;
- spawn и избранное;
- тюнинг и сохранённые конфигурации;
- ремонт, принудительные фары, поворотники;
- nitro;
- speed limiter;
- сирены;
- online radio unlock;
- online traffic;
- сохранение/восстановление машин мира;
- inflatable boat сценарий.

### Игрок / NPC / мир

- weapon menu;
- clothing/outfit функциональность;
- player interaction menu;
- NoClip;
- camera lock;
- animal morph;
- bullet time;
- telekinesis;
- bong interaction;
- companions и chauffeur cruise;
- police suppression / police officer interaction;
- ped physics;
- North Yankton loader + ambient population.

## Управление

Основной источник истины для клавиш — `Input/InputRouter.cs`, AI-ввод обрабатывается в `Ai/AiController.cs`.

| Клавиша | Действие |
|---|---|
| `F5` | открыть/закрыть встроенную справку |
| `Z` удерживать | говорить с ближайшим NPC; повторное нажатие отменяет текущий AI-запрос |
| `X` | тюнинг в машине / player interaction menu пешком |
| `O` | стандартное меню транспорта |
| `[` | online transport menu |
| `]` | favorites transport menu |
| `N` | ремонт текущей машины |
| `H` | принудительные фары |
| `Num7` / `Num9` | поворотники |
| `Num3` | создать компаньона |
| `Num1` | chauffeur cruise |
| `Num6` | отпустить компаньонов |
| `L` | weapon menu |
| `Num .` / `Num ,` | clothing menu |
| `B` | police suppression |
| `Y` | загрузить North Yankton |
| `U` | включить/выключить население North Yankton |
| `J` | NoClip |
| `T` | Bullet Time |
| `K` | ударить ближайших NPC |
| `0` | camera lock |
| `7` | сохранить/убрать текущую машину из world vehicle store |
| `8` | bong interaction |
| `9` | speed limiter |
| `Num +` | увеличить лимит скорости |
| `E` пешком | quick command для police officer |

Внутри меню используются `Num8/Num2`, `Num7/Num9`, `Num5`, `Num4/Num6`, `Num0`, `Esc/Back` в зависимости от конкретного меню.

## Структура кода

```text
Ai/          AI dialog pipeline, NPC identity/memory, recording/playback
Core/        paths, settings, logging, notifications, menu primitives/help
Input/       global keyboard routing
Peds/        companions, police, ped queries/physics
Player/      player abilities, weapons, clothing, menus
Vehicles/    spawning, menus, tuning, traffic, radio and vehicle mechanics
World/       North Yankton loading and ambient world logic
MainScript.cs composition root + ScriptHookVDotNet lifecycle
```

Подробная карта runtime-потоков и ответственности: [`docs/CODE_OVERVIEW.md`](docs/CODE_OVERVIEW.md).

Исторические планы распределены по статусам:

- `done/` — уже реализованные планы;
- `research/` — исследование/эксперименты;
- `not_impl/` — запланировано, но ещё не реализовано.

## AI-конфигурация

Рабочий runtime-файл с ключами не хранится в Git.

1. Скопируйте `ai_settings.example.json` в `scripts/ai_settings.json`.
2. Укажите реальные API keys.
3. Выберите `ActiveProvider`.
4. При необходимости включите `ProactiveEnabled`.

Пример содержит текущие дефолты:

- OpenAI: `gpt-4o-mini`;
- Google: `gemini-2.5-flash`;
- ElevenLabs: voice IDs для TTS.

`ai_settings.json` и `scripts/ai_settings.json` находятся в `.gitignore`. Placeholder `YOUR_*` не считается валидным ключом.

## Сборка

Требования:

- Windows;
- .NET Framework 4.8 developer/targeting pack;
- NuGet/MSBuild;
- `ScriptHookVDotNet3.dll` из совместимой с вашей GTA версии ScriptHookVDotNet.

Восстановление пакетов:

```powershell
nuget restore gta.sln
```

### Рекомендуемый вариант: явный путь к DLL

```powershell
msbuild gta.csproj /p:Configuration=Release /p:Platform="AnyCPU" /p:ScriptHookVDotNetPath="C:\path\to\ScriptHookVDotNet3.dll"
```

### Через GTA directory

Если DLL лежит в корне GTA:

```powershell
msbuild gta.csproj /p:Configuration=Release /p:Gta5Path="C:\Games\Grand Theft Auto V"
```

### Локальный `gta.csproj.user`

Можно создать untracked файл рядом с проектом:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <Gta5Path>C:\Games\Grand Theft Auto V</Gta5Path>
  </PropertyGroup>
</Project>
```

### Деплой в GTA

```powershell
msbuild gta.csproj /p:Configuration=Release /p:Gta5Path="C:\Games\Grand Theft Auto V" /p:DeployToGta=true
```

Обычная сборка пишет результат только в `bin\Debug` / `bin\Release` и не зависит от локальной структуры каталогов разработчика.

## CI

`.github/workflows/build.yml` выполняет на чистом Windows runner:

1. `nuget restore gta.sln`;
2. скачивание **закреплённого** `ScriptHookVDotNetEnhanced v1.1.0.6`;
3. проверку SHA-256 release-архива;
4. Debug build;
5. Release build.

Binary dependency в Git не коммитится.

## Runtime / threading

`MainScript` является composition root и единственной точкой ScriptHookVDotNet lifecycle (`Tick`, `KeyDown`, `KeyUp`, `Aborted`).

GTA API и сущности должны использоваться на script thread. AI/network/audio/file операции могут выполняться асинхронно, но GTA mutations возвращаются через очередь `AiController.ProcessQueue()` и повторно валидируют entity handle перед применением.

`OnAborted()` останавливает AI, запись/воспроизведение, временные файлы и stateful gameplay services.

## Работа coding agents

Перед изменениями агент должен прочитать [`AGENTS.md`](AGENTS.md). Узкие правила находятся в `.agent/skills/`:

- architecture;
- implementation;
- code-review;
- gta-scripthook.

Правила нейтральны к Codex / Claude / Gemini и другим coding agents.
