Describe "PS Gsudo (PSv$($PSVersionTable.PSVersion.Major))" {	
	BeforeAll {
		$env:Path = (Get-Item (Join-Path $PSScriptRoot "..\gsudo.Wrappers")).FullName + ";" + $env:Path
		$Path = (Get-Item (Join-Path $PSScriptRoot "..\gsudo.Wrappers\gsudoModule.psm1")).FullName
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
	
	It "Elevates a ScriptBlock." {
		$result = gsudo { (1+1) }
		$result | Should -Be "2"
		$result -is [System.Int32] | Should -Be $true
	}

	It "Elevates a ScriptBlock with arguments." {
		$result = gsudo { "$($args[1]) $($args[0])" } -args "World", "Hello"
		$result | Should -Be "Hello World"
		$result -is [System.String] | Should -Be $true
	}

	It "Return can be captured." {
		$result = gsudo {Get-Command Get-Help}
		$result.CommandType -eq [System.Management.Automation.CommandTypes]::Cmdlet | Should -BeTrue
	}
}
