Import-Module (Join-Path (Split-Path -parent $MyInvocation.MyCommand.Definition) "Uninstall-ChocolateyPath.psm1")

$dir = "$(Get-ToolsLocation)\gsudo\Current"
Uninstall-ChocolateyPath $installPath 'Machine'

Remove-Item "$(Get-ToolsLocation)\gsudo"
