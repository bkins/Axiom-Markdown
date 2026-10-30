#requires -Version 7.0
[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'docs'))
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$packages = @{}
foreach ($project in @('Axiom.App','Axiom.Core','Axiom.Infrastructure')) {
    $assetsPath = Join-Path $repoRoot "src/$project/obj/project.assets.json"
    if (!(Test-Path -LiteralPath $assetsPath)) { throw 'Restore all production projects first.' }
    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
    foreach ($entry in $assets.libraries.PSObject.Properties) {
        if ($entry.Value.type -ne 'package') { continue }
        $parts = $entry.Name.Split('/')
        $license = 'NOASSERTION'
        foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
            $packageFolder = Join-Path $folder $entry.Value.path
            if (!(Test-Path -LiteralPath $packageFolder)) { continue }
            $nuspec = Get-ChildItem -LiteralPath $packageFolder -Filter '*.nuspec' -File | Select-Object -First 1
            if ($nuspec) {
                [xml]$xml = Get-Content -LiteralPath $nuspec.FullName -Raw
                $licenseNode = $xml.SelectSingleNode("//*[local-name()='license']")
                if ($licenseNode -and $licenseNode.type -eq 'expression') { $license = $licenseNode.InnerText }
            }
        }
        $packages[$entry.Name] = [ordered]@{ id=$parts[0]; version=$parts[1]; license=$license; sha512=$entry.Value.sha512; source='NuGet'; url="https://www.nuget.org/packages/$($parts[0])/$($parts[1])" }
    }
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$generated = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
$orderedPackages = @($packages.Values | Sort-Object id,version)
[ordered]@{ generatedUtc=$generated; scope='Restored production-project NuGet dependencies; external runtimes and bundled JS are documented separately.'; packages=$orderedPackages } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'dependency-inventory.json') -Encoding utf8
$spdxPackages = @()
$relations = @()
$sequence = 0
foreach ($package in $orderedPackages) {
    $sequence++
    $id = "SPDXRef-Package-$sequence"
    $spdxPackages += [ordered]@{ SPDXID=$id; name=$package.id; versionInfo=$package.version; downloadLocation=$package.url; filesAnalyzed=$false; licenseConcluded='NOASSERTION'; licenseDeclared=$package.license; copyrightText='NOASSERTION'; externalRefs=@(@{referenceCategory='PACKAGE-MANAGER';referenceType='purl';referenceLocator="pkg:nuget/$($package.id)@$($package.version)"}) }
    $relations += @{spdxElementId='SPDXRef-DOCUMENT';relationshipType='DESCRIBES';relatedSpdxElement=$id}
}
[ordered]@{ spdxVersion='SPDX-2.3'; dataLicense='CC0-1.0'; SPDXID='SPDXRef-DOCUMENT'; name='Axiom-Markdown-dependencies'; documentNamespace=('https://github.com/bkins/Axiom-Markdown/spdx/' + [guid]::NewGuid().ToString()); creationInfo=@{created=$generated;creators=@('Tool: Axiom-Markdown-DependencyInventory')}; packages=$spdxPackages; relationships=$relations } | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'dependency-sbom.spdx.json') -Encoding utf8
Write-Output "Recorded $($orderedPackages.Count) production NuGet packages."
