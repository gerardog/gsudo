Import-Module (Join-Path (Split-Path -parent $MyInvocation.MyCommand.Definition) "Uninstall-ChocolateyPath.psm1")

function MarkFileDelete {
param(
    [parameter(Mandatory=$true)]
	[string] $path
)

# the code below has been used from
#    https://blogs.technet.com/b/heyscriptingguy/archive/2013/10/19/weekend-scripter-use-powershell-and-pinvoke-to-remove-stubborn-files.aspx
# with inspiration from
#    http://www.leeholmes.com/blog/2009/02/17/moving-and-deleting-really-locked-files-in-powershell/
# and error handling from
#    https://blogs.technet.com/b/heyscriptingguy/archive/2013/06/25/use-powershell-to-interact-with-the-windows-api-part-1.aspx

Add-Type @'
    using System;
    using System.Text;
    using System.Runtime.InteropServices;
       
    public class Posh
    {
        public enum MoveFileFlags
        {
            MOVEFILE_DELAY_UNTIL_REBOOT         = 0x00000004
        }
 
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, MoveFileFlags dwFlags);
        
        public static bool MarkFileDelete (string sourcefile)
        {
            return MoveFileEx(sourcefile, null, MoveFileFlags.MOVEFILE_DELAY_UNTIL_REBOOT);         
        }
    }
'@
	$deleteResult = [Posh]::MarkFileDelete($path)
	
    if ($deleteResult) {
        write-Warning "(Delete of $path failed: Will be deleted at next boot.)"
    } else {
		write-Warning "(Error marking $path for deletion at next boot.)"
    }
}


$installPath = "$(Get-ToolsLocation)\gsudo"
Uninstall-ChocolateyPath "$installPath\Current" 'Machine'

$ErrorActionPreference="Ignore"

# Delete symlinks in Pwsh 5.
Get-ChildItem $installPath -Recurse |? LinkType -eq 'SymbolicLink'|%{$_.Delete()}
# Delete the rest.
Remove-Item $installPath -Recurse -Force -ErrorAction Ignore
Remove-Item $installPath -Recurse -Force -ErrorAction Ignore

if (Test-Path $installPath) {
	# Files are in use so delete failed.
	# Rename used files and directories. 

    Get-ChildItem $installPath -Recurse -Exclude "*.deleteMe" | Sort-Object -Descending {(++$script:i)} | % { Rename-Item -Path $_.FullName -NewName ($_.Name + ".deleteMe")  ; } *> $NULL
	# Mark remaining for delete after restart.
	Get-ChildItem $installPath -Recurse | % { MarkFileDelete ( $_.FullName) }
	MarkFileDelete ( $installPath );
}

$ErrorActionPreference = 'Continue'
