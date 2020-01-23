$toolsPath = Split-Path -parent $MyInvocation.MyCommand.Definition
$unScriptPath = Join-Path $toolsPath "Uninstall-ChocolateyPath.psm1"

$installPath = "$env:ChocolateyInstall\lib\gsudo\bin\"

Import-Module $unScriptPath
Uninstall-ChocolateyPath $installPath 'User'
