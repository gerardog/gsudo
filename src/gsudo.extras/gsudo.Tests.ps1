Describe "PS Gsudo (v$($PSVersionTable.PSVersion.ToString()))" {
	BeforeAll {
		$Path = (Get-Item (Join-Path $PSScriptRoot "gsudoModule.psm1")).FullName
		$Path | Should -not -BeNullOrEmpty
		Import-Module $Path
	}

	BeforeEach {
		$ErrorActionPreference='Continue'
	}

	It "It serializes return values as string." {
		$result = gsudo "1+1"
		$result | Should -Be "2"
		$result -is [System.String] | Should -Be $true
	}

	It "When invoked as `gsudo !!`, It elevates the last command executed" {

		@"
#TYPE Microsoft.PowerShell.Commands.HistoryInfo
"Id","CommandLine","ExecutionStatus","StartExecutionTime","EndExecutionTime","Duration"
"1","Write-Output 'Hello World'","Completed","2/2/2022 12:13:11 PM","2/2/2022 12:13:11 PM","00:00:00.0421414"
"@ | ConvertFrom-Csv | Add-History -ErrorAction stop
	
		gsudo !! | Should -Be 'Hello World'
	}
}
