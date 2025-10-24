#!/usr/bin/env pwsh
# Setup script for initializing User Secrets for local development
# Usage: .\setup-secrets.ps1

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  AI Agent Suite - Secrets Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Push-Location "src/Agent.Presentation.Cli"

Write-Host "Initializing User Secrets..." -ForegroundColor Yellow
dotnet user-secrets init

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Enter your API Keys" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$serpApiKey = Read-Host "Enter your SerpApi API Key"
if (-not [string]::IsNullOrWhiteSpace($serpApiKey)) {
    dotnet user-secrets set "SerpApi:ApiKey" $serpApiKey
    Write-Host "? SerpApi key saved" -ForegroundColor Green
} else {
    Write-Host "? SerpApi key skipped" -ForegroundColor Yellow
}

Write-Host ""
$llmKey = Read-Host "Enter your LLM API Key (optional, usually blank for local llama.cpp)"
if (-not [string]::IsNullOrWhiteSpace($llmKey)) {
    dotnet user-secrets set "LLM:ApiKey" $llmKey
    Write-Host "? LLM key saved" -ForegroundColor Green
}

Write-Host ""
$embeddingsKey = Read-Host "Enter your Embeddings API Key (optional, usually blank for local llama.cpp)"
if (-not [string]::IsNullOrWhiteSpace($embeddingsKey)) {
    dotnet user-secrets set "Embeddings:ApiKey" $embeddingsKey
    Write-Host "? Embeddings key saved" -ForegroundColor Green
}

Pop-Location

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$secretsPath = if ($PSVersionTable.Platform -eq "Unix") {
    "~/.microsoft/usersecrets/agensuit-cli-secrets/secrets.json"
} else {
    "$env:APPDATA\Microsoft\UserSecrets\agensuit-cli-secrets\secrets.json"
}

Write-Host "Saved secrets are stored locally at:" -ForegroundColor Cyan
Write-Host $secretsPath -ForegroundColor Gray
Write-Host ""
Write-Host "To verify secrets were saved, run:" -ForegroundColor Cyan
Write-Host "cd src/Agent.Presentation.Cli && dotnet user-secrets list" -ForegroundColor Gray
Write-Host ""
