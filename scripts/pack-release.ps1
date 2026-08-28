# Packs the Umbraco.Automate.Salesforce NuGet package with the correct release-mode dependency
# pins.
#
# Why this script exists: packing with the default UseProjectReferences=true (the dev-mode
# default, active whenever the sibling ../Umbraco.Automate monorepo checkout exists — see the
# root README's "Repository home" section) bakes in whatever local preview version
# Nerdbank.GitVersioning computed for that checkout's Umbraco.Automate.Core/Umbraco.Automate.OpenIddict
# projects, instead of the real published version range pinned in Directory.Packages.props. A
# package built that way restores fine on this machine and nowhere else.
# -p:UseProjectReferences=false forces the real PackageReference path — confirmed by inspecting
# the resulting .nuspec after packing both ways.

param(
    [string]$OutputDirectory = "artifacts/nupkg",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Push-Location $RepoRoot

$Projects = @(
    "src/Umbraco.Automate.Salesforce/Umbraco.Automate.Salesforce.csproj"
)

if (-not $SkipBuild) {
    Write-Host "Building solution with real (non-project-reference) dependencies..." -ForegroundColor Green
    dotnet build Umbraco.Automate.Salesforce.slnx --configuration Release -p:UseProjectReferences=false
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed — not packing." -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

foreach ($project in $Projects) {
    Write-Host "Packing $project ..." -ForegroundColor Green
    dotnet pack $project `
        --configuration Release `
        --no-build `
        --output $OutputDirectory `
        -p:UseProjectReferences=false
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Pack failed for $project" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

Write-Host ""
Write-Host "=== Packed ===" -ForegroundColor Cyan
Get-ChildItem $OutputDirectory -Filter "*.nupkg" | ForEach-Object { Write-Host "  $($_.Name)" -ForegroundColor Gray }
Write-Host ""
Write-Host "See scripts/install-package-test-site.ps1 to verify this actually installs before publishing." -ForegroundColor Yellow

Pop-Location
