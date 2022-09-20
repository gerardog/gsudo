Describe "PS Invoke-Gsudo (PSv$($PSVersionTable.PSVersion.Major))" {	
	BeforeAll {
		$env:Path = (Get-Item (Join-Path $PSScriptRoot "..\gsudo.Wrappers")).FullName + ";" + $env:Path
	}
	
	BeforeEach {
		$global:ErrorActionPreference=$ErrorActionPreference='Continue';
	}
	
	It "It serializes return values maintaining its type" {
		$result = Invoke-gsudo { 1+1 }
		$result | Should -Be 2
		$result -is [System.Int32] | Should -Be $true
	}

	It "It serializes return values mantaining its properties." {
		$result = Invoke-Gsudo { Get-Date }
		$result.Year | Should -Not -BeNullOrEmpty
	}

	It "It returns an array of values mantaining its properties." {
		$result = Invoke-Gsudo { @(
			[PSCustomObject]@{ First = 'John' ;  Last = 'Smith' }
			[PSCustomObject]@{ First = 'Peter' ;  Last = 'Smith' }
		) }
		$result.Count | Should -Be 2
		$result[0].First | Should -Be 'John'
		$result[1].First | Should -Be 'Peter'
	}

	It "It accepts objects from the pipeline." {
		$currentDate = Invoke-Gsudo { Get-Date }
		$currentDate.Year | Should -Be (Get-Date).Year
	}

	It "It throws when Error thrown" {
		{ Invoke-gsudo { throw } } | Should -throw "ScriptHalted"
	}
	
	It "It throws with expression runtime errors" {
		{ Invoke-gsudo { 0/0 } } | Should -throw "Attempted to divide by zero."
		{ Invoke-gsudo { Get-InvalidCmdLet } } | Should -throw "*is not recognized*"
	}

	It "It throws with .Net Exceptions" {
		{ Invoke-gsudo { [int]::Parse('foo') } } | Should -throw "*Input string was not*"
	}
	
	It "It throws when ErrorAction = Stop" {
		{ Invoke-gsudo { Get-Item "\non-existent" -ErrorAction Stop } } | Should -throw "Cannot find path*"
	}

	It "It throws when ErrorActionPreference = Stop" {
		{ 
			Invoke-gsudo { $ErrorActionPreference = "Stop"; Get-Item "\non-existent" } 
		} | Should -throw 
	}

	It "It forwards ErrorActionPreference '<ea>' to the elevated instance" -TestCases @(
      @{ ea = 'Stop' }
      @{ ea = 'Continue' }
      @{ ea = 'Ignore' }
    ) {
      param ($ea)
		$global:ErrorActionPreference = $ErrorActionPreference = $ea; Invoke-gsudo { "$ErrorActionPreference" } | Should -be $ea
	}
	
	It "It doesn't throw when ErrorActionPreference = Continue" {
		{Invoke-gsudo { "\non-existent" | Get-Item }} | Should -not -throw	
		{"\non-existent" | Invoke-gsudo { Get-Item }} | Should -not -throw	
	}
	
	It "It doesn't throw with '-ErrorAction Continue-'" {
		$ErrorActionPreference = "Stop";
		
		{Invoke-gsudo { "\non-existent" | Get-Item -ErrorAction Continue}} | Should -not -throw
		{"\non-existent" | Invoke-gsudo { Get-Item -ErrorAction Continue}} | Should -not -throw
		
		{Invoke-gsudo { "\non-existent" | Get-Item } -ErrorAction Continue} | Should -not -throw
		{"\non-existent" | Invoke-gsudo { Get-Item } -ErrorAction Continue} | Should -not -throw

		$ErrorActionPreference = "Continue";
		{Invoke-gsudo { "\non-existent" | Get-Item }} | Should -not -throw	
	}
}
