$ErrorActionPreference = 'Stop'
Write-Host 'Deploying verified frame-work reductions'
$names = @('SimpleAfterimage', 'MainGameSubCameraDisplayProbe', 'MainGameKaleidoscope',
    'MainGameVrDildo', 'MainGameVMDPlay', 'MainGameVoiceFaceEventBridge', 'MainGameHandySync', 'MainGameUiHelp')
& (Join-Path $PSScriptRoot 'deploy_perf_reductions.ps1') -Names $names -SkipClothConfig -ReportName 'frame-deployment.json'
