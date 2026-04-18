#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Pack kentico-sentinel from source, uninstall the global tool, and reinstall from the fresh package.
    Use this during iterative development to see local code changes reflected in the `sentinel` command.

.EXAMPLE
    ./scripts/dev-reinstall.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$csproj = Join-Path $repoRoot "src\KenticoSentinel\KenticoSentinel.csproj"
$packageOutputDir = Join-Path $repoRoot "src\KenticoSentinel\bin\Release"

Write-Host "==> Packing $csproj (Release)" -ForegroundColor Cyan
& dotnet pack $csproj -c Release --nologo | Out-Host
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed." }

Write-Host "==> Uninstalling global tool (if present)" -ForegroundColor Cyan
& dotnet tool uninstall -g RefinedElement.Kentico.Sentinel 2>$null | Out-Null

Write-Host "==> Installing global tool from $packageOutputDir" -ForegroundColor Cyan
& dotnet tool install -g --add-source $packageOutputDir RefinedElement.Kentico.Sentinel | Out-Host
if ($LASTEXITCODE -ne 0) { throw "dotnet tool install failed." }

Write-Host "==> Installed. Run 'sentinel --version' to verify." -ForegroundColor Green
