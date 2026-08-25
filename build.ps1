# Build script for MotionSicknessHelper.
#
# Usage:
#   .\build.ps1                 # Small framework-dependent single-file EXE (needs .NET 8 Desktop Runtime)
#   .\build.ps1 -SelfContained  # Also builds a no-runtime-needed EXE (larger, ~70 MB)
#
# Output goes to .\publish\

param(
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'
$Project = Join-Path $PSScriptRoot 'MotionSicknessHelper.csproj'
$PublishDir = Join-Path $PSScriptRoot 'publish'

Write-Host 'Publishing framework-dependent single-file EXE...'
dotnet publish $Project -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o (Join-Path $PublishDir 'MotionSicknessHelper')
Copy-Item (Join-Path $PSScriptRoot 'config.json') (Join-Path $PublishDir 'MotionSicknessHelper\config.json') -Force

if ($SelfContained) {
    Write-Host 'Publishing self-contained single-file EXE (no .NET runtime needed)...'
    dotnet publish $Project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o (Join-Path $PublishDir 'MotionSicknessHelper-SelfContained')
    Copy-Item (Join-Path $PSScriptRoot 'config.json') (Join-Path $PublishDir 'MotionSicknessHelper-SelfContained\config.json') -Force
}

Write-Host 'Done. Output is in:'
Write-Host $PublishDir
