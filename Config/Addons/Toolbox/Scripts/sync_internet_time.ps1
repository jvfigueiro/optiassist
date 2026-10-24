# OptiAssist - Synchronize Date & Time via Internet (NTP)
# Ensures Windows Time service (w32time) is enabled, running, and forces an immediate resync.

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "   SYNCHRONIZING SYSTEM DATE & TIME (NTP)        " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

try {
    Write-Host "[1/3] Enabling and starting Windows Time service (w32time)..." -ForegroundColor Yellow
    Set-Service -Name w32time -StartupType Automatic -ErrorAction SilentlyContinue
    Start-Service -Name w32time -ErrorAction SilentlyContinue

    Write-Host "[2/3] Resynchronizing clock with Internet time servers..." -ForegroundColor Yellow
    w32tm /resync /rediscover

    Write-Host "[3/3] Synchronized System Date and Time:" -ForegroundColor Green
    Get-Date -Format "yyyy-MM-dd HH:mm:ss (dddd)"
}
catch {
    Write-Error "Failed to synchronize system time: $_"
}
