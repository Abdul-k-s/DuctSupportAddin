# Build and Package Script for DuctSupportAddin
# Run from DuctSupportAddin folder: .\build.ps1

param(
    [switch]$Release,
    [switch]$Installer,
    [switch]$Clean,
    [string]$InnoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"

# Project directory is where this script lives
$ProjectDir = $PSScriptRoot
$Configuration = if ($Release) { "Release" } else { "Debug" }
$OutputDir = if ($Release) { Join-Path $ProjectDir "bin\Release" } else { "$env:APPDATA\Autodesk\Revit\Addins\2025\RectangularDuctSupport" }
$InstallerDir = Join-Path $ProjectDir "Installer"
$InstallerOutput = Join-Path $InstallerDir "Output"
$CsprojFile = Join-Path $ProjectDir "DuctSupportAddin.csproj"

Write-Host "================================" -ForegroundColor Cyan
Write-Host "DuctSupportAddin Build Script" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Project: $CsprojFile" -ForegroundColor Gray
Write-Host "Config:  $Configuration" -ForegroundColor Gray
Write-Host ""

# Verify project file exists
if (-not (Test-Path $CsprojFile)) {
    Write-Host "ERROR: Project file not found: $CsprojFile" -ForegroundColor Red
    Write-Host "Make sure you're running this script from the DuctSupportAddin folder." -ForegroundColor Yellow
    exit 1
}

# Clean if requested
if ($Clean) {
    Write-Host "[1/4] Cleaning build artifacts..." -ForegroundColor Yellow
    
    $cleanDirs = @(
        (Join-Path $ProjectDir "bin"),
        (Join-Path $ProjectDir "obj"),
        $InstallerOutput
    )
    
    foreach ($dir in $cleanDirs) {
        if (Test-Path $dir) {
            Remove-Item -Path $dir -Recurse -Force
            Write-Host "  Removed: $dir" -ForegroundColor Gray
        }
    }
    
    Write-Host "  Clean complete." -ForegroundColor Green
}
else {
    Write-Host "[1/4] Skipping clean (use -Clean to clean first)" -ForegroundColor Gray
}

# Restore NuGet packages
Write-Host ""
Write-Host "[2/4] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $CsprojFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ERROR: Package restore failed!" -ForegroundColor Red
    exit 1
}
Write-Host "  Restore complete." -ForegroundColor Green

# Build the project
Write-Host ""
Write-Host "[3/4] Building $Configuration configuration..." -ForegroundColor Yellow
dotnet build $CsprojFile -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ERROR: Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "  Build complete." -ForegroundColor Green

# Create installer if requested
if ($Installer) {
    Write-Host ""
    Write-Host "[4/4] Creating installer..." -ForegroundColor Yellow
    
    # Check if Inno Setup is installed
    if (-not (Test-Path $InnoSetupPath)) {
        Write-Host "  ERROR: Inno Setup not found at: $InnoSetupPath" -ForegroundColor Red
        Write-Host "  Download from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  Or specify custom path: .\build.ps1 -Installer -InnoSetupPath 'C:\path\to\ISCC.exe'" -ForegroundColor Gray
        exit 1
    }
    
    # Create output directory
    if (-not (Test-Path $InstallerOutput)) {
        New-Item -ItemType Directory -Path $InstallerOutput | Out-Null
    }
    
    # Run Inno Setup compiler
    $issFile = Join-Path $InstallerDir "DuctSupportAddin.iss"
    & $InnoSetupPath $issFile
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ERROR: Installer creation failed!" -ForegroundColor Red
        exit 1
    }
    
    # Show output
    $installerFile = Get-ChildItem -Path $InstallerOutput -Filter "*.exe" | Select-Object -First 1
    if ($installerFile) {
        Write-Host "  Installer created: $($installerFile.FullName)" -ForegroundColor Green
        Write-Host "  Size: $([math]::Round($installerFile.Length / 1MB, 2)) MB" -ForegroundColor Gray
    }
}
else {
    Write-Host ""
    Write-Host "[4/4] Skipping installer (use -Installer to create)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output: $OutputDir" -ForegroundColor Gray
Write-Host ""

# Usage hints
if (-not $Installer) {
    Write-Host "To create installer, run:" -ForegroundColor Yellow
    Write-Host "  .\build.ps1 -Release -Installer" -ForegroundColor White
    Write-Host ""
}
