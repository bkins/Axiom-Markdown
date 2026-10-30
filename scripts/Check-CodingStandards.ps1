#requires -Version 7.0
[CmdletBinding()]
param([string]$ReportPath)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$arguments = @('run', '--project', (Join-Path $repositoryRoot 'tools/StandardsAudit'), '--', $repositoryRoot)
if ($ReportPath) { $arguments += [IO.Path]::GetFullPath($ReportPath) }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Coding standards source checks failed. Review the findings; this check does not replace a Clean Code/SOLID review.' }
