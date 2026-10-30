#requires -Version 7.0
[CmdletBinding()]
param([string]$OutputDirectory, [switch]$WithInstaller)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Commit the standalone source before packaging.' }
if (& git -C $repoRoot status --porcelain -- src scripts Directory.Build.props) { throw 'Commit source and packaging changes before producing an exact candidate.' }
[xml]$props = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$version = [string]$props.Project.PropertyGroup.Version
$short = $commit.Substring(0,12)
$identity = "$version+$short"
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repoRoot "artifacts/Axiom-Markdown-$version-$short-win-x64" }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Choose a new output directory; retained candidates are not overwritten.' }
$payload = Join-Path $output 'payload'
& dotnet publish (Join-Path $repoRoot 'src/Axiom.App/Axiom.App.csproj') -c Release -r win-x64 --self-contained true "-p:InformationalVersion=$identity" -o $payload --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
& (Join-Path $PSScriptRoot 'Generate-DependencyInventory.ps1') -OutputDirectory (Join-Path $payload 'docs')
foreach ($document in @('USER-GUIDE.md','SECURITY-REVIEW.md','ADMIN-POLICY.md','PROVENANCE.md','THIRD-PARTY.md','REVIEW.md','extraction-manifest.json')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot "docs/$document") -Destination (Join-Path $payload "docs/$document")
}
@"
@echo off
setlocal
set "AXIOM_MARKDOWN_STATE_ROOT=%LOCALAPPDATA%\AxiomMarkdown-Review-$version-$short"
echo Axiom Markdown $identity
echo Review state: %AXIOM_MARKDOWN_STATE_ROOT%
"%~dp0Axiom.Markdown.exe" %*
endlocal
"@ | Set-Content -LiteralPath (Join-Path $payload 'Start-Axiom-Markdown.cmd') -Encoding ascii
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination (Join-Path $payload 'README.md')
@"
Axiom Markdown $identity - unsigned review candidate

Run Start-Axiom-Markdown.cmd for separate review state.
Read docs\USER-GUIDE.md and docs\REVIEW.md.
Security review packet: docs\SECURITY-REVIEW.md
Source: https://github.com/bkins/Axiom-Markdown
Source commit: $commit

This package does not register file associations or retarget full Axiom.
Portable review does not self-update. An optional installer is packaged separately.
Organizational approval and human acceptance remain pending.
"@ | Set-Content -LiteralPath (Join-Path $payload 'START-HERE.txt') -Encoding utf8
$entries = @(Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($payload,$_.FullName).Replace('\','/');length=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
[ordered]@{ product='Axiom Markdown'; informationalVersion=$identity; sourceCommit=$commit; stateName="AxiomMarkdown-Review-$version-$short"; updateRepository='https://github.com/bkins/Axiom-Markdown'; files=$entries } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $payload 'payload-manifest.json') -Encoding utf8
if ($WithInstaller) {
    $vpk = Join-Path $repoRoot '.tools/velopack/vpk.exe'
    if (!(Test-Path -LiteralPath $vpk)) {
        & dotnet tool install vpk --version 1.2.161 --tool-path (Split-Path $vpk -Parent)
        if ($LASTEXITCODE -ne 0) { throw 'Velopack tool installation failed.' }
    }
    $releases = Join-Path $output 'releases'
    & $vpk pack --packId AxiomMarkdown --packTitle 'Axiom Markdown' --packAuthors 'Ben Hopkins' --packVersion $version --mainExe Axiom.Markdown.exe --runtime win-x64 --channel win-x64-stable --releaseNotes (Join-Path $repoRoot 'docs/RELEASE-NOTES.md') --icon (Join-Path $repoRoot 'src/Axiom.App/Assets/Axiom.ico') --delta none --exclude '^$' --noDefaultExclude --shortcuts StartMenuRoot --packDir $payload --outputDir $releases
    if ($LASTEXITCODE -ne 0) { throw 'Installer packaging failed.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archivePath = Join-Path $releases 'AxiomMarkdown-win-x64-stable-Portable.zip'
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        foreach ($entry in $entries) {
            $item = $archive.GetEntry('current/' + $entry.path)
            if (!$item) { throw "Packaged payload entry missing: $($entry.path)" }
            $stream = $item.Open()
            try { $hasher = [Security.Cryptography.SHA256]::Create(); $hash = [Convert]::ToHexString($hasher.ComputeHash($stream)); $hasher.Dispose() } finally { $stream.Dispose() }
            if ($hash -ne $entry.sha256) { throw "Packaged payload entry differs: $($entry.path)" }
        }
    } finally { $archive.Dispose() }
}
Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object { $_.FullName -notlike "$payload\*" } | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash + '  ' + [IO.Path]::GetRelativePath($output,$_.FullName) } | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Prepared $identity at $output. No GitHub release or file association was published."
