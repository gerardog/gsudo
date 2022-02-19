function Test-IsSymLink([string]$path) {
  $file = Get-Item $path -Force -ea SilentlyContinue
  return [bool]($file.Attributes -band [IO.FileAttributes]::ReparsePoint)
}

Import-Module (Join-Path (Split-Path -parent $MyInvocation.MyCommand.Definition) "Uninstall-ChocolateyPath.psm1")

$ErrorActionPreference = 'Continue'

$bin = "$env:ChocolateyInstall\lib\gsudo\bin\"

############ Clean-up previous versions
if (Test-Path "$bin\sudo.exe")
{
	Remove-Item "$bin\sudo.exe" 
}

# Remove from User Path on previous versions ( <= 0.7.1 )
Uninstall-ChocolateyPath $bin 'User'

# Remove from Path on previous versions ( <= 1.0.2 )
Uninstall-ChocolateyPath $bin 'Machine'

if (Test-Path "$env:ChocolateyInstall\bin\gsudo.exe")  # Previous installers created symlinks on chocolatey\bin, we no longer need them.
{ 
  Remove-Item "$env:ChocolateyInstall\bin\gsudo.exe"
  if (Test-IsSymLink "$env:ChocolateyInstall\bin\sudo.exe")
  {
    Remove-Item "$env:ChocolateyInstall\bin\sudo.exe"
  }
}
############

$ToolsLocation = Get-ToolsLocation 
$TargetDir = "$ToolsLocation\gsudo\v" + (Get-Item "$bin\gsudo.exe").VersionInfo.FileVersion
$SymLinkDir = "$ToolsLocation\gsudo\Current"

# Add to System Path
mkdir $TargetDir -ErrorAction Ignore
copy "$bin\*.*" $TargetDir -Exclude *.ignore -Force
Install-ChocolateyPath -PathToInstall $SymLinkDir -PathType 'Machine'

cmd /c mklink "$TargetDir\sudo.exe" "$TargetDir\gsudo.exe" 2>$null

$OldCurrentDir = Get-Item $SymLinkDir -ErrorAction ignore
if ($OldCurrentDir) 
{
	$OldCurrentDir.Delete()
}

cmd /c mklink /d "$SymLinkDir" "$TargetDir\"

# gsudo powershell module banner.
"";

Write-Output "gsudo successfully installed. Please restart your consoles to use gsudo."

if (Get-Module gsudoModule) {
	"Please restart PowerShell to update PowerShell gsudo Module."
} else {
	& { 
	"PowerShell users: To use enhanced gsudo and Invoke-Gsudo cmdlet, add the following line to your `$PROFILE"
	"  Import-Module '$SymLinkDir\gsudoModule.psm1'"
	"Or run: "
	"  Write-Output `"``nImport-Module '$SymLinkDir\gsudoModule.psm1'`" | Add-Content `$PROFILE"

	} 
}
