param(
    [ValidateSet('test', 'production')]
    [string]$DeployProfile = 'test',

    [ValidateSet('dll', 'source', 'both')]
    [string]$Mode = 'both',

    [string]$SourceDeployRoot = 'F:\kks\work\_tmp\kks-voiceface-bridge-pack_release_work',
    [string]$ProductionRepoUrl = 'https://github.com/canon64/kks-voiceface-bridge-pack.git',
    [string]$GitHubRepo = 'canon64/kks-voiceface-bridge-pack',
    [string]$PluginName = 'kks-voiceface-bridge-pack',

    [string]$Version = '',
    [switch]$AllowExistingTag,

    [string]$OutputDir = 'F:\kks\work\_tmp\voiceface_bridge_pack_dll_zip',
    [string]$ZipName = '',
    [string]$ReleaseAssetName = 'kks_voiceface_bridge_pack.zip',

    [string]$GitCommitMessage = 'ボイスフェイスセットソース更新',
    [switch]$GitCommitOnProduction,
    [switch]$GitPushOnProduction,
    [string]$GitBranch = '',
    [switch]$NoGitIgnore
)

$ErrorActionPreference = 'Stop'

function Test-CommandAvailable {
    param([string]$CommandName)
    return [bool](Get-Command $CommandName -ErrorAction SilentlyContinue)
}

function Test-SemVerCore {
    param([string]$SemVerText)
    return ($SemVerText -match '^\d+\.\d+\.\d+$')
}

function Get-NextPatchVersion {
    param([string]$CurrentVersion)

    if (-not (Test-SemVerCore -SemVerText $CurrentVersion)) {
        throw "Invalid version format: $CurrentVersion (expected: MAJOR.MINOR.PATCH)"
    }

    $parts = $CurrentVersion.Split('.')
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2] + 1
    return ('{0}.{1}.{2}' -f $major, $minor, $patch)
}

function Test-GitHubReleaseExists {
    param(
        [string]$Repo,
        [string]$Tag
    )

    try {
        $null = & gh release view $Tag --repo $Repo 2>$null
        return ($LASTEXITCODE -eq 0)
    }
    catch {
        return $false
    }
}

function Ensure-ProductionRepoReady {
    param(
        [string]$SourceDeployRootPath,
        [string]$RepoUrl,
        [string]$ExpectedRemoteToken
    )

    if (-not (Test-CommandAvailable -CommandName 'git')) {
        throw 'git command not found.'
    }

    if (-not (Test-Path -LiteralPath $SourceDeployRootPath)) {
        $parentDir = Split-Path -Parent $SourceDeployRootPath
        if ($parentDir -and -not (Test-Path -LiteralPath $parentDir)) {
            New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
        }

        Write-Output ('Production repo not found. Cloning: {0} -> {1}' -f $RepoUrl, $SourceDeployRootPath)
        & git clone $RepoUrl $SourceDeployRootPath
        if ($LASTEXITCODE -ne 0) {
            throw "git clone failed: $RepoUrl -> $SourceDeployRootPath"
        }
    }

    if (-not (Test-Path -LiteralPath (Join-Path $SourceDeployRootPath '.git'))) {
        throw "SourceDeployRoot is not a git repository: $SourceDeployRootPath"
    }

    $originUrl = (& git -C $SourceDeployRootPath remote get-url origin 2>$null)
    if (-not $originUrl) {
        throw "origin remote not found in $SourceDeployRootPath"
    }
    if ($originUrl -notmatch [Regex]::Escape($ExpectedRemoteToken)) {
        throw "origin remote mismatch. origin=$originUrl expected~$ExpectedRemoteToken"
    }
}

function Resolve-ReleaseVersion {
    param(
        [string]$ExplicitVersion,
        [string]$GitHubRepoName,
        [string]$TargetPluginName,
        [bool]$AllowExistingTagReuse
    )

    if (-not (Test-CommandAvailable -CommandName 'gh')) {
        throw 'gh command not found.'
    }

    if (-not [string]::IsNullOrWhiteSpace($ExplicitVersion)) {
        if (-not (Test-SemVerCore -SemVerText $ExplicitVersion)) {
            throw "Invalid -Version value: $ExplicitVersion (expected: MAJOR.MINOR.PATCH)"
        }

        $explicitTag = ('{0}-v{1}' -f $TargetPluginName, $ExplicitVersion)
        if (Test-GitHubReleaseExists -Repo $GitHubRepoName -Tag $explicitTag) {
            if (-not $AllowExistingTagReuse) {
                throw "Release tag already exists: $explicitTag"
            }
            Write-Output ("Reuse existing release tag: {0}" -f $explicitTag)
        }

        return $ExplicitVersion
    }

    $candidateVersion = '1.0.0'
    $guard = 0
    while ($true) {
        $candidateTag = ('{0}-v{1}' -f $TargetPluginName, $candidateVersion)
        if (-not (Test-GitHubReleaseExists -Repo $GitHubRepoName -Tag $candidateTag)) {
            break
        }

        $candidateVersion = Get-NextPatchVersion -CurrentVersion $candidateVersion
        $guard++
        if ($guard -gt 200) {
            throw 'Failed to resolve next version (too many existing tags).'
        }
    }

    return $candidateVersion
}

