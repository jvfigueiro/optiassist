# OptiAssist - Performance and Interface Tweaks for Windows 10 / 11
# Optimizes visual effects, reduces animation latency and improves system responsiveness

Write-Host "Applying visual performance optimizations..." -ForegroundColor Cyan

# 1. Configure Visual Effects for 'Best Performance' preserving smooth fonts
$RegVisual = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"
if (!(Test-Path $RegVisual)) { New-Item -Path $RegVisual -Force | Out-Null }
Set-ItemProperty -Path $RegVisual -Name "VisualFXSetting" -Value 2 -Type DWord

# 2. Disable window animations when minimizing/maximizing
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop\WindowMetrics" -Name "MinAnimate" -Value "0"

# 3. Reduce menu delay (MenuShowDelay) from 400ms to 50ms
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "MenuShowDelay" -Value "50"

# 4. Disable shadows under windows and mouse pointer
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "UserPreferencesMask" -Value ([byte[]](0x90,0x12,0x03,0x80,0x10,0x00,0x00,0x00))

# 5. Disable Windows tips and suggestions
$ContentDelivery = "HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"
if (Test-Path $ContentDelivery) {
    Set-ItemProperty -Path $ContentDelivery -Name "SubscribedContent-338389Enabled" -Value 0 -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $ContentDelivery -Name "SystemPaneSuggestionsEnabled" -Value 0 -ErrorAction SilentlyContinue
}

Write-Host "Visual optimizations applied successfully!" -ForegroundColor Green
