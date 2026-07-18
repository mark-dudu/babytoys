[CmdletBinding()]
param(
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64",
    [string]$OutputDirectory = "artifacts",
    [string]$DotNetPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "BabyToys.csproj"
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ([string]::IsNullOrWhiteSpace($DotNetPath) -and $null -ne $dotnetCommand) {
    $DotNetPath = $dotnetCommand.Source
}
if ([string]::IsNullOrWhiteSpace($DotNetPath)) {
    $windowsSdkPath = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
    if (Test-Path $windowsSdkPath) {
        $DotNetPath = $windowsSdkPath
    }
}
if ([string]::IsNullOrWhiteSpace($DotNetPath) -or -not (Test-Path $DotNetPath)) {
    throw "Unable to locate dotnet. Install the .NET 10 SDK or pass -DotNetPath."
}

$project = [xml](Get-Content -Raw $projectPath)
$version = [string]$project.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "BabyToys.csproj must define Version."
}

$artifactRoot = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    [IO.Path]::GetFullPath($OutputDirectory)
} else {
    [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
}
$publishDirectory = Join-Path $artifactRoot "publish-$Runtime"
$packageBaseName = "BabyToys-v$version-$Runtime"
$packagePath = Join-Path $artifactRoot "$packageBaseName.zip"
$checksumPath = "$packagePath.sha256"

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
if (Test-Path $publishDirectory) {
    Remove-Item -Recurse -Force $publishDirectory
}
if (Test-Path $packagePath) {
    Remove-Item -Force $packagePath
}
if (Test-Path $checksumPath) {
    Remove-Item -Force $checksumPath
}

$publishArguments = @(
    "publish",
    "`"$projectPath`"",
    "--configuration", "Release",
    "--runtime", $Runtime,
    "--self-contained", "true",
    "--output", "`"$publishDirectory`"",
    "/p:DebugType=None"
)
$publishProcess = Start-Process `
    -FilePath $DotNetPath `
    -ArgumentList $publishArguments `
    -Wait `
    -PassThru `
    -NoNewWindow
if ($publishProcess.ExitCode -ne 0) {
    throw "dotnet publish failed with exit code $($publishProcess.ExitCode)."
}

Copy-Item (Join-Path $repoRoot "packaging\README.md") (Join-Path $publishDirectory "README.md")
Copy-Item (Join-Path $repoRoot "LICENSE") $publishDirectory
Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $packagePath -CompressionLevel Optimal

$hash = (Get-FileHash -Algorithm SHA256 $packagePath).Hash.ToLowerInvariant()
Set-Content -Path $checksumPath -Value "$hash  $([IO.Path]::GetFileName($packagePath))" -Encoding ascii

Write-Host "Created $packagePath"
Write-Host "Created $checksumPath"
