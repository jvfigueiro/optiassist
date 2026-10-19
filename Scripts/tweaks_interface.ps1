# OptiAssist - Tweaks de Desempenho e Interface Windows 10 / 11
# Otimiza efeitos visuais, reduz latencia de animacoes e melhora resposta do sistema

Write-Host "Aplicando otimizacoes visuais para alto desempenho..." -ForegroundColor Cyan

# 1. Configurar Visual Effects para 'Melhor Desempenho' preservando fontes suaves
$RegVisual = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"
if (!(Test-Path $RegVisual)) { New-Item -Path $RegVisual -Force | Out-Null }
Set-ItemProperty -Path $RegVisual -Name "VisualFXSetting" -Value 2 -Type DWord

# 2. Desativar animacoes de janelas ao minimizar/maximizar
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop\WindowMetrics" -Name "MinAnimate" -Value "0"

# 3. Reduzir atraso de menus (MenuShowDelay) de 400ms para 50ms
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "MenuShowDelay" -Value "50"

# 4. Desativar sombras sob janelas e cursor
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "UserPreferencesMask" -Value ([byte[]](0x90,0x12,0x03,0x80,0x10,0x00,0x00,0x00))

# 5. Desativar dicas e sugestoes do Windows
$ContentDelivery = "HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"
if (Test-Path $ContentDelivery) {
    Set-ItemProperty -Path $ContentDelivery -Name "SubscribedContent-338389Enabled" -Value 0 -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $ContentDelivery -Name "SystemPaneSuggestionsEnabled" -Value 0 -ErrorAction SilentlyContinue
}

Write-Host "Otimizacoes visuais aplicadas com sucesso!" -ForegroundColor Green
