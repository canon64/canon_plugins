param([string[]]$Projects, [string]$PythonScript, [switch]$AllFrameWork, [switch]$VoiceRegression, [switch]$TimingProbeRegression, [switch]$MilkRegression, [switch]$MapRegression)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$runtime = Join-Path $root '_diagnostics\perf-reduction-20261027\runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
# Direct CLI, compiler, NuGet and temporary writes to the workspace, never C:.
$env:TEMP = $runtime
$env:TMP = $runtime
$env:DOTNET_CLI_HOME = $runtime
$env:NUGET_PACKAGES = Join-Path $runtime 'nuget'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $runtime 'nuget-http'
$env:NUGET_SCRATCH = Join-Path $runtime 'nuget-scratch'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_NOLOGO = 'true'
Write-Host ('Performance checks/build runtime: ' + $runtime)
Push-Location $root
try {
    if ($AllFrameWork) {
        $Projects = @('SimpleAfterimage', 'MainGameSubCameraDisplayProbe', 'MainGameKaleidoscope',
            'MainGameVrDildo', 'MainGameVMDPlay', 'MainGameVoiceFaceEventBridge', 'MainGameHandySync', 'MainGameUiHelp')
    }
    foreach ($project in $Projects) {
        $path = Join-Path $root "$project\$project.csproj"
        if (-not (Test-Path -LiteralPath $path)) { throw "Project missing: $path" }
        Write-Host ('Building ' + $project)
        & dotnet build $path -c Release --nologo -v quiet
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
    }
    if ($PythonScript) {
        Write-Host ('Running ' + $PythonScript)
        & python $PythonScript
        if ($LASTEXITCODE -ne 0) { throw "Checks failed: $PythonScript" }
    }
    if ($MilkRegression) {
        # Logging appends within a run; never mix previous runs into Single-row assertions.
        $milkRun = Join-Path $runtime ('milk-regression\' + [Guid]::NewGuid().ToString('N'))
        & dotnet run --project MainGameMilkSpray/Tests/PerformanceTests.csproj -c Release -- $milkRun
        if ($LASTEXITCODE -ne 0) { throw 'Milk performance/simulation regression failed' }
    }
    if ($MapRegression) {
        Write-Host 'Running existing map characterization harness'
        & dotnet run --project MainGameBlankMapAdd/Tests/MainGameBlankMapAdd.Characterization.csproj -c Release -- (Join-Path $root 'MainGameBlankMapAdd')
        if ($LASTEXITCODE -ne 0) { throw 'Map characterization regression failed' }
    }
    if ($TimingProbeRegression) {
        & dotnet run --project MainGameFrameTimingProbe/Tests/Tests.csproj -c Release --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Timing probe regression failed' }
    }
    if ($VoiceRegression) {
        Write-Host 'Running existing VoiceFace regression harness'
        & dotnet run --project MainGameVoiceFaceEventBridge/RegressionHarness/MainGameVoiceFaceEventBridge.RegressionHarness.csproj -c Release --nologo
        if ($LASTEXITCODE -ne 0) { throw 'VoiceFace regression harness failed' }
    }
} finally { Pop-Location }
