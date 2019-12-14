$ErrorActionPreference = 'SilentlyContinue'

(Get-Item "$env:ChocolateyInstall\bin\sudo.exe").Delete()
(Get-Item "$env:ChocolateyInstall\bin\gsudo.exe").Delete()
