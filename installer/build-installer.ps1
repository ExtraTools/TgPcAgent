param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$PublishDirectory,
    [string]$OutputDirectory,
    [switch]$Lite
)

$ErrorActionPreference = "Stop"

function Resolve-IsccPath {
    $candidates = @(
        "C:\Users\$env:USERNAME\AppData\Local\Programs\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $registryLocations = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )

    foreach ($location in $registryLocations) {
        $entry = Get-ItemProperty $location -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -like "Inno Setup*" } |
            Select-Object -First 1

        if ($null -ne $entry -and $entry.InstallLocation) {
            $candidate = Join-Path $entry.InstallLocation "ISCC.exe"
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    throw "ISCC.exe was not found. Install Inno Setup 6 first."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDirectory = if ($PublishDirectory) {
    $PublishDirectory
} else {
    Join-Path $repoRoot "installer\publish"
}
$outputDirectory = if ($OutputDirectory) {
    $OutputDirectory
} else {
    Join-Path $repoRoot "installer\Output"
}

$projectPath = Join-Path $repoRoot "TgPcAgent.App\TgPcAgent.App.csproj"
$issPath = Join-Path $PSScriptRoot "TgPcAgent.iss"
$isccPath = Resolve-IsccPath

$isSelfContained = if ($Lite) { "false" } else { "true" }

[xml]$csproj = Get-Content $projectPath
$appVersion = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $appVersion) { $appVersion = "0.0.0" }

$installerSuffix = if ($Lite) { "-Lite" } else { "" }
$installerBaseName = "TgPcAgent-Setup-$appVersion$installerSuffix"

Write-Host "Publishing application to $publishDirectory (Self-Contained: $isSelfContained) ..."
Remove-Item $publishDirectory -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

dotnet publish $projectPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained $isSelfContained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path (Join-Path $publishDirectory "TgPcAgent.App.exe"))) {
    throw "TgPcAgent.App.exe was not found after publish."
}

Write-Host "Building installer via $isccPath ..."
if (-not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

& $isccPath `
    "/Qp" `
    "/DPublishDir=$publishDirectory" `
    "/DOutputDir=$outputDirectory" `
    "/DOutputBaseFilename=$installerBaseName" `
    $issPath

if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE."
}

$setupPath = Join-Path $outputDirectory "$installerBaseName.exe"
if (-not (Test-Path $setupPath)) {
    throw "Installer was not created: $setupPath"
}

Write-Host ""
Write-Host "Done:"
Write-Host $setupPath
