$ErrorActionPreference = "SilentlyContinue"

Write-Output "Terminating running gsudo instances..."
taskkill /IM "gsudo.exe" /F
taskkill /IM "sudo.exe" /F