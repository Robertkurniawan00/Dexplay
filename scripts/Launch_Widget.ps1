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

try {
    Start-Process -FilePath $ExePath -WorkingDirectory $ProjectRoot
    Write-Host "`n[SUCCESS] Dexplay is now active!" -ForegroundColor Green
    Write-Host "Floating on your monitor without any browser or web server." -ForegroundColor Gray
} catch {
    $msg = $_.Exception.Message
    if ($msg -match "Application Control|Smart App|blocked") {
        Write-Host "`n[BLOCKED] Windows Smart App Control blocked Dexplay.exe." -ForegroundColor Red
        Write-Host "Reason : Dexplay.exe is unsigned and Windows Smart App Control is currently ON." -ForegroundColor Yellow
        Write-Host "Fixes  :" -ForegroundColor Cyan
        Write-Host "  1. Sign Dexplay.exe with a publicly trusted Code Signing cert (DigiCert/Sectigo/Azure Trusted Signing/SignPath):" -ForegroundColor Gray
        Write-Host "     powershell -ExecutionPolicy Bypass -File scripts\build_pure_widget.ps1 -CertThumbprint <THUMBPRINT>" -ForegroundColor Gray
        Write-Host "  2. Or if running your own local unsigned builds on this PC, set Smart App Control to Off:" -ForegroundColor Gray
        Write-Host "     Windows Security > App & browser control > Smart App Control settings > Off" -ForegroundColor Gray
        exit 1
    }
    throw
}
