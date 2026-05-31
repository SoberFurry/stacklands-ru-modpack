chcp 65001 | Out-Null
$ErrorActionPreference = "Continue"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$desktop   = [Environment]::GetFolderPath("Desktop")
$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm"
$logPath   = Join-Path $desktop "Stacklands_Install_Log_$timestamp.txt"
$modsBase  = "C:\Users\VKoti\AppData\LocalLow\sokpop\Stacklands\Mods"
$backupBase = Join-Path $modsBase "backup"

function Write-Log {
    param([string]$msg, [string]$color = "White")
    $line = "[$(Get-Date -Format 'HH:mm:ss')] $msg"
    Write-Host $line -ForegroundColor $color
    Add-Content -Path $logPath -Value $line -Encoding UTF8
}

# ── Проверка: не запускать из ZIP / Temp ──────────────────────────────────────
if ($scriptDir -match "Temp|\.zip|AppData\\Local\\Temp") {
    Write-Log "ОШИБКА: Скрипт запущен из временной папки или ZIP. Распакуйте сначала!" Red
    Read-Host "Нажмите Enter для выхода"
    exit 1
}

Write-Log "=== Stacklands Mod Pack Installer ===" Cyan
Write-Log "Рабочая папка: $scriptDir"

# ── Проверка .NET SDK ─────────────────────────────────────────────────────────
$dotnet = where.exe dotnet 2>$null
if (-not $dotnet) {
    Write-Log "ПРЕДУПРЕЖДЕНИЕ: dotnet SDK не найден." Yellow
    Write-Log "Установить? (Y/N)" Yellow
    $ans = Read-Host
    if ($ans -eq "Y" -or $ans -eq "y") {
        winget install Microsoft.DotNet.SDK.7 --silent
    }
}

# ── Создать нужные папки ──────────────────────────────────────────────────────
New-Item -ItemType Directory -Force $modsBase | Out-Null
New-Item -ItemType Directory -Force $backupBase | Out-Null

# ── Функция установки мода ────────────────────────────────────────────────────
function Install-Mod {
    param(
        [string]$ModName,
        [string]$SourceDir
    )
    $destDir = Join-Path $modsBase $ModName
    $backupDir = Join-Path $backupBase "${ModName}_$(Get-Date -Format 'yyyy-MM-dd')"

    Write-Log "--- Устанавливаю $ModName ---" Cyan

    # Backup existing
    if (Test-Path $destDir) {
        New-Item -ItemType Directory -Force $backupDir | Out-Null
        Copy-Item -Path "$destDir\*" -Destination $backupDir -Recurse -Force
        Write-Log "  Старая версия сохранена: $backupDir" Green
    }

    # Install
    New-Item -ItemType Directory -Force $destDir | Out-Null
    if (-not (Test-Path $SourceDir)) {
        Write-Log "  ОШИБКА: Исходная папка не найдена: $SourceDir" Red
        return
    }
    Copy-Item -Path "$SourceDir\*" -Destination $destDir -Recurse -Force
    Write-Log "  Установлен: $destDir" Green
}

# ── BetterSideBar ─────────────────────────────────────────────────────────────
$bsDir = Join-Path $scriptDir "..\BetterSideBar"
$bsBinDir = Join-Path $bsDir "bin\Release"

if (Test-Path (Join-Path $bsBinDir "BetterSideBar.dll")) {
    # Copy compiled DLL + assets to a staging folder
    $bsStage = Join-Path $env:TEMP "BSStage"
    New-Item -ItemType Directory -Force $bsStage | Out-Null
    Copy-Item "$bsBinDir\BetterSideBar.dll" $bsStage -Force
    Copy-Item "$bsDir\manifest.json" $bsStage -Force -ErrorAction SilentlyContinue
    Copy-Item "$bsDir\config.json" $bsStage -Force -ErrorAction SilentlyContinue
    Copy-Item "$bsDir\Icons" $bsStage -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item "$bsDir\Blueprints" $bsStage -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item "$bsDir\Boosterpacks" $bsStage -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item "$bsDir\Cards" $bsStage -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item "$bsDir\Sounds" $bsStage -Recurse -Force -ErrorAction SilentlyContinue
    Install-Mod "BetterSideBar" $bsStage
} else {
    # Build first
    Write-Log "BetterSideBar.dll не найден, собираю проект..." Yellow
    Push-Location $bsDir
    dotnet build -c Release 2>&1 | Write-Host
    Pop-Location
    if (Test-Path (Join-Path $bsBinDir "BetterSideBar.dll")) {
        Install-Mod "BetterSideBar" $bsBinDir
    } else {
        Write-Log "ОШИБКА: Сборка BetterSideBar не удалась." Red
    }
}

# ── RecipeInspector ───────────────────────────────────────────────────────────
$riDir = Join-Path $scriptDir "..\RecipeInspector"
$riBinDir = Join-Path $riDir "bin\Release"

if (-not (Test-Path (Join-Path $riBinDir "RecipeInspector.dll"))) {
    Write-Log "RecipeInspector.dll не найден, собираю..." Yellow
    Push-Location $riDir
    dotnet build -c Release 2>&1 | Write-Host
    Pop-Location
}

if (Test-Path (Join-Path $riBinDir "RecipeInspector.dll")) {
    $riStage = Join-Path $env:TEMP "RIStage"
    New-Item -ItemType Directory -Force $riStage | Out-Null
    Copy-Item "$riBinDir\RecipeInspector.dll" $riStage -Force
    Copy-Item "$riDir\manifest.json" $riStage -Force
    Install-Mod "RecipeInspector" $riStage
} else {
    Write-Log "ОШИБКА: RecipeInspector.dll не собрался." Red
}

Write-Log "=== Установка завершена ===" Cyan
Write-Log "Лог: $logPath"
Write-Host ""
Read-Host "Нажмите Enter для выхода"
