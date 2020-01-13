#New-Item -ItemType SymbolicLink -Path "$env:ChocolateyInstall\bin\gsudo.exe" -Target "$env:ChocolateyInstall\lib\gsudo\tools\gsudo.exe"
cmd /c mklink "$env:ChocolateyInstall\bin\gsudo.exe" "$env:ChocolateyInstall\lib\gsudo\tools\gsudo.exe"
#New-Item -ItemType SymbolicLink -Path "$env:ChocolateyInstall\bin\sudo.exe" -Target "$env:ChocolateyInstall\lib\gsudo\tools\gsudo.exe"
cmd /c mklink "$env:ChocolateyInstall\bin\sudo.exe" "$env:ChocolateyInstall\lib\gsudo\tools\gsudo.exe"
