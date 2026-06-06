# AnimeUP Dependency Downloader Script
# This script downloads Anime4K shaders, uosc UI, and yt-dlp.exe automatically.

$ErrorActionPreference = "Stop"

Write-Host "=== Downloading AnimeUP Dependencies ===" -ForegroundColor Cyan

# Create folders
$mpvConfigDir = Join-Path $PSScriptRoot "src\mpv-config"
$shadersDir = Join-Path $mpvConfigDir "shaders"
$distDir = Join-Path $PSScriptRoot "dist"

if (-not (Test-Path $shadersDir)) {
    New-Item -ItemType Directory -Force -Path $shadersDir | Out-Null
}
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Force -Path $distDir | Out-Null
}

# 1. Anime4K Shaders
Write-Host "`n[1/3] Downloading Anime4K v4.0.1 Shaders..." -ForegroundColor Yellow
$anime4kUrl = "https://github.com/bloc97/Anime4K/releases/download/v4.0.1/Anime4K_v4.0.1.zip"
$anime4kZip = Join-Path $distDir "Anime4K_v4.0.1.zip"

Invoke-WebRequest -Uri $anime4kUrl -OutFile $anime4kZip -UseBasicParsing
Write-Host "  -> Extracting to: $shadersDir" -ForegroundColor Green
Expand-Archive -Path $anime4kZip -DestinationPath $shadersDir -Force
Remove-Item -Path $anime4kZip -Force

# 2. uosc UI
Write-Host "`n[2/3] Downloading uosc UI..." -ForegroundColor Yellow
$uoscUrl = "https://github.com/tomasklaen/uosc/releases/latest/download/uosc.zip"
$uoscZip = Join-Path $distDir "uosc.zip"

Invoke-WebRequest -Uri $uoscUrl -OutFile $uoscZip -UseBasicParsing
Write-Host "  -> Extracting to: $mpvConfigDir" -ForegroundColor Green
Expand-Archive -Path $uoscZip -DestinationPath $mpvConfigDir -Force
Remove-Item -Path $uoscZip -Force

# 3. yt-dlp.exe
Write-Host "`n[3/3] Downloading yt-dlp.exe..." -ForegroundColor Yellow
$ytdlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"
$ytdlpDest = Join-Path $distDir "yt-dlp.exe"

Invoke-WebRequest -Uri $ytdlpUrl -OutFile $ytdlpDest -UseBasicParsing
Write-Host "  ✓ yt-dlp.exe downloaded -> $ytdlpDest" -ForegroundColor Green

Write-Host "`n=== All dependencies downloaded successfully! ===" -ForegroundColor Green
