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

Для сборки требуется .NET Framework 4.8 и внешняя зависимость `ScriptHookVDotNet3.dll`.

### 1. Сборка с указанием пути к GTA V / ScriptHookVDotNet:
Укажите путь к установленной папке GTA V (содержащей `ScriptHookVDotNet3.dll`):
```bash
msbuild gta.csproj /p:Configuration=Release /p:Gta5Path="C:\Games\Grand Theft Auto V"
```

### 2. Сборка через локальный `gta.csproj.user`:
Создайте untracked файл `gta.csproj.user` рядом с `gta.csproj`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <Gta5Path>C:\Games\Grand Theft Auto V</Gta5Path>
  </PropertyGroup>
</Project>
```
После этого стандартная сборка `msbuild gta.csproj /p:Configuration=Release` подхватит путь автоматически.

### 3. Автоматический деплой в папку `scripts`:
```bash
msbuild gta.csproj /p:Gta5Path="C:\Games\Grand Theft Auto V" /p:DeployToGta=true
```
