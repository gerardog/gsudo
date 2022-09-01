function Test-IsAdmin {
  return (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (! (Test-IsAdmin)) {
	throw "Must be admin to run tests"
}

pushd $PSScriptRoot\..
if (-not(gcm Invoke-Pester)) { choco install Pester }

dotnet test -f net7.0 .\src\gsudo.sln --logger "trx;LogFileName=$((gi .).FullName)\TestResults.trx" 

$env:path=$env:path+";"+(Get-Item .\src\gsudo\bin\net7.0\).FullName

.\src\gsudo\bin\net7.0\gsudo -k
Get-process gsudo -ErrorAction SilentlyContinue | stop-process

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
Write-Verbose -verbose "Running PowerShell Tests on Pwsh Core (v7.x)"
pwsh $script

.\src\gsudo\bin\net7.0\gsudo.exe -k
