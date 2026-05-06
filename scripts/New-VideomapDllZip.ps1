param(
    [ValidateSet('dll', 'source', 'both')]
    [string]$Mode = 'both',

    [ValidateSet('test', 'production', 'apply')]
    [string]$SourceDeployMode = 'test',

    [string]$SourceCanonPluginsRoot = 'F:\kks\BepInEx\plugins\canon_plugins',
    [string]$SourceProjectRoot = 'F:\kks\work\plugins\canon_plugins',
    [string]$RepoRoot = 'F:\kks\work\plugins\canon_plugins',
    [string]$SourceRootReadmePath = 'F:\kks\work\plugins\canon_plugins\README_videomap_source.md',

    [string]$OutputDir = 'F:\kks\work\_tmp\videomap_dll_zip',
    [string]$ZipName = '',
    [string]$SourceDeployRoot = 'F:\kks\work\_tmp\MainGameBlankMapAdd_release_work',
    [string]$SourceDeployTestRootBase = 'F:\kks\work\_tmp\videomap_dll_zip\_source_deploy_test',
    [switch]$GitCommitOnProduction,
    [switch]$GitPushOnProduction,
    [string]$GitCommitMessage = '動画マップソース更新',
    [string]$GitBranch = '',
    [string]$ExpectedProductionRemote = 'github.com/canon64/MainGameBlankMapAdd',

    [string[]]$BinaryIncludePatterns = @('*.dll', 'README.md', 'README_ja.md'),
    [string]$BinaryToolsDirectoryName = '_tools',
    [bool]$IncludeTools = $true,
    [string[]]$SourceIncludePatterns = @('*.cs', '*.csproj', 'README.md', 'README_ja.md'),
    [switch]$NoGitIgnore
)

$ErrorActionPreference = 'Stop'

$pluginNames = @(
    'MainGameBlankMapAdd',
    'MainGameTransformGizmo',
    'MainGameLogRelay',
    'MainGameSpeedLimitBreak',
    'MainGameBeatSyncSpeed',
    'MainGameAllPoseMap'
)

