$bin = "$env:ChocolateyInstall\lib\gsudo\bin\"
Install-ChocolateyPath -PathToInstall $bin -PathType 'User'
#Copy-Item -path "$bin\gsudo.exe" "$bin\sudo.exe"
cmd /c mklink "$bin\sudo.exe" "$bin\gsudo.exe"
