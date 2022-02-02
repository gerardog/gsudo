Describe 'Gsudo' {
	BeforeAll {
		$Path = (Get-Item (Join-Path $PSScriptRoot "gsudo.psm1")).FullName
		$Path | Should -not -BeNullOrEmpty
		Import-Module $Path 
	}

	It "It serializes return values as string." {
		$result = gsudo "1+1"
		$result | Should -Be "2"
		$result -is [System.String] | Should -Be $true
	}

	It "When invoked as `gsudo !!`, It elevates the last command executed" {
		$history = [PSCustomObject] @{
			CommandLine        = "Write-Output 'Hello World'"
			ExecutionStatus    = [Management.Automation.Runspaces.PipelineState]::Completed
			StartExecutionTime = Get-Date
			EndExecutionTime   = Get-Date
		}
		$history | Add-History

		gsudo !! | Should -Be 'Hello World'
	}
}