function Test-IncludeCandidate {
    param(
        [string]$FileName,
        [string]$RelativePath,
        [string[]]$Patterns
    )

    $relativeUnix = $RelativePath.Replace('\', '/')
    foreach ($pattern in $Patterns) {
        if ($FileName -like $pattern) {
            return $true
        }

        if ($relativeUnix -like $pattern) {
            return $true
        }
    }

    return $false
}

function Test-IgnoredByGit {
    param(
        [string]$VirtualRelativePath,
        [string]$RootDir
    )

    if (-not (Test-Path (Join-Path $RootDir '.git'))) {
        return $false
    }

    $null = & git -C $RootDir check-ignore --no-index -q -- $VirtualRelativePath 2>$null
    return ($LASTEXITCODE -eq 0)
}

function Assert-PathWithinRoot {
    param(
        [string]$PathValue,
        [string]$ExpectedRoot,
        [string]$Label
    )

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        throw "$Label is empty."
    }

    $fullPath = [System.IO.Path]::GetFullPath($PathValue).TrimEnd('\', '/')
    $fullRoot = [System.IO.Path]::GetFullPath($ExpectedRoot).TrimEnd('\', '/')
    if ($fullPath.Equals($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return
    }

    $fullRootWithSep = $fullRoot + '\'
    if (-not $fullPath.StartsWith($fullRootWithSep, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw ("{0} must be under {1} actual={2}" -f $Label, $fullRootWithSep, $fullPath)
    }
}

function Invoke-GitCommitUtf8Message {
    param(
        [string]$RepoPath,
        [string]$CommitMessage
    )

    if ([string]::IsNullOrWhiteSpace($CommitMessage)) {
        throw 'Commit message is empty.'
    }

    $commitMessagePath = Join-Path $RepoPath '.__tmp_commit_message_utf8.txt'
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)

    try {
        [System.IO.File]::WriteAllText($commitMessagePath, ($CommitMessage + [Environment]::NewLine), $utf8NoBom)
        $null = & git -C $RepoPath -c i18n.commitEncoding=utf-8 -c i18n.logOutputEncoding=utf-8 commit -F $commitMessagePath
        return $LASTEXITCODE
    }
    finally {
        if (Test-Path -LiteralPath $commitMessagePath) {
            Remove-Item -LiteralPath $commitMessagePath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Resolve-PushBranch {
    param(
        [string]$RepoPath,
        [string]$RequestedBranch
    )

    $branch = $RequestedBranch
    if ([string]::IsNullOrWhiteSpace($branch)) {
        $branch = (& git -C $RepoPath rev-parse --abbrev-ref HEAD 2>$null)
        if ($branch) {
            $branch = $branch.Trim()
        }
    }

    if ([string]::IsNullOrWhiteSpace($branch) -or $branch -eq 'HEAD') {
        throw "Could not resolve push branch for $RepoPath"
    }

    return $branch
}

$canonicalSourceRoot = 'F:\kks\work\plugins\canon_plugins'
Assert-PathWithinRoot -PathValue $SourceProjectRoot -ExpectedRoot $canonicalSourceRoot -Label 'SourceProjectRoot'
Assert-PathWithinRoot -PathValue $RepoRoot -ExpectedRoot $canonicalSourceRoot -Label 'RepoRoot'
Assert-PathWithinRoot -PathValue $SourceRootReadmePath -ExpectedRoot $canonicalSourceRoot -Label 'SourceRootReadmePath'

function Copy-FilteredPluginFiles {
    param(
        [string]$InputRoot,
        [string]$DestRoot,
        [string]$VirtualRootForIgnore,
        [string[]]$Patterns,
        [string]$CategoryLabel
    )

    $included = New-Object System.Collections.Generic.List[string]
    $skippedByIgnore = 0
    $skippedByPattern = 0

    foreach ($pluginName in $pluginNames) {
        $sourceDir = Join-Path $InputRoot $pluginName
        if (-not (Test-Path $sourceDir)) {
            throw "$CategoryLabel source directory not found: $sourceDir"
        }

        $allFiles = Get-ChildItem -Path $sourceDir -Recurse -File
        foreach ($file in $allFiles) {
            $relativeInPlugin = $file.FullName.Substring($sourceDir.Length + 1)
            $virtualRelative = ('{0}/{1}' -f $pluginName, $relativeInPlugin.Replace('\', '/'))

            if (-not (Test-IncludeCandidate -FileName $file.Name -RelativePath $relativeInPlugin -Patterns $Patterns)) {
                $skippedByPattern++
                continue
            }

            if (-not $NoGitIgnore -and (Test-IgnoredByGit -VirtualRelativePath $virtualRelative -RootDir $VirtualRootForIgnore)) {
                $skippedByIgnore++
                continue
            }

            $destPath = Join-Path (Join-Path $DestRoot $pluginName) $relativeInPlugin
            $destDir = Split-Path -Parent $destPath
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $destPath -Force
            $included.Add(('source/canon_plugins/{0}/{1}' -f $pluginName, $relativeInPlugin.Replace('\', '/'))) | Out-Null
        }
    }

    return [PSCustomObject]@{
        Included = $included
        IncludedCount = $included.Count
        SkippedByIgnore = $skippedByIgnore
        SkippedByPattern = $skippedByPattern
    }
}

if ([string]::IsNullOrWhiteSpace($ZipName)) {
    $ZipName = ('videomap_dll_{0}.zip' -f (Get-Date -Format 'yyyyMMdd_HHmmss'))
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$runStamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$stagingRoot = Join-Path $OutputDir ('_staging_videomap_{0}' -f $runStamp)
if (Test-Path $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

$binaryIncluded = New-Object System.Collections.Generic.List[string]
$binarySkippedByIgnore = 0
$sourceResult = $null
$zipPath = $null
$effectiveSourceDeployRoot = $null
$isProductionSourceDeploy = ($SourceDeployMode -eq 'production' -or $SourceDeployMode -eq 'apply')

if ($Mode -eq 'dll' -or $Mode -eq 'both') {
    $zipRoot = Join-Path $stagingRoot 'BepInEx\plugins\canon_plugins'
    New-Item -ItemType Directory -Path $zipRoot -Force | Out-Null

    foreach ($pluginName in $pluginNames) {
        $sourceDir = Join-Path $SourceCanonPluginsRoot $pluginName
        if (-not (Test-Path $sourceDir)) {
            throw "Binary source directory not found: $sourceDir"
        }

        $allFiles = Get-ChildItem -Path $sourceDir -Recurse -File
        foreach ($file in $allFiles) {
            $relativeInPlugin = $file.FullName.Substring($sourceDir.Length + 1)
            $virtualRelative = ('{0}/{1}' -f $pluginName, $relativeInPlugin.Replace('\', '/'))

            if (-not (Test-IncludeCandidate -FileName $file.Name -RelativePath $relativeInPlugin -Patterns $BinaryIncludePatterns)) {
                continue
            }

            if (-not $NoGitIgnore -and (Test-IgnoredByGit -VirtualRelativePath $virtualRelative -RootDir $RepoRoot)) {
                $binarySkippedByIgnore++
                continue
            }

            $destPath = Join-Path (Join-Path $zipRoot $pluginName) $relativeInPlugin
            $destDir = Split-Path -Parent $destPath
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $destPath -Force
            $binaryIncluded.Add(('BepInEx/plugins/canon_plugins/{0}/{1}' -f $pluginName, $relativeInPlugin.Replace('\', '/'))) | Out-Null
        }
    }

    if ($IncludeTools -and -not [string]::IsNullOrWhiteSpace($BinaryToolsDirectoryName)) {
        $toolsSourceDir = Join-Path $SourceCanonPluginsRoot $BinaryToolsDirectoryName
        if (Test-Path $toolsSourceDir) {
            $toolFiles = Get-ChildItem -Path $toolsSourceDir -Recurse -File
            foreach ($toolFile in $toolFiles) {
                $relativeInTools = $toolFile.FullName.Substring($toolsSourceDir.Length + 1)
                $virtualRelative = ('{0}/{1}' -f $BinaryToolsDirectoryName, $relativeInTools.Replace('\', '/'))
                if (-not $NoGitIgnore -and (Test-IgnoredByGit -VirtualRelativePath $virtualRelative -RootDir $RepoRoot)) {
                    $binarySkippedByIgnore++
                    continue
                }

                $toolDestPath = Join-Path (Join-Path $zipRoot $BinaryToolsDirectoryName) $relativeInTools
                $toolDestDir = Split-Path -Parent $toolDestPath
                New-Item -ItemType Directory -Path $toolDestDir -Force | Out-Null
                Copy-Item -LiteralPath $toolFile.FullName -Destination $toolDestPath -Force
                $binaryIncluded.Add(('BepInEx/plugins/canon_plugins/{0}/{1}' -f $BinaryToolsDirectoryName, $relativeInTools.Replace('\', '/'))) | Out-Null
            }
        }
    }

    if ($binaryIncluded.Count -le 0) {
        throw 'No binary files included. Check BinaryIncludePatterns and .gitignore rules.'
    }

    $zipPath = Join-Path $OutputDir $ZipName
    if (Test-Path $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $stagingRoot 'BepInEx') -DestinationPath $zipPath -CompressionLevel Optimal
}

if ($Mode -eq 'source' -or $Mode -eq 'both') {
    if (-not $isProductionSourceDeploy) {
        $effectiveSourceDeployRoot = Join-Path $SourceDeployTestRootBase ('run_{0}' -f $runStamp)
    }
    else {
        $effectiveSourceDeployRoot = $SourceDeployRoot

        if (-not (Test-Path $effectiveSourceDeployRoot)) {
            throw "Production source deploy root not found: $effectiveSourceDeployRoot"
        }

        if (-not (Test-Path (Join-Path $effectiveSourceDeployRoot '.git'))) {
            throw "Production source deploy root is not a git repository: $effectiveSourceDeployRoot"
        }
    }

    New-Item -ItemType Directory -Path $effectiveSourceDeployRoot -Force | Out-Null

    foreach ($pluginName in $pluginNames) {
        $pluginDest = Join-Path $effectiveSourceDeployRoot $pluginName
        if (Test-Path $pluginDest) {
            Remove-Item -LiteralPath $pluginDest -Recurse -Force
        }
    }

    $sourceResult = Copy-FilteredPluginFiles `
        -InputRoot $SourceProjectRoot `
        -DestRoot $effectiveSourceDeployRoot `
        -VirtualRootForIgnore $RepoRoot `
        -Patterns $SourceIncludePatterns `
        -CategoryLabel 'source'

    if (-not (Test-Path $SourceRootReadmePath)) {
        throw "Source root README template not found: $SourceRootReadmePath"
    }

    $sourceRootReadmeDest = Join-Path $effectiveSourceDeployRoot 'README.md'
    Copy-Item -LiteralPath $SourceRootReadmePath -Destination $sourceRootReadmeDest -Force
    $sourceResult.Included.Add('README.md') | Out-Null
    $sourceResult.IncludedCount = $sourceResult.Included.Count

    if ($sourceResult.IncludedCount -le 0) {
        throw 'No source files included. Check SourceIncludePatterns and .gitignore rules.'
    }

    if ($isProductionSourceDeploy -and ($GitCommitOnProduction -or $GitPushOnProduction)) {
        & git -C $effectiveSourceDeployRoot config user.name canon64
        & git -C $effectiveSourceDeployRoot config user.email canon64@users.noreply.github.com

        & git -C $effectiveSourceDeployRoot add .
        $statusAfterAdd = & git -C $effectiveSourceDeployRoot status --porcelain
        if ($statusAfterAdd) {
            $commitExitCode = Invoke-GitCommitUtf8Message -RepoPath $effectiveSourceDeployRoot -CommitMessage $GitCommitMessage
            if ($commitExitCode -ne 0) {
                throw 'Failed to commit source deploy changes.'
            }
        }
        else {
            Write-Output 'Production source deploy: no changes to commit.'
        }

        if ($GitPushOnProduction) {
            $remoteUrl = (& git -C $effectiveSourceDeployRoot remote get-url origin 2>$null)
            if (-not $remoteUrl) {
                throw "Production source deploy: origin remote not found in $effectiveSourceDeployRoot"
            }

            if ($remoteUrl -notmatch [Regex]::Escape($ExpectedProductionRemote)) {
                throw "Production source deploy: origin remote mismatch. origin=$remoteUrl expected~$ExpectedProductionRemote"
            }

            $pushBranch = Resolve-PushBranch -RepoPath $effectiveSourceDeployRoot -RequestedBranch $GitBranch
            & git -C $effectiveSourceDeployRoot push origin $pushBranch
            if ($LASTEXITCODE -ne 0) {
                throw "Production source deploy: failed to push branch $pushBranch"
            }
        }
    }
}

Write-Output ('Mode: {0}' -f $Mode)
if ($zipPath) {
    Write-Output ('DLL zip created: {0}' -f $zipPath)
    Write-Output ('Included binary files: {0}' -f $binaryIncluded.Count)
    foreach ($path in $binaryIncluded | Sort-Object) {
        Write-Output ('- {0}' -f $path)
    }
    if (-not $NoGitIgnore) {
        Write-Output ('DLL skipped by .gitignore: {0}' -f $binarySkippedByIgnore)
    }
}

if ($sourceResult) {
    Write-Output ('Source deploy mode: {0}' -f $SourceDeployMode)
    Write-Output ('Source deploy profile: {0}' -f ($(if ($isProductionSourceDeploy) { 'production' } else { 'test' })))
    Write-Output ('Source deploy root: {0}' -f $effectiveSourceDeployRoot)
    Write-Output ('Included source files: {0}' -f $sourceResult.IncludedCount)
    foreach ($path in $sourceResult.Included | Sort-Object) {
        Write-Output ('- {0}' -f $path)
    }
    if (-not $NoGitIgnore) {
        Write-Output ('Source skipped by .gitignore: {0}' -f $sourceResult.SkippedByIgnore)
    }

    if (Test-Path (Join-Path $effectiveSourceDeployRoot '.git')) {
        Write-Output 'Git status (source deploy root):'
        & git -C $effectiveSourceDeployRoot status --short
    }
}
