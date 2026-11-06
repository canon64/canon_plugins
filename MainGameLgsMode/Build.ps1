param([switch]$Tests)
$ErrorActionPreference = 'Stop'
Write-Host 'Building MainGameLgsMode'
$env:DOTNET_CLI_HOME = 'J:\system_tools\dotnet_cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:NUGET_PACKAGES = 'J:\system_tools\nuget-packages'
$env:TEMP = Join-Path (Split-Path $PSScriptRoot -Parent) '.build'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null
dotnet build (Join-Path $PSScriptRoot 'MainGameLgsMode.csproj') -c Release --nologo
if ($LASTEXITCODE) { throw 'Plugin build failed' }
if ($Tests) {
    dotnet build (Join-Path $PSScriptRoot 'Tests\Tests.csproj') -c Release --nologo
    if ($LASTEXITCODE) { throw 'Test build failed' }
    & (Join-Path $PSScriptRoot 'Tests\bin\Release\net472\MainGameLgsMode.Tests.exe')
    if ($LASTEXITCODE) { throw 'LGS mode tests failed' }
}
Write-Host 'MainGameLgsMode build finished'
