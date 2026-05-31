chcp 65001 | Out-Null
$ErrorActionPreference = "Continue"

$modsBase   = "C:\Users\VKoti\AppData\LocalLow\sokpop\Stacklands\Mods"
$backupBase = Join-Path $modsBase "backup"
$desktop    = [Environment]::GetFolderPath("Desktop")
$timestamp  = Get-Date -Format "yyyy-MM-dd_HH-mm"
$logPath    = Join-Path $desktop "Stacklands_Rollback_Log_$timestamp.txt"

function Write-Log {
    param([string]$msg, [string]$color = "White")
    $line = "[$(Get-Date -Format 'HH:mm:ss')] $msg"
    Write-Host $line -ForegroundColor $color
    Add-Content -Path $logPath -Value $line -Encoding UTF8
}

Write-Log "=== Stacklands Mod Rollback ===" Cyan

$modNames = @("BetterSideBar", "RecipeInspector")

foreach ($modName in $modNames) {
    # Find latest backup
    $backups = Get-ChildItem $backupBase -Directory |
        Where-Object { $_.Name -like "${modName}_*" } |
        Sort-Object Name -Descending

    if ($backups.Count -eq 0) {
        Write-Log "$modName — бэкап не найден, пропускаем." Yellow
        continue
    }

    $latest = $backups[0]
    Write-Log "$modName — найден бэкап: $($latest.Name)" Cyan
    Write-Host "Откатить $modName до $($latest.Name)? (Y/N)" -ForegroundColor Yellow
    $ans = Read-Host
    if ($ans -ne "Y" -and $ans -ne "y") {
        Write-Log "$modName — откат отменён пользователем." Yellow
        continue
    }

    $destDir = Join-Path $modsBase $modName
    # Remove current
    if (Test-Path $destDir) {
        Remove-Item "$destDir\*" -Recurse -Force
    }
    # Restore backup
    Copy-Item -Path "$($latest.FullName)\*" -Destination $destDir -Recurse -Force
    Write-Log "$modName — откат выполнен." Green
}

Write-Log "=== Откат завершён ===" Cyan
Write-Host ""
Read-Host "Нажмите Enter для выхода"
