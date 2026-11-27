# Build Script for Dexplay — Native Desktop Music Island
# Compiles src\Dexplay.cs into standalone Dexplay.exe
# Supports optional Authenticode code signing for Windows Smart App Control compatibility.
# Language: PowerShell 5.1 / 7+

param(
    [string]$CertThumbprint = $env:DEXPLAY_SIGN_CERT_THUMBPRINT,
    [string]$PfxPath = $env:DEXPLAY_SIGN_PFX_PATH,
    [string]$PfxPassword = $env:DEXPLAY_SIGN_PFX_PASSWORD,
    [string]$TimestampServer = "http://timestamp.digicert.com"
)

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
$ManifestFile = Join-Path $ProjectRoot "assets\app.manifest"

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
if (Test-Path $ManifestFile) {
    $CompilerArgs += "/win32manifest:$ManifestFile"
    Write-Host "[OK] Embedding DPI-aware Win32 manifest: $ManifestFile" -ForegroundColor Green
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

    # 4. Optional Authenticode Code Signing (Required when Smart App Control is enabled)
    $signTargets = @(
        $TargetExe,
        (Join-Path $ProjectRoot "native\audio_probe.exe"),
        (Join-Path $ProjectRoot "native\title_cleaner.dll")
    ) | Where-Object { Test-Path $_ }

    $cert = $null
    if (-not [string]::IsNullOrWhiteSpace($PfxPath) -and (Test-Path $PfxPath)) {
        if (-not [string]::IsNullOrWhiteSpace($PfxPassword)) {
            $secPwd = ConvertTo-SecureString -String $PfxPassword -AsPlainText -Force
            $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($PfxPath, $secPwd)
        } else {
            $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($PfxPath)
        }
    } elseif (-not [string]::IsNullOrWhiteSpace($CertThumbprint)) {
        $cert = Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My -CodeSigningCert -ErrorAction SilentlyContinue |
                Where-Object { $_.Thumbprint -eq $CertThumbprint.Replace(" ", "").ToUpper() } |
                Select-Object -First 1
    }

    if ($null -ne $cert) {
        Write-Host "[+] Signing binaries with Authenticode certificate ($($cert.Subject))..." -ForegroundColor Cyan
        foreach ($bin in $signTargets) {
            $sig = Set-AuthenticodeSignature -FilePath $bin -Certificate $cert -HashAlgorithm SHA256 -TimestampServer $TimestampServer
            Write-Host "    $bin -> $($sig.Status)" -ForegroundColor Gray
        }
    } else {
        $sacState = (Get-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy" -ErrorAction SilentlyContinue).VerifiedAndReputablePolicyState
        if ($sacState -eq 1) {
            Write-Host "`n[NOTE] Windows Smart App Control is ON (VerifiedAndReputablePolicyState = 1)." -ForegroundColor Yellow
            Write-Host "       Unsigned local builds are blocked by Smart App Control." -ForegroundColor Yellow
            Write-Host "       - For distribution: sign with a publicly trusted CA / Azure Trusted Signing / SignPath" -ForegroundColor Gray
            Write-Host "         via -CertThumbprint or -PfxPath." -ForegroundColor Gray
            Write-Host "       - For local unsigned testing: Settings > Privacy & security > Windows Security >" -ForegroundColor Gray
            Write-Host "         App & browser control > Smart App Control settings > Off." -ForegroundColor Gray
        }
    }
} else {
    Write-Error "Compilation failed."
    exit 1
}
