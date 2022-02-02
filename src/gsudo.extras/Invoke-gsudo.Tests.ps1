Describe 'Invoke-Gsudo' {
	It "It serializes return values types mainting its type" {
		$result = invoke-gsudo { 1+1 }
		$result | Should -Be 2
		$result -is [System.Int32] | Should -Be $true
	}

	It "It serializes return values mantaining its properties." {
		$result = Invoke-Gsudo { Get-Date }
		$result.Year | Should -Not -BeNullOrEmpty
	}

	It "It returns an array of values mantaining its properties." {
		$result = Invoke-Gsudo { Get-ChildItem C:\ }
		$result.Count | Should -BeGreaterThan 1
		$result[0].FullName | Should -Not -BeNullOrEmpty
	}

	It "It accepts objects from the pipeline." {
		$currentDate = Invoke-Gsudo { Get-Date }
		$currentDate.Year | Should -Be (Get-Date).Year
	}
}
