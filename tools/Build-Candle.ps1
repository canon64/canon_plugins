param([switch]$Tests)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$scratch = Join-Path $repo '.build\candle'
Write-Host '[candle] build started'
foreach ($name in @('home','nuget','temp')) { New-Item -ItemType Directory -Path (Join-Path $scratch $name) -Force | Out-Null }
$env:DOTNET_CLI_HOME = Join-Path $scratch 'home'
$env:NUGET_PACKAGES = Join-Path $scratch 'nuget'
$env:TEMP = Join-Path $scratch 'temp'
$env:TMP = $env:TEMP
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:MSBUILDDISABLENODEREUSE = '1'
$projects = @('MainGameMagicPaint','MainGameVoiceFaceEventBridge','MainGameCandle','MainGameBlankMapAdd')
foreach ($name in $projects) {
    Write-Host "[candle] building $name"
    & dotnet build (Join-Path $repo "$name\$name.csproj") -c Release --nologo -v minimal -p:AutomaticallyUseReferenceAssemblyPackages=false
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $name" }
}
if ($Tests) {
    foreach ($project in @('MainGameCandle\Tests\Candle.Tests.csproj','MainGameMagicPaint\Tests\Tests.csproj','_shared\VrItemFocus.Tests\VrItemFocus.Tests.csproj','MainGameVoiceFaceEventBridge\RegressionHarness\MainGameVoiceFaceEventBridge.RegressionHarness.csproj','MainGameBlankMapAdd.EmbeddedUi.Tests\MainGameBlankMapAdd.EmbeddedUi.Tests.csproj')) {
        Write-Host "[candle] testing $project"
        & dotnet run --project (Join-Path $repo $project) -c Release --no-launch-profile
        if ($LASTEXITCODE -ne 0) { throw "Tests failed: $project" }
    }
}
Write-Host '[candle] build complete'
