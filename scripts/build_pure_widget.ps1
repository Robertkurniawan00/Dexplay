# Build Script for Dexplay — Native Desktop Music Island
# Compiles src\Dexplay.cs into standalone Dexplay.exe
# Language: PowerShell 5.1 / 7+

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
Set-Location $ProjectRoot

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "       BUILDING DEXPLAY DESKTOP MUSIC ISLAND (Dexplay.exe)  " -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

# 1. Locate C# Compiler (csc.exe)
$CscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $CscPath)) {
    $CscPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $CscPath)) {
    Write-Error "Microsoft .NET C# Compiler (csc.exe) not found."
    exit 1
}
Write-Host "[OK] Using C# Compiler: $CscPath" -ForegroundColor Green

# 2. Source and Target
$SourceFile = Join-Path $ProjectRoot "src\Dexplay.cs"
$TargetExe = Join-Path $ProjectRoot "Dexplay.exe"
$IconFile = Join-Path $ProjectRoot "assets\dexplay.ico"

# Safely close running instance if any
Get-Process -Name "Dexplay", "MusicNotchWidget" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 200

if (-not (Test-Path $SourceFile)) {
    Write-Error "Source file not found: $SourceFile"
    exit 1
}

# 3. Compile as Native Windows GUI Application (/t:winexe -> No Console Window!)
Write-Host "[+] Compiling C# source code into standalone Windows executable..." -ForegroundColor Cyan

$WpfDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
if (-not (Test-Path $WpfDir)) {
    $WpfDir = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\WPF"
}

$References = @(
    "System.dll",
    "System.Windows.Forms.dll",
    "System.Drawing.dll",
    (Join-Path $WpfDir "PresentationCore.dll"),
    (Join-Path $WpfDir "WindowsBase.dll")
) -join ","

$CompilerArgs = @(
    "/nologo",
    "/target:winexe",
    "/warn:4",
    "/optimize+",
    "/platform:anycpu",
    "/codepage:65001",
    "/utf8output"
)
if (Test-Path $IconFile) {
    $CompilerArgs += "/win32icon:$IconFile"
}
$CompilerArgs += @(
    "/r:$References",
    "/out:$TargetExe",
    $SourceFile
)

& $CscPath $CompilerArgs

if ($LASTEXITCODE -eq 0 -and (Test-Path $TargetExe)) {
    $sizeKb = [math]::Round((Get-Item $TargetExe).Length / 1KB, 1)
    Write-Host "`n[SUCCESS] Dexplay.exe built successfully ($sizeKb KB)!" -ForegroundColor Green
    Write-Host "Output: $TargetExe" -ForegroundColor Cyan
} else {
    Write-Error "Compilation failed."
    exit 1
}
