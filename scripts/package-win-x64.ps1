$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$bash = Get-Command bash -ErrorAction SilentlyContinue
if ($null -eq $bash) {
    throw "bash is required to run the shared portable packaging script."
}

& $bash.Source (Join-Path $scriptRoot "package-runtime.sh") "win-x64"
