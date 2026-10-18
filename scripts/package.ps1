<#
.SYNOPSIS
  Builds CBMod and packs a ready-to-install release zip: official BepInEx 6 IL2CPP files + CBMod.dll.

.DESCRIPTION
  1. Builds CBMod (Release) unless -SkipBuild is given.
  2. Extracts the official BepInEx 6 IL2CPP (Windows x64) archive you downloaded into a temporary folder.
  3. Adds BepInEx\plugins\CBMod.dll plus LICENSE, README and the third-party notices.
  4. Refuses to continue if any game file or generated interop file ended up in the package.
  5. Writes ChainedBeastsMod-v<version>.zip and a matching .sha256 file.

  The version is read from <Version> in CBMod\CBMod.csproj (the single place it is set).
  The BepInEx archive is NOT downloaded by this script and is never modified: get it from the official builds page
  https://builds.bepinex.dev/projects/bepinex_be (file name looks like BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.789+<commit>.zip).

.PARAMETER BepInExZip
  Path to the official BepInEx 6 IL2CPP win-x64 zip.

.PARAMETER OutDir
  Where the finished zip and .sha256 are written. Default: .\dist next to the repository root.

.PARAMETER GameDir
  Passed to the build as -p:GameDir=... (only needed if the game is not in the default Steam location).

.PARAMETER SkipBuild
  Use the CBMod.dll that is already in CBMod\bin\Release\net6.0 instead of building again.

.EXAMPLE
  .\scripts\package.ps1 -BepInExZip "$HOME\Downloads\BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.789+6cea286.zip"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $BepInExZip,

    [string] $OutDir,

    [string] $GameDir,

    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$csproj   = Join-Path $repoRoot 'CBMod\CBMod.csproj'
if (-not $OutDir) { $OutDir = Join-Path $repoRoot 'dist' }

# ---- version (single source of truth: the csproj) ----
[xml] $proj = Get-Content -LiteralPath $csproj -Raw
$version = ($proj.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw "Could not read <Version> from $csproj" }
Write-Host "CBMod version: $version"

# ---- input checks ----
if (-not (Test-Path -LiteralPath $BepInExZip -PathType Leaf)) { throw "BepInEx archive not found: $BepInExZip" }
$bepName = Split-Path -Leaf $BepInExZip
if ($bepName -notmatch 'IL2CPP' -or $bepName -notmatch 'win-x64') {
    Write-Warning "The file name '$bepName' does not look like a BepInEx IL2CPP win-x64 build. Continuing anyway."
}
$bepHash = (Get-FileHash -LiteralPath $BepInExZip -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "BepInEx archive: $bepName"
Write-Host "BepInEx SHA256 : $bepHash   (compare with the official download if you want to be sure)"

# ---- build ----
$dllPath = Join-Path $repoRoot 'CBMod\bin\Release\net6.0\CBMod.dll'
if (-not $SkipBuild) {
    $buildArgs = @('build', $csproj, '-c', 'Release', '--nologo')
    if ($GameDir) { $buildArgs += "-p:GameDir=$GameDir" }
    Write-Host "dotnet $($buildArgs -join ' ')"
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
if (-not (Test-Path -LiteralPath $dllPath)) { throw "CBMod.dll not found at $dllPath (build it first or drop -SkipBuild)." }

# ---- stage in a brand-new temporary folder ----
$stage = Join-Path ([System.IO.Path]::GetTempPath()) ('cbmod-package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    # The archive is only ever read. Entries are checked so nothing can be written outside $stage.
    $stageFull = [System.IO.Path]::GetFullPath($stage).TrimEnd('\') + '\'
    $archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $BepInExZip).Path)
    try {
        foreach ($entry in $archive.Entries) {
            if ([string]::IsNullOrEmpty($entry.Name)) { continue }   # directory entry
            $target = [System.IO.Path]::GetFullPath((Join-Path $stage $entry.FullName))
            if (-not $target.StartsWith($stageFull, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Unsafe path in BepInEx archive: $($entry.FullName)"
            }
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
    }
    finally { $archive.Dispose() }

    # The archive must be the game-root layout (winhttp.dll next to the game exe, BepInEx\core inside).
    if (-not (Test-Path -LiteralPath (Join-Path $stage 'BepInEx\core'))) {
        throw 'The BepInEx archive has no BepInEx\core folder. Is this the right file (IL2CPP, win-x64)?'
    }

    # Add our files.
    $plugins = Join-Path $stage 'BepInEx\plugins'
    New-Item -ItemType Directory -Force -Path $plugins | Out-Null
    Copy-Item -LiteralPath $dllPath -Destination (Join-Path $plugins 'CBMod.dll')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $stage 'CBMod-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $stage 'THIRD_PARTY_NOTICES.md')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination (Join-Path $stage 'CBMod-README.md')

    # Safety net: no game files and no generated interop assemblies may be in the package.
    $forbidden = Get-ChildItem -LiteralPath $stage -Recurse -Force |
        Where-Object {
            $_.FullName -match '\\interop\\' -or
            $_.FullName -match '_Data(\\|$)' -or
            $_.Name -like 'Assembly-CSharp*' -or
            $_.Name -eq 'GameAssembly.dll' -or
            $_.Name -eq 'UnityPlayer.dll' -or
            $_.Name -eq 'global-metadata.dat' -or
            $_.Name -like 'ChainedBeasts*.exe'
        }
    if ($forbidden) {
        $forbidden | ForEach-Object { Write-Error "Forbidden file in package: $($_.FullName)" }
        throw 'Package contains game or interop files. Aborting.'
    }

    # ---- zip with forward-slash entry names (portable) ----
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
    $zipName = "ChainedBeastsMod-v$version.zip"
    $zipPath = Join-Path (Resolve-Path -LiteralPath $OutDir).Path $zipName
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

    $zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -LiteralPath $stage -Recurse -File -Force | ForEach-Object {
            $rel = $_.FullName.Substring($stageFull.Length).Replace('\', '/')
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $rel, [System.IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally { $zip.Dispose() }

    # ---- checksum ----
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $shaPath = "$zipPath.sha256"
    # Same format as `sha256sum`: "<hash> *<file name>"
    [System.IO.File]::WriteAllText($shaPath, "$hash *$zipName`n", (New-Object System.Text.UTF8Encoding($false)))

    $dllHash = (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Host ''
    Write-Host "Package   : $zipPath"
    Write-Host "SHA256    : $hash"
    Write-Host "Checksum  : $shaPath"
    Write-Host "CBMod.dll : $dllHash"
}
finally {
    Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
}
