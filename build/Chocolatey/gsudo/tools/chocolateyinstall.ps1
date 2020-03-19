function Test-IsSymLink([string]$path) {
  $file = Get-Item $path -Force -ea SilentlyContinue
  return [bool]($file.Attributes -band [IO.FileAttributes]::ReparsePoint)
}

if (Get-Process gsudo -ErrorAction SilentlyContinue) {
	$ErrorActionPreference = "Stop"
	Write-Output '##### Please close gsudo before installing. ##### '
	throw "Unable to install/uninstall if gsudo is running"
}

$bin = "$env:ChocolateyInstall\lib\gsudo\bin\"
Install-ChocolateyPath -PathToInstall $bin -PathType 'User'

if (!(Test-Path "$bin\sudo.exe"))
{
  cmd /c mklink "$bin\sudo.exe" "$bin\gsudo.exe"
}

if (Test-Path "$env:ChocolateyInstall\bin\gsudo.exe")  # Previous installers created symlinks on chocolatey\bin, we no longer need them.
{ 
  Remove-Item "$env:ChocolateyInstall\bin\gsudo.exe"
  if (Test-IsSymLink "$env:ChocolateyInstall\bin\sudo.exe")
  {
    Remove-Item "$env:ChocolateyInstall\bin\sudo.exe"
  }
}

Write-Host "Done."

