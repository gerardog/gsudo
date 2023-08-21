function Test-IsAdmin {
  return (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if ((Test-IsAdmin) -eq $false) {
 Write-Warning "This script requires local admin privileges. Elevating..."
 gsudo "& '$($MyInvocation.MyCommand.Source)'" $args
 if ($LastExitCode -eq 999 ) {
    Write-error 'Failed to elevate.'
 }
 return
}

# You are elevated here. Do admin stuff.