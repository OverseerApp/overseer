#!/usr/bin/env pwsh
# Start both Overseer Server and Client in development mode

Write-Host "Starting Overseer Server and Client..." -ForegroundColor Green

# Define paths
$serverPath = Join-Path $PSScriptRoot "Overseer.Server"
$clientPath = Join-Path $PSScriptRoot "Overseer.Client"

# Detect PowerShell executable
$psExe = if (Get-Command pwsh -ErrorAction SilentlyContinue) {
    "pwsh"
} elseif (Get-Command powershell -ErrorAction SilentlyContinue) {
    "powershell"
} else {
    $PSHOME + "\powershell.exe"
}

# Function to start a process in a new window
function Start-DevProcess {
    param(
        [string]$Title,
        [string]$WorkingDirectory,
        [string]$Command,
        [string[]]$Arguments
    )
    
    $argList = @(
        "-NoExit",
        "-Command",
        "Set-Location '$WorkingDirectory'; Write-Host 'Starting $Title...' -ForegroundColor Cyan; $Command $($Arguments -join ' ')"
    )
    
    Start-Process $psExe -ArgumentList $argList -WindowStyle Normal
}

# Start Server
Write-Host "Starting Server..." -ForegroundColor Yellow
Start-DevProcess -Title "Overseer Server" `
    -WorkingDirectory $serverPath `
    -Command "dotnet" `
    -Arguments @("watch", "--Environment=Development")

# Wait a moment before starting client
Start-Sleep -Seconds 2

# Start Client
Write-Host "Starting Client..." -ForegroundColor Yellow
Start-DevProcess -Title "Overseer Client" `
    -WorkingDirectory $clientPath `
    -Command "npm" `
    -Arguments @("start")

Write-Host ""
Write-Host "Both applications are starting in separate windows." -ForegroundColor Green
Write-Host "Server: Running with dotnet watch" -ForegroundColor Cyan
Write-Host "Client: Running with npm start" -ForegroundColor Cyan
Write-Host "" 
