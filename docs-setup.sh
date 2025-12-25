#!/bin/bash

# Bash script to set up DocFX documentation
echo "Setting up DocFX documentation for G0..."

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET is not installed. Please install .NET 8.0 or later."
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "Found .NET version: $DOTNET_VERSION"

# Install DocFX
echo "Installing DocFX..."
dotnet tool install -g docfx

# Build the project to generate XML documentation
echo "Building project to generate XML documentation..."
dotnet build G0.csproj --configuration Release

if [ $? -ne 0 ]; then
    echo "ERROR: Failed to build project. Please check for compilation errors."
    exit 1
fi

# Build documentation
echo "Building documentation with DocFX..."
docfx docfx.json

if [ $? -eq 0 ]; then
    echo "Documentation built successfully!"
    echo "To serve documentation locally, run: docfx serve _site"
else
    echo "ERROR: Failed to build documentation."
    exit 1
fi