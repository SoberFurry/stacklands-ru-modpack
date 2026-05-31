chcp 65001 | Out-Null
$ErrorActionPreference = "Stop"

$savePath = "C:\Users\VKoti\AppData\LocalLow\sokpop\Stacklands"
$desktop   = [Environment]::GetFolderPath("Desktop")
$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm"
$zipName   = "Stacklands_Save_Backup_$timestamp.zip"
$zipPath   = Join-Path $desktop $zipName
$logPath   = Join-Path $desktop "Stacklands_Backup_Log_$timestamp.txt"

function Write-Log {
    param([string]$msg)
    $line = "[$(Get-Date -Format 'HH:mm:ss')] $msg"
    Write-Host $line
    Add-Content -Path $logPath -Value $line -Encoding UTF8
}

try {
    if (-not (Test-Path $savePath)) {
        throw "Папка сохранений не найдена: $savePath"
    }

    Write-Log "Начинаем бэкап..."
    Write-Log "Источник: $savePath"
    Write-Log "Назначение: $zipPath"

    # Создаём ZIP без удаления оригинала
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($savePath, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

    Write-Log "Бэкап создан: $zipPath"
    Write-Host ""
    Write-Host "Бэкап создан: $zipPath" -ForegroundColor Green
}
catch {
    Write-Log "ОШИБКА: $_"
    Write-Host "ОШИБКА: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "Нажмите любую клавишу для выхода..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
