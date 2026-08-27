# gta5-scripts

GTA V scripts and related tooling (ScriptHookVDotNet).

## Настройка AI-интеграции

1. Скопируйте шаблон конфигурации `ai_settings.example.json` в целевое расположение:
   ```bash
   cp ai_settings.example.json scripts/ai_settings.json
   ```
2. Откройте `scripts/ai_settings.json` и укажите ваши реальные API ключи (OpenAI, Google Gemini, ElevenLabs).
3. Файл `scripts/ai_settings.json` и локальный `ai_settings.json` добавлены в `.gitignore` и не попадут в репозиторий.

## Сборка проекта

Для сборки требуется .NET Framework 4.8 и ссылка на `ScriptHookVDotNet3.dll`.

### Базовая сборка:
```bash
msbuild gta.csproj /p:Configuration=Release
```

### Сборка с кастомным путем к GTA V / ScriptHookVDotNet:
Вы можете переопределить свойство `Gta5Path` через параметры командной строки, файл `gta.csproj.user` или переменную окружения:
```bash
msbuild gta.csproj /p:Gta5Path="C:\Games\Grand Theft Auto V"
```

### Автоматический деплой в папку `scripts`:
```bash
msbuild gta.csproj /p:Gta5Path="C:\Games\Grand Theft Auto V" /p:DeployToGta=true
```
