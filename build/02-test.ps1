function Test-IsAdmin {
  return (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (! (Test-IsAdmin)) {
	throw "Must be admin to run tests"
}

$failure=$false

pushd $PSScriptRoot\..

dotnet build .\src\gsudo.sln || $(exit $LASTEXITCODE)

$originalPath = $env:path

$env:path=(Get-Item .\src\gsudo.Tests\bin\Debug\net9.0\).FullName+";" + [String]::Join(";", (($ENV:Path).Split(";") -notlike "*gsudo*" | % {$_ -replace "\\$" }))

function Get-CygwinBashPath {
  @(
    "C:\tools\cygwin\bin\bash.exe",
    "C:\tools\cygwin64\bin\bash.exe",
    "C:\cygwin64\bin\bash.exe"
  ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}

function Invoke-CygwinElevationTests {
  param([string]$gsudoExePath)

  $bashExe = Get-CygwinBashPath
  if (! $bashExe) {
    choco install cygwin --confirm --no-progress --limit-output || $(exit $LASTEXITCODE)
    $bashExe = Get-CygwinBashPath
  }

  if (! $bashExe) {
    throw "Unable to find Cygwin bash.exe after installation."
  }

  $env:GSUDO_EXE_WINDOWS = $gsudoExePath

  $wrapperOutput = & $bashExe -lc 'set -e; GSUDO_EXE_FOR_CYGWIN="$(cygpath -u "$GSUDO_EXE_WINDOWS")"; gsudo() { WSLENV=WSL_DISTRO_NAME:USER:$WSLENV MSYS_NO_PATHCONV=1 "$GSUDO_EXE_FOR_CYGWIN" "$@"; }; gsudo cmd /c echo CYGWIN_WRAPPER_ELEVATION_OK' 2>&1
  $wrapperExitCode = $LASTEXITCODE
  $wrapperText = [String]::Join("`n", @($wrapperOutput))
  if ($wrapperExitCode -ne 0 -or $wrapperText -notmatch "CYGWIN_WRAPPER_ELEVATION_OK") {
    throw "Cygwin wrapper elevation attempt failed. ExitCode=$wrapperExitCode Output:`n$wrapperText"
  }

  $directOutput = & $bashExe -lc 'set -e; GSUDO_EXE_FOR_CYGWIN="$(cygpath -u "$GSUDO_EXE_WINDOWS")"; WSLENV=WSL_DISTRO_NAME:USER:$WSLENV MSYS_NO_PATHCONV=1 "$GSUDO_EXE_FOR_CYGWIN" cmd /c echo CYGWIN_DIRECT_ELEVATION_OK' 2>&1
  $directExitCode = $LASTEXITCODE
  $directText = [String]::Join("`n", @($directOutput))
  if ($directExitCode -ne 0 -or $directText -notmatch "CYGWIN_DIRECT_ELEVATION_OK") {
    throw "Cygwin direct elevation attempt failed. ExitCode=$directExitCode Output:`n$directText"
  }
}

gsudo -k
gsudo --debug cache on -p 0 -d 1
$gsudoExePath = (Get-Command gsudo.exe -ErrorAction Stop).Source
Invoke-CygwinElevationTests $gsudoExePath
$env:nokill=1
gsudo -d --debug --integrity medium dotnet test .\src\gsudo.sln --no-build --logger "trx;LogFileName=$((gi .).FullName)\TestResults.trx" --logger:"console;verbosity=normal" -v quiet -p:WarningLevel=0

if (! $?) { $failure = $true }
if ($failure) { exit 1 } # fail fast

$script  = {
	$ProgressPreference = "SilentlyContinue";
	if ((Get-InstalledModule Pester -ErrorAction SilentlyContinue).Version -lt "5.0.0") { Install-Module Pester -Force -SkipPublisherCheck }
	Import-Module Pester

	$configuration = New-PesterConfiguration;
	$configuration.Run.Path = "src"
	$configuration.TestResult.Enabled = $true
	$configuration.TestResult.OutputPath = "TestResults_PS$($PSVersionTable.PSVersion.Major).xml"
	$configuration.TestResult.OutputFormat = "JUnitXml"
#	$configuration.Should.ErrorAction = 'Continue'
#	$configuration.CodeCoverage.Enabled = $true

    Invoke-Pester -Configuration $configuration
}

gsudo --debug cache on -p 0 -d 1
Write-Verbose -verbose "Running PowerShell Tests on Windows PowerShell (v5.x)"
gsudo --integrity medium powershell -noprofile $script -outputformat text
if (! $?) { $failure = $true }

gsudo cache on -p 0 -d 1
Write-Verbose -verbose "Running PowerShell Tests on Pwsh Core (v7.x)"
gsudo --integrity medium pwsh -noprofile $script
if (! $?) { $failure = $true }

gsudo.exe -k

if ($failure) { exit 1 }
