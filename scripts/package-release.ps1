<#
.SYNOPSIS
    Packages a self-contained, release build of T4CodeGen.exe into a versioned zip
    for GitHub Releases.

.DESCRIPTION
    Builds T4CodeGen.csproj in Release (after building the CustomBuildTasks library
    in Debug, which the solution build order requires), stages T4CodeGen.exe plus the
    runtime-referenced subset of vendored engine/Roslyn assemblies into a flat
    staging folder, zips it as T4CodeGen-win-x64-<version>.zip, and computes a SHA-256
    checksum file. Intended to run from a Developer PowerShell / VsDevCmd prompt so
    `msbuild` is on PATH.

    Ships only the runtime-referenced DLL subset the exe demonstrably loads at run
    time (derived empirically by a stripped-copy probe) -- not the full compile-time
    reference set. .NET Framework 4.7.2 provides the GAC types that make the
    remaining referenced DLLs unnecessary beside the exe.

.PARAMETER Version
    Required semantic version without the leading 'v', e.g. 1.0.0.

.EXAMPLE
    pwsh scripts\package-release.ps1 -Version 1.0.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$TaskProject = Join-Path $RepoRoot 'CustomBuildTasks\CustomBuildTasks.csproj'
$CliProject = Join-Path $RepoRoot 'T4CodeGen\T4CodeGen.csproj'
$ReleaseDir = Join-Path $RepoRoot 'T4CodeGen\bin\Release'
$ExePath = Join-Path $ReleaseDir 'T4CodeGen.exe'
$StagingRoot = Join-Path $RepoRoot 'staging'
$StagingDir = Join-Path $StagingRoot 'T4CodeGen'
$ZipPath = Join-Path $RepoRoot "T4CodeGen-win-x64-$Version.zip"
$ShaPath = "$ZipPath.sha256"

# The runtime-referenced subset the exe demonstrably loads at run time (stripped-copy
# probe): the vendored engine/Roslyn assemblies plus the System.* runtime deps that are
# NOT resident in the .NET Framework 4.7.2 GAC. Deliberately excludes the three
# compile-time references .NET 4.7.2 resolves from the GAC (System.Buffers,
# System.Text.Encoding.CodePages, System.Threading.Tasks.Extensions).
$RequiredDlls = @(
    'Microsoft.CodeAnalysis.CSharp.dll',
    'Microsoft.CodeAnalysis.dll',
    'Mono.TextTemplating.dll',
    'Mono.TextTemplating.Roslyn.dll',
    'System.Collections.Immutable.dll',
    'System.Memory.dll',
    'System.Numerics.Vectors.dll',
    'System.Reflection.Metadata.dll',
    'System.Runtime.CompilerServices.Unsafe.dll'
)

function Invoke-Msbuild {
    param([string]$Project, [string]$Configuration)
    Write-Host "Building $Project ($Configuration)..."
    & msbuild $Project "/p:Configuration=$Configuration" /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "msbuild failed for $Project ($Configuration)" }
}

# Validate msbuild is callable (Developer PowerShell / VsDevCmd prompt).
& msbuild /version 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "msbuild is not on PATH. Run from a Developer PowerShell / VsDevCmd prompt."
}

Write-Host "Version: $Version"
Write-Host "Repo root: $RepoRoot"

# Clean the ephemeral Release output so the package is a fresh, self-consistent build.
if (Test-Path -LiteralPath $ReleaseDir) {
    Remove-Item -Recurse -Force -LiteralPath $ReleaseDir
}

# Build the library (Debug) first -- RunCodeGen.targets / build order requires the
# already-compiled library DLL before the CLI's Release build.
Invoke-Msbuild -Project $TaskProject -Configuration Debug
Invoke-Msbuild -Project $CliProject -Configuration Release

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "T4CodeGen.exe not found at $ExePath"
}

# Fresh, flat staging directory.
if (Test-Path -LiteralPath $StagingDir) {
    Remove-Item -Recurse -Force -LiteralPath $StagingDir
}
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null

Copy-Item -LiteralPath $ExePath -Destination $StagingDir

foreach ($dll in $RequiredDlls) {
    $src = Join-Path $ReleaseDir $dll
    if (-not (Test-Path -LiteralPath $src)) {
        throw "Expected runtime DLL not found in Release output: $dll"
    }
    Copy-Item -LiteralPath $src -Destination $StagingDir
}

# Future-proof: carry the config file if the build ever emits one (not currently).
$config = Join-Path $ReleaseDir 'T4CodeGen.exe.config'
if (Test-Path -LiteralPath $config) {
    Copy-Item -LiteralPath $config -Destination $StagingDir
}

# Zip the flat T4CodeGen\ folder (one level of nesting, so extraction yields a clean
# T4CodeGen\ directory). No .pdb files -- debug symbols are not needed by consumers.
Compress-Archive -Path $StagingDir -DestinationPath $ZipPath -Force

# SHA-256 checksum file: "<hash>  <zip-name>".
$hash = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash
"$hash  $(Split-Path -Leaf $ZipPath)" | Set-Content -LiteralPath $ShaPath -Encoding ascii

$fileCount = (Get-ChildItem -LiteralPath $StagingDir -File).Count
Write-Host ""
Write-Host "Package created: $ZipPath"
Write-Host "Checksum:        $ShaPath"
Write-Host "SHA-256:         $hash"
Write-Host "Files packed:    $fileCount"

# Clean up the temporary staging directory tree.
Remove-Item -Recurse -Force -LiteralPath $StagingRoot

Write-Host ""
Write-Host "Done. Attach the zip and checksum files as GitHub Release assets."
