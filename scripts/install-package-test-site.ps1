# Creates a fresh Umbraco 17 site and installs Umbraco.Automate + Automate.Salesforce.Connector
# from the locally packed .nupkg files (see scripts/pack-release.ps1) plus nuget.org for
# Umbraco.Automate's own dependencies. This is the real "can an implementer actually install
# this from NuGet" check — everything else in this repo up to now (the demo site under
# demos/v17/Automate.Salesforce.Connector.DemoSite) builds via ProjectReference to a sibling
# monorepo checkout, which proves the code works but not that the package does.
#
# Mirrors the real monorepo's scripts/install-package-test-site.ps1, adapted to install from
# a local folder feed (this package isn't on any public feed yet) instead of MyGet/nuget.org.

param(
    [string]$SiteName = "Automate.Salesforce.Connector.PackageTestSite",
    [string]$LocalFeedPath = "artifacts/nupkg",
    [switch]$Force,
    [switch]$SkipPack
)

$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Push-Location $RepoRoot

if (-not $SkipPack) {
    & (Join-Path $PSScriptRoot "pack-release.ps1") -OutputDirectory $LocalFeedPath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$nupkg = Get-ChildItem $LocalFeedPath -Filter "Automate.Salesforce.Connector.*.nupkg" |
    Select-Object -First 1
if (-not $nupkg) {
    Write-Host "ERROR: No Automate.Salesforce.Connector package found in $LocalFeedPath — run pack-release.ps1 first." -ForegroundColor Red
    exit 1
}
if ($nupkg.Name -notmatch "^Automate\.Salesforce\.Connector\.(.+)\.nupkg$") {
    Write-Host "ERROR: Could not parse version from $($nupkg.Name)" -ForegroundColor Red
    exit 1
}
$SalesforceVersion = $matches[1]
Write-Host "Using Automate.Salesforce.Connector version: $SalesforceVersion" -ForegroundColor Gray

$sitePath = "demos/v17/$SiteName"
if ((Test-Path $sitePath) -and -not $Force) {
    Write-Host "Site folder '$sitePath' already exists. Use -Force to recreate." -ForegroundColor Yellow
    exit 0
}
if ($Force -and (Test-Path $sitePath)) {
    Write-Host "Removing existing site folder '$sitePath'..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $sitePath
}

New-Item -ItemType Directory -Path "demos/v17" -Force | Out-Null

Write-Host "Creating fresh Umbraco site '$SiteName'..." -ForegroundColor Green
Push-Location "demos/v17"
dotnet new umbraco --force -n $SiteName --friendly-name "Administrator" --email "admin@example.com" --password "password1234" --development-database-type SQLite
Pop-Location

Write-Host "Configuring NuGet sources (local feed for Salesforce packages, nuget.org for everything else)..." -ForegroundColor Green
$absoluteFeedPath = (Resolve-Path $LocalFeedPath).Path
$nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-salesforce" value="$absoluteFeedPath" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="local-salesforce">
      <package pattern="Automate.Salesforce.Connector*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@
$nugetConfig | Out-File -FilePath "$sitePath/nuget.config" -Encoding utf8 -Force

Write-Host "Installing Umbraco.Automate (from nuget.org) and Automate.Salesforce.Connector (from local feed)..." -ForegroundColor Green
Push-Location $sitePath
dotnet add package Umbraco.Automate --version 17.2.0
dotnet add package Automate.Salesforce.Connector --version $SalesforceVersion
Pop-Location

Write-Host "Adding placeholder Salesforce Connected App config (replace with real values before authenticating)..." -ForegroundColor Green
$devSettingsPath = "$sitePath/appsettings.Development.json"
$devSettings = Get-Content $devSettingsPath -Raw | ConvertFrom-Json
$devSettings.Umbraco.CMS | Add-Member -NotePropertyName "Global" -NotePropertyValue ([PSCustomObject]@{ DisableElectionForSingleServer = $true }) -Force
$devSettings.Umbraco.CMS | Add-Member -NotePropertyName "WebRouting" -NotePropertyValue ([PSCustomObject]@{ UmbracoApplicationUrl = "https://localhost:44399/" }) -Force
$devSettings.Umbraco | Add-Member -NotePropertyName "Automate" -NotePropertyValue ([PSCustomObject]@{
    # Automate requires its own connection string — without this, the site fails to start with
    # "Umbraco Automate requires a database connection string named 'umbracoAutomateDbDSN'".
    # Sharing the CMS's own SQLite database is the simplest option for a local test site.
    UseNamedConnectionString = "umbracoDbDSN"
    Providers = [PSCustomObject]@{ Salesforce = [PSCustomObject]@{ ClientId = "REPLACE_ME"; ClientSecret = "REPLACE_ME" } }
    Salesforce = [PSCustomObject]@{ ApiVersion = "v61.0" }
}) -Force
$devSettings | ConvertTo-Json -Depth 10 | Out-File -FilePath $devSettingsPath -Encoding utf8 -Force

Write-Host ""
Write-Host "=== Setup complete ===" -ForegroundColor Cyan
Write-Host "Site location: $sitePath" -ForegroundColor Gray
Write-Host "  1. Add real Salesforce Connected App ClientId/ClientSecret to appsettings.Development.json" -ForegroundColor Yellow
Write-Host "  2. cd $sitePath && dotnet run --urls https://localhost:44399" -ForegroundColor Gray
Write-Host "  3. Open https://localhost:44399/umbraco (admin@example.com / password1234)" -ForegroundColor Gray
Write-Host "  4. Automation > Connections > Create — confirm Salesforce appears" -ForegroundColor Gray
Write-Host "  5. Automation > (any automation) > add a step — confirm all 6 Salesforce actions appear, zero triggers" -ForegroundColor Gray

Pop-Location
