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

$env:path=(Get-Item .\src\gsudo.Tests\bin\Debug\net7.0\).FullName+";" + [String]::Join(";", (($ENV:Path).Split(";") -notlike "*gsudo*" | % {$_ -replace "\\$" }))

gsudo -k
gsudo --debug cache on -p 0 -d 1
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
