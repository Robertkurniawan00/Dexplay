# Dexplay — Native Desktop Music Island Launcher
# Language: PowerShell 5.1 / 7+

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
Set-Location $ProjectRoot

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "             DEXPLAY — DESKTOP MUSIC ISLAND                 " -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

$ExePath = Join-Path $ProjectRoot "Dexplay.exe"
$SourcePath = Join-Path $ProjectRoot "src\Dexplay.cs"

$needsRebuild = $false
if (-not (Test-Path $ExePath)) {
    $needsRebuild = $true
} elseif (Test-Path $SourcePath) {
    if ((Get-Item $SourcePath).LastWriteTime -gt (Get-Item $ExePath).LastWriteTime) {
        $needsRebuild = $true
    }
}

if ($needsRebuild) {
    Write-Host "[+] Compiling Dexplay executable..." -ForegroundColor Cyan
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $ScriptDir "build_pure_widget.ps1")
}

Write-Host "[OK] Verified Native Executable: $ExePath" -ForegroundColor Green
Write-Host "[+] Launching Dexplay.exe on Windows Desktop..." -ForegroundColor Cyan

Start-Process -FilePath $ExePath -WorkingDirectory $ProjectRoot

Write-Host "`n[SUCCESS] Dexplay is now active!" -ForegroundColor Green
Write-Host "Floating on your monitor without any browser or web server." -ForegroundColor Gray
