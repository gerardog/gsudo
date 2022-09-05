function Test-IsAdmin {
  return (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (! (Test-IsAdmin)) {
	throw "Must be admin to run tests"
}

$failure=$false

pushd $PSScriptRoot\..

dotnet test .\src\gsudo.sln --logger "trx;LogFileName=$((gi .).FullName)\TestResults.trx" --logger:"console;verbosity=normal" -v quiet -p:WarningLevel=0
if (! $?) { $failure = $true }

$env:path=(Get-Item .\src\gsudo\bin\net7.0\).FullName+";"+$env:path

gsudo -k > $null

$script  = {
 	Install-Module Pester -Force -SkipPublisherCheck
	Import-Module Pester 
	
	$configuration = New-PesterConfiguration;
	$configuration.Run.Path = "src"
	$configuration.TestResult.Enabled = $true
	$configuration.TestResult.OutputPath = "TestResults_PS$($PSVersionTable.PSVersion.Major).xml"
	$configuration.TestResult.OutputFormat = "NUnitXml"
#	$configuration.Should.ErrorAction = 'Continue'
#	$configuration.CodeCoverage.Enabled = $true  
	
    Invoke-Pester -Configuration $configuration 
}


Write-Verbose -verbose "Running PowerShell Tests on Windows PowerShell (v5.x)"
powershell $script
if (! $?) { $failure = $true }

Write-Verbose -verbose "Running PowerShell Tests on Pwsh Core (v7.x)"
pwsh $script
if (! $?) { $failure = $true }

.\src\gsudo\bin\net7.0\gsudo.exe -k

if ($failure) { exit 1 }
