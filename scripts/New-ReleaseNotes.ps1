[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$ChangelogPath = "",
    [Parameter(Mandatory = $true)]
    [string]$ChecksumPath,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$normalizedVersion = $Version.Trim().TrimStart("v")
if ([string]::IsNullOrWhiteSpace($normalizedVersion)) {
    throw "Version cannot be empty."
}

if ([string]::IsNullOrWhiteSpace($ChangelogPath)) {
    $ChangelogPath = Join-Path $repoRoot "CHANGELOG.md"
}

$lines = @(Get-Content -Path $ChangelogPath -Encoding utf8)
$headingPattern = "^##\s+$([regex]::Escape($normalizedVersion))(?:\s+-.*)?$"
$sectionStart = -1
for ($index = 0; $index -lt $lines.Count; $index++) {
    if ($lines[$index] -match $headingPattern) {
        $sectionStart = $index + 1
        break
    }
}

if ($sectionStart -lt 0) {
    throw "CHANGELOG.md does not contain a section for version $normalizedVersion."
}

$sectionEnd = $lines.Count
for ($index = $sectionStart; $index -lt $lines.Count; $index++) {
    if ($lines[$index] -match "^##\s+") {
        $sectionEnd = $index
        break
    }
}

$contentStart = $sectionStart
while ($contentStart -lt $sectionEnd -and [string]::IsNullOrWhiteSpace($lines[$contentStart])) {
    $contentStart++
}
$contentEnd = $sectionEnd - 1
while ($contentEnd -ge $contentStart -and [string]::IsNullOrWhiteSpace($lines[$contentEnd])) {
    $contentEnd--
}
if ($contentStart -gt $contentEnd) {
    throw "The changelog section for version $normalizedVersion is empty."
}
$section = @()
for ($index = $contentStart; $index -le $contentEnd; $index++) {
    $section += $lines[$index]
}

$checksumLine = (Get-Content -Path $ChecksumPath -Encoding ascii | Select-Object -First 1).Trim()
if ($checksumLine -notmatch "^(?<hash>[0-9a-fA-F]{64})\s+(?<asset>.+)$") {
    throw "Checksum file has an unexpected format: $ChecksumPath"
}

$hash = $Matches.hash.ToLowerInvariant()
$assetName = $Matches.asset.Trim()
$notes = @(
    "## Changes",
    ""
) + $section + @(
    "",
    "## Download and verification",
    "",
    ('- Self-contained Windows 10/11 x64 build: **{0}**' -f $assetName),
    ('- SHA-256: **{0}**' -f $hash),
    "",
    'Extract the ZIP before running BabyToys.exe. The package includes a Chinese user guide.'
)

$outputDirectory = Split-Path -Parent $OutputPath
if (![string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}
Set-Content -Path $OutputPath -Value $notes -Encoding utf8
Write-Host "Created $OutputPath"
