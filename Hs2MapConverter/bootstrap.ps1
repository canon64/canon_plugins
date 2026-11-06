$ErrorActionPreference = 'Stop'
$destination = 'F:\kks\work\hs2_map_trials'
$template = 'F:\kks\work\hs2_hotel_zipmods\UnityProject'
New-Item -ItemType Directory -Force -Path "$destination\UnityProject\Assets\Editor", "$destination\UnityProject\Assets\ImportData", "$destination\UnityProject\ProjectSettings", "$destination\UnityProject\Packages", "$PSScriptRoot\UnityEditor" | Out-Null
if (!(Test-Path -LiteralPath "$PSScriptRoot\UnityEditor\Hs2MapBuild.cs")) { Copy-Item -LiteralPath "$template\Assets\Editor\Hs2MapBuild.cs" -Destination "$PSScriptRoot\UnityEditor\Hs2MapBuild.cs" }
Copy-Item -Path "$template\Assets\*.cs" -Destination "$destination\UnityProject\Assets"
Copy-Item -LiteralPath "$template\ProjectSettings\TagManager.asset", "$template\ProjectSettings\ProjectVersion.txt" -Destination "$destination\UnityProject\ProjectSettings"
Copy-Item -LiteralPath "$template\Packages\manifest.json" -Destination "$destination\UnityProject\Packages"
Copy-Item -LiteralPath "$template\Assets\ImportData\native_tags.json" -Destination "$destination\UnityProject\Assets\ImportData"
Write-Output "Prepared reusable converter and batch project: $destination"
