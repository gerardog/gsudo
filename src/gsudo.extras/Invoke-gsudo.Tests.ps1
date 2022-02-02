Describe "Invoke-Gsudo with PowerShell v$($PSVersionTable.PSVersion.ToString())" {
	It "It serializes return values maintaining its type" {
		$result = invoke-gsudo { 1+1 }
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
}
