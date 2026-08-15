# ============================================================
# APRS Command — Windows code signing (Azure Artifact Signing)
#
# OPTIONAL and CREDENTIAL-GATED. Signing is OFF by default.
# This script signs the file(s) you pass ONLY when all of these are true:
#   1. The three AZURE_SIGN_* environment variables are set (see below).
#   2. You are logged in to Azure   (run:  az login).
#   3. The 'sign' tool is installed  (dotnet tool install -g --prerelease sign).
#
# If any of those is missing, it prints a warning and SKIPS signing,
# leaving UNSIGNED output so the same build pipeline still succeeds.
# Unsigned apps trigger the Windows SmartScreen "unknown publisher"
# warning — users click "More info" then "Run anyway".
#
# ENABLE SIGNING (set these once per PowerShell session, your real values):
#   $env:AZURE_SIGN_ENDPOINT = "https://eus.codesigning.azure.net/"
#   $env:AZURE_SIGN_ACCOUNT  = "aprscommandsign"
#   $env:AZURE_SIGN_PROFILE  = "rospopo-public"
#   az login
#
# No certificates, keys, or passwords are handled here — Azure holds the
# certificate and issues a short-lived one at signing time.
#
# USAGE:
#   pwsh scripts/sign-windows.ps1 "path\to\App.exe" ["path\to\Setup.exe" ...]
# ============================================================
param(
    [Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)]
    [string[]] $Files
)

$ErrorActionPreference = "Stop"

$endpoint = $env:AZURE_SIGN_ENDPOINT
$account  = $env:AZURE_SIGN_ACCOUNT
$profile  = $env:AZURE_SIGN_PROFILE

# ── Gate 1: signing configured? ───────────────────────────────────────────────
if (-not $endpoint -or -not $account -or -not $profile) {
    Write-Warning "Code signing not configured (AZURE_SIGN_* env vars unset) - SKIPPING. Output will be UNSIGNED."
    exit 0
}

# ── Gate 2: 'sign' tool available? ────────────────────────────────────────────
if (-not (Get-Command sign -ErrorAction SilentlyContinue)) {
    Write-Warning "'sign' tool not found - SKIPPING signing. Install with: dotnet tool install -g --prerelease sign"
    exit 0
}

# ── Gate 3: logged in to Azure? ───────────────────────────────────────────────
$loggedIn = $false
try {
    az account show 1>$null 2>$null
    if ($LASTEXITCODE -eq 0) { $loggedIn = $true }
} catch { $loggedIn = $false }

if (-not $loggedIn) {
    Write-Warning "Not logged in to Azure (run 'az login') - SKIPPING signing. Output will be UNSIGNED."
    exit 0
}

# ── Sign each file ────────────────────────────────────────────────────────────
$failed = $false
foreach ($file in $Files) {
    if (-not (Test-Path $file)) {
        Write-Warning "File not found, skipping: $file"
        continue
    }
    Write-Host "Signing: $file" -ForegroundColor Cyan
    sign code artifact-signing "$file" `
        --azure-credential-type azure-cli `
        --artifact-signing-endpoint  "$endpoint" `
        --artifact-signing-account   "$account" `
        --artifact-signing-certificate-profile "$profile" `
        --verbosity warning
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Signed OK: $file" -ForegroundColor Green
    } else {
        Write-Warning "  Signing FAILED for: $file"
        $failed = $true
    }
}

if ($failed) { exit 1 }
