function Test-IsSymLink([string]$path) {
  $file = Get-Item $path -Force -ea SilentlyContinue
  return [bool]($file.Attributes -band [IO.FileAttributes]::ReparsePoint)
}

Import-Module (Join-Path (Split-Path -parent $MyInvocation.MyCommand.Definition) "Uninstall-ChocolateyPath.psm1")

$ErrorActionPreference = 'Continue'
$ToolsLocation = Get-ToolsLocation 

if ([Environment]::Is64BitOperatingSystem -eq $true) {
  $bin = "$env:ChocolateyInstall\lib\gsudo\tools\x64"
} else {
  $bin = "$env:ChocolateyInstall\lib\gsudo\tools\x86"
}

if ([System.Environment]::CurrentDirectory -like "$ToolsLocation*") {
	Write-Output -Verbose "Changing directory to $ToolsLocation to ensure successfull install/upgrade."
	Set-Location $ToolsLocation
}

$TargetDir = ("$ToolsLocation\gsudo\v" + ((Get-Item "$bin\gsudo.exe").VersionInfo.ProductVersion -split "\+" )[0])
$SymLinkDir = "$ToolsLocation\gsudo\Current"

# Add to System Path
mkdir $TargetDir -ErrorAction Ignore > $null
copy "$bin\*" $TargetDir -Exclude *.ignore -Force
Install-ChocolateyPath -PathToInstall $SymLinkDir -PathType 'Machine'

cmd /c mklink "$TargetDir\sudo.exe" "$TargetDir\gsudo.exe" 2>$null

# Copy gsudoModule to "$env:ProgramFiles\PowerShell\Modules\gsudoModule"
$PSModulesTargetDir = "$env:ProgramFiles\PowerShell\Modules\gsudoModule"
md $PSModulesTargetDir -ErrorAction SilentlyContinue
copy "$bin\*.ps*" $PSModulesTargetDir -Exclude *.ignore -Force

$OldCurrentDir = Get-Item $SymLinkDir -ErrorAction ignore
if ($OldCurrentDir) 
{
	$OldCurrentDir.Delete()
}

cmd /c mklink /d "$SymLinkDir" "$TargetDir\"

# gsudo powershell module banner.
"";

if (Get-Module gsudoModule) {
	"Please restart all your PowerShell consoles to update PowerShell gsudo Module."
} else {
	& { 
	"PowerShell users: Add auto-complete to gsudo by adding the following line to your `$PROFILE"
	"  Import-Module 'gsudoModule'"
	"Or run: "
	"  Write-Output `"``nImport-Module 'gsudoModule'`" | Add-Content `$PROFILE"

	} 
}

Write-Output "gsudo successfully installed. Please restart your consoles to use gsudo.`n"
