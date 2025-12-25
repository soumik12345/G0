# PowerShell script to set up DocFX documentation
Write-Host "Setting up DocFX documentation for G0..." -ForegroundColor Green

# Check if .NET is installed
try {
    $dotnetVersion = dotnet --version
    Write-Host "Found .NET version: $dotnetVersion" -ForegroundColor Yellow
} catch {
    Write-Host "ERROR: .NET is not installed. Please install .NET 8.0 or later." -ForegroundColor Red
    exit 1
}

# Install DocFX
Write-Host "Installing DocFX..." -ForegroundColor Yellow
dotnet tool install -g docfx

# Build the project to generate XML documentation
Write-Host "Building project to generate XML documentation..." -ForegroundColor Yellow
dotnet build G0.csproj --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to build project. Please check for compilation errors." -ForegroundColor Red
    exit 1
}

# Build documentation
Write-Host "Building documentation with DocFX..." -ForegroundColor Yellow
docfx docfx.json

if ($LASTEXITCODE -eq 0) {
    Write-Host "Documentation built successfully!" -ForegroundColor Green
    Write-Host "To serve documentation locally, run: docfx serve _site" -ForegroundColor Cyan
} else {
    Write-Host "ERROR: Failed to build documentation." -ForegroundColor Red
    exit 1
}