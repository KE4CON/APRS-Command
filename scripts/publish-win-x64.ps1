param(
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

$supported = @("win-x64")
if ($supported -notcontains $RuntimeIdentifier) {
    throw "Unsupported runtime identifier for this script: $RuntimeIdentifier"
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")
$solution = Join-Path $repoRoot "CrossPlatformAprs.sln"
$project = Join-Path $repoRoot "src/Aprs.Desktop/Aprs.Desktop.csproj"
$output = Join-Path $repoRoot "artifacts/publish/$RuntimeIdentifier"

function Require-Path {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path $Path)) {
        throw "Could not locate $Description at: $Path. Run this script from the APRS Command repository, or keep scripts/ beside the repository root."
    }
}

Require-Path $solution "solution file"
Require-Path $project "desktop project file"
Require-Path (Join-Path $repoRoot "README.md") "README.md"
Require-Path (Join-Path $repoRoot "src") "src directory"
Require-Path (Join-Path $repoRoot "docs") "docs directory"
Require-Path (Join-Path $repoRoot "tests") "tests directory"

Write-Host "Publishing APRS Command for $RuntimeIdentifier"
Write-Host "Repository root: $repoRoot"
Write-Host "Solution: $solution"
Write-Host "Desktop project: $project"
dotnet restore $solution
dotnet build $solution -c Release --no-restore
dotnet test $solution -c Release --no-build
dotnet publish $project -c Release -r $RuntimeIdentifier --self-contained true -o $output /p:PublishSingleFile=false /p:DebugType=none /p:DebugSymbols=false

Write-Host "APRS Command publish output: $output"
