param([switch]$ProductOnly)
$ErrorActionPreference = 'Stop'
$placementBuild = Join-Path (Split-Path $PSScriptRoot -Parent) '.build\h-location-placement'
New-Item -ItemType Directory -Force -Path "$placementBuild\temp", "$placementBuild\cli", "$placementBuild\http" | Out-Null
$env:DOTNET_CLI_HOME = "$placementBuild\cli"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_HTTP_CACHE_PATH = "$placementBuild\http"
$env:TEMP = "$placementBuild\temp"
$env:TMP = "$placementBuild\temp"
dotnet build (Join-Path $PSScriptRoot 'MainGameHLocationPlacement.csproj') -c Release --nologo -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if ($ProductOnly) { exit 0 }
dotnet build (Join-Path $PSScriptRoot 'Tests\Tests.csproj') -c Release --nologo -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build (Join-Path $PSScriptRoot 'Tests\ConnectionProbe\ConnectionProbe.csproj') -c Release --nologo -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& (Join-Path $PSScriptRoot 'Tests\bin\Release\net472\Tests.exe')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& (Join-Path $PSScriptRoot 'Tests\ConnectionProbe\bin\Release\net472\ConnectionProbe.exe')
exit $LASTEXITCODE
