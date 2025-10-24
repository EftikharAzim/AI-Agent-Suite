@echo off
REM Setup script for initializing User Secrets for local development
REM Run this from the project directory: setup-secrets.bat

echo.
echo ========================================
echo  AI Agent Suite - Secrets Setup
echo ========================================
echo.

cd src\Agent.Presentation.Cli

echo Initializing User Secrets...
dotnet user-secrets init

echo.
echo ========================================
echo Enter your API Keys
echo ========================================
echo.

set /p SERPAPI_KEY="Enter your SerpApi API Key: "
if not "%SERPAPI_KEY%"=="" (
    dotnet user-secrets set "SerpApi:ApiKey" "%SERPAPI_KEY%"
    echo ? SerpApi key saved
) else (
    echo ? SerpApi key skipped
)

echo.
set /p LLM_KEY="Enter your LLM API Key (optional, usually blank for local llama.cpp): "
if not "%LLM_KEY%"=="" (
    dotnet user-secrets set "LLM:ApiKey" "%LLM_KEY%"
    echo ? LLM key saved
)

echo.
set /p EMBEDDINGS_KEY="Enter your Embeddings API Key (optional, usually blank for local llama.cpp): "
if not "%EMBEDDINGS_KEY%"=="" (
    dotnet user-secrets set "Embeddings:ApiKey" "%EMBEDDINGS_KEY%"
    echo ? Embeddings key saved
)

echo.
echo ========================================
echo Setup Complete!
echo ========================================
echo.
echo Saved secrets are stored locally at:
echo %APPDATA%\Microsoft\UserSecrets\agensuit-cli-secrets\secrets.json
echo.
echo To verify secrets were saved, run:
echo dotnet user-secrets list
echo.
pause