if ($DeployProfile -eq 'test' -and ($GitCommitOnProduction -or $GitPushOnProduction)) {
    throw 'GitCommitOnProduction / GitPushOnProduction cannot be used with test profile.'
}

$baseScriptPath = Join-Path $PSScriptRoot 'New-VoicefaceBridgePackDllZip.ps1'
if (-not (Test-Path -LiteralPath $baseScriptPath)) {
    throw "Base script not found: $baseScriptPath"
}

$sourceDeployMode = if ($DeployProfile -eq 'production') { 'production' } else { 'test' }
$requiresSourceDeploy = ($Mode -eq 'source' -or $Mode -eq 'both')
$requiresDll = ($Mode -eq 'dll' -or $Mode -eq 'both')
$shouldCreateRelease = ($DeployProfile -eq 'production' -and $GitPushOnProduction -and $requiresDll)
$expectedRemoteToken = ('github.com/{0}' -f $GitHubRepo)

if ($DeployProfile -eq 'production' -and ($requiresSourceDeploy -or $shouldCreateRelease)) {
    Ensure-ProductionRepoReady `
        -SourceDeployRootPath $SourceDeployRoot `
        -RepoUrl $ProductionRepoUrl `
        -ExpectedRemoteToken $expectedRemoteToken
}

if ($shouldCreateRelease -and [string]::IsNullOrWhiteSpace($ZipName)) {
    $ZipName = $ReleaseAssetName
}

$invokeParams = @{
    Mode = $Mode
    SourceDeployMode = $sourceDeployMode
    SourceDeployRoot = $SourceDeployRoot
    OutputDir = $OutputDir
    GitCommitMessage = $GitCommitMessage
    ExpectedProductionRemote = $expectedRemoteToken
}

if (-not [string]::IsNullOrWhiteSpace($GitBranch)) {
    $invokeParams['GitBranch'] = $GitBranch
}

if (-not [string]::IsNullOrWhiteSpace($ZipName)) {
    $invokeParams['ZipName'] = $ZipName
}

if ($NoGitIgnore) {
    $invokeParams['NoGitIgnore'] = $true
}

if ($DeployProfile -eq 'production' -and $GitCommitOnProduction) {
    $invokeParams['GitCommitOnProduction'] = $true
}

if ($DeployProfile -eq 'production' -and $GitPushOnProduction) {
    $invokeParams['GitPushOnProduction'] = $true
}

Write-Output ('Deploy profile: voiceface_bridge_pack {0}' -f $DeployProfile)
Write-Output ('Mode: {0}' -f $Mode)
Write-Output ('SourceDeployMode: {0}' -f $sourceDeployMode)
Write-Output ('SourceDeployRoot: {0}' -f $SourceDeployRoot)
Write-Output ('ProductionRepoUrl: {0}' -f $ProductionRepoUrl)
Write-Output ('GitHubRepo: {0}' -f $GitHubRepo)
Write-Output ('CommitOnProduction: {0}' -f ($(if ($GitCommitOnProduction) { 'enabled' } else { 'disabled' })))
Write-Output ('PushOnProduction: {0}' -f ($(if ($GitPushOnProduction) { 'enabled' } else { 'disabled' })))
Write-Output ('CreateRelease: {0}' -f ($(if ($shouldCreateRelease) { 'enabled' } else { 'disabled' })))

& $baseScriptPath @invokeParams

if (-not $shouldCreateRelease) {
    if ($DeployProfile -eq 'production' -and -not $GitPushOnProduction) {
        Write-Output 'Release step skipped: GitPushOnProduction is disabled.'
    }
    return
}

if (-not (Test-CommandAvailable -CommandName 'gh')) {
    throw 'gh command not found.'
}

$zipPath = Join-Path $OutputDir $ZipName
if (-not (Test-Path -LiteralPath $zipPath)) {
    throw "Release zip not found: $zipPath"
}

$resolvedVersion = Resolve-ReleaseVersion `
    -ExplicitVersion $Version `
    -GitHubRepoName $GitHubRepo `
    -TargetPluginName $PluginName `
    -AllowExistingTagReuse:$AllowExistingTag

$releaseTag = ('{0}-v{1}' -f $PluginName, $resolvedVersion)
$assetArg = ('{0}#{1}' -f $zipPath, $ReleaseAssetName)
$sourceCommit = (& git -C $SourceDeployRoot rev-parse HEAD).Trim()

if (Test-GitHubReleaseExists -Repo $GitHubRepo -Tag $releaseTag) {
    & gh release upload $releaseTag $assetArg --repo $GitHubRepo --clobber
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to upload release asset: $releaseTag"
    }
}
else {
    $releaseNotes = @"
Automated deployment.

- Plugin: $PluginName
- Version: $resolvedVersion
- Source commit: $sourceCommit
"@
    & gh release create $releaseTag $assetArg --repo $GitHubRepo --title $releaseTag --notes $releaseNotes
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create release: $releaseTag"
    }
}

$releaseUrl = ('https://github.com/{0}/releases/tag/{1}' -f $GitHubRepo, $releaseTag)
Write-Output ('Release created/updated: {0}' -f $releaseTag)
Write-Output ('Release URL: {0}' -f $releaseUrl)

