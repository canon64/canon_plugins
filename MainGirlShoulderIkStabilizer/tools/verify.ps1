param([switch]$TestsOnly, [switch]$BuildOnly)
$ErrorActionPreference = 'Stop'
Write-Host '[shoulder] verification starting'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$env:DOTNET_CLI_HOME = 'J:\system_tools\cache\dotnet-cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
$env:NUGET_PACKAGES = 'J:\system_tools\cache\nuget-packages'
$env:NUGET_HTTP_CACHE_PATH = 'J:\system_tools\cache\nuget-http'
$env:TEMP = Join-Path $repo 'output\shoulder-verification\temp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Force -Path $env:TEMP | Out-Null
if (!$TestsOnly) {
    dotnet build (Join-Path $repo 'MainGirlShoulderIkStabilizer\MainGirlShoulderIkStabilizer.csproj') -c Release --disable-build-servers -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Shoulder/HipHijack build failed' }
}
if (!$BuildOnly) {
    dotnet run --project (Join-Path $repo 'MainGirlShoulderIkStabilizer\Tests\ShoulderPose.Tests.csproj') -c Release --disable-build-servers
    if ($LASTEXITCODE -ne 0) { throw 'Shoulder pose regression failed' }
    dotnet run --project (Join-Path $repo 'MainGirlHipHijack\Characterization\MainGirlHipHijack.Characterization.csproj') -c Release --disable-build-servers -- $repo
    if ($LASTEXITCODE -ne 0) { throw 'HipHijack characterization failed' }
}
Write-Host '[shoulder] verification complete'
