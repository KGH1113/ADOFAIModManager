param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$windowsRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $windowsRoot "ADOFAIModManager.Windows\ADOFAIModManager.Windows.csproj"
$output = Join-Path $windowsRoot "dist\$Runtime"

dotnet restore $project -r $Runtime
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed. Install a stable .NET 10 SDK and review the error above."
}

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    -p:PublishProfile=win-x64 `
    -o $output
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed. Review the build error above."
}

$executable = Join-Path $output "ADOFAIModManager.Windows.exe"
if (-not (Test-Path $executable)) {
    throw "Published executable was not created: $executable"
}

$hash = (Get-FileHash -Algorithm SHA256 $executable).Hash.ToLowerInvariant()
$checksumPath = "$executable.sha256"
"$hash  $(Split-Path -Leaf $executable)" | Set-Content -Encoding ascii $checksumPath

Write-Host "Published: $executable"
Write-Host "Checksum: $checksumPath"
Write-Host "Run the executable once on a Windows test account to verify installation and file association."
