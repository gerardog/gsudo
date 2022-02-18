function Uninstall-ChocolateyPath {
<#
.SYNOPSIS
**NOTE:** Administrative Access Required when `-PathType 'Machine'.`

This puts a directory to the PATH environment variable.

.DESCRIPTION
Looks at both PATH environment variables to ensure a path variable
does not show up on the right PATH.

.NOTES
This command will assert UAC/Admin privileges on the machine if
`-PathType 'Machine'`.

This is used when the application/tool is not being linked by Chocolatey
(not in the lib folder).

.INPUTS
None

.OUTPUTS
None

.PARAMETER PathToUninstall
The full path to a location to remove / ensure is not in the PATH.

.PARAMETER PathType
Which PATH to remove from it. If specifying `Machine`, this requires admin
privileges to run correctly.

.PARAMETER IgnoredArguments
Allows splatting with arguments that do not apply. Do not use directly.

.EXAMPLE
Uninstall-ChocolateyPath -PathToUninstall "$($env:SystemDrive)\tools\gittfs"

.EXAMPLE
Uninstall-ChocolateyPath "$($env:SystemDrive)\Program Files\MySQL\MySQL Server 5.5\bin" -PathType 'Machine'

.LINK
Install-ChocolateyPath

.LINK
Install-ChocolateyEnvironmentVariable

.LINK
Uninstall-ChocolateyEnvironmentVariable

.LINK
Get-EnvironmentVariable

.LINK
Set-EnvironmentVariable

.LINK
Get-ToolsLocation
#>
param(
  [parameter(Mandatory=$true, Position=0)][string] $pathToUninstall,
  [parameter(Mandatory=$false, Position=1)][System.EnvironmentVariableTarget] $pathType = [System.EnvironmentVariableTarget]::User,
  [parameter(ValueFromRemainingArguments = $true)][Object[]] $ignoredArguments
)

  Write-FunctionCallLogMessage -Invocation $MyInvocation -Parameters $PSBoundParameters
  ## Called from chocolateysetup.psm1 - wrap any Write-Host in try/catch

  $originalPathToUninstall = $pathToUninstall

  $statementTerminator = ";"

  # if the last digit is ;, then we are removing it
  if ($pathToUninstall.EndsWith($statementTerminator)) {
    $pathToUninstall = $pathToUninstall.Substring(0, $pathToUninstall.LastIndexOf($statementTerminator))
  }

  #get the PATH variable
  Update-SessionEnvironment
  $envPath = $env:PATH
  if ($envPath.ToLower().Contains($pathToUninstall.ToLower()))
  {
    try {
      Write-Host "PATH environment variable has $pathToUninstall in it. Removing..."
    } catch {
      Write-Verbose "PATH environment variable has $pathToUninstall in it. Removing..."
    }

    $actualPath = Get-EnvironmentVariable -Name 'Path' -Scope $pathType -PreserveVariables

    $actualPath = $actualPath.Replace($pathToUninstall, "")

    while ($actualPath.Contains($statementTerminator + $statementTerminator)) {
      $actualPath = $actualPath.Replace($statementTerminator + $statementTerminator, $statementTerminator)
    }

    if ($pathType -eq [System.EnvironmentVariableTarget]::Machine) {
      if (Test-ProcessAdminRights) {
        Set-EnvironmentVariable -Name 'Path' -Value $actualPath -Scope $pathType
      } else {
        $psArgs = "Uninstall-ChocolateyPath -pathToUninstall `'$originalPathToUninstall`' -pathType `'$pathType`'"
        Start-ChocolateyProcessAsAdmin "$psArgs"
      }
    } else {
      Set-EnvironmentVariable -Name 'Path' -Value $actualPath -Scope $pathType
    }

    #removing it from the local path as well
    $envPSPath = $env:PATH
    $envPSPath = $envPSPath.Replace($pathToUninstall, "")
    while($envPSPath.Contains($statementTerminator + $statementTerminator)) {
      $envPSPath = $envPSPath.Replace($statementTerminator + $statementTerminator, $statementTerminator)
    }
    $env:Path = $envPSPath
  }
}