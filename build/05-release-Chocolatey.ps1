function Test-IsAdmin {
  return (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (! (Test-IsAdmin)) {
	throw "Must be admin to properly test generated package"
}

pushd $PSScriptRoot\..

if ($env:version) {
	"- Getting version from env:version"
	$version = $env:version
	$version_MajorMinorPatch=$env:version_MajorMinorPatch
} else {
	"- Getting version using GitVersion"
	$version = $(gitversion /showvariable LegacySemVer)
	$version_MajorMinorPatch=$(gitversion /showvariable MajorMinorPatch)
}
"- Using version number v$version / v$version_MajorMinorPatch"

"- Cleaning Choco template folder"
git clean .\Build\Chocolatey\gsudo -xf

"- Adding Artifacts"
cp artifacts\x?? .\Build\Chocolatey\gsudo\tools -Recurse -Force -Exclude *.pdb

# Generate gsudo.nuspec
(Get-Content  Build\Chocolatey\gsudo.nuspec.template) -replace '#VERSION#', "$version" | Out-File -encoding UTF8 .\Build\Chocolatey\gsudo\gsudo.nuspec
# Generate Tools\VERIFICATION.txt
Get-Content .\Build\Chocolatey\verification.txt.template | Out-File -encoding UTF8 .\Build\Chocolatey\gsudo\Tools\VERIFICATION.txt

"- Calculating Hashes "

@"
---
Version Hashes for v$version

"@ >> .\Build\Chocolatey\gsudo\Tools\VERIFICATION.txt
Get-FileHash .\Build\Chocolatey\gsudo\Tools\*\*.* | Out-String -Width 200 | %{$_.Replace("$((gi Build\Chocolatey\gsudo).FullName)\", "",'OrdinalIgnoreCase')} >> .\Build\Chocolatey\gsudo\Tools\VERIFICATION.txt
Get-childitem *.bak -Recurse | Remove-Item

"- Packing v$version to chocolatey"
mkdir Artifacts\Chocolatey -Force > $null
& choco pack .\Build\Chocolatey\gsudo\gsudo.nuspec -outdir="$((get-item Artifacts\Chocolatey).FullName)" || $(throw "Choco pack failed.")

"- Testing package"
if (choco list -lo | Where-object { $_.StartsWith("gsudo") }) {
	choco upgrade gsudo --failonstderr -s Artifacts\Chocolatey -f -pre --confirm || $(throw "Choco upgrade failed.")
} else {
	choco install gsudo --failonstderr -s Artifacts\Chocolatey -f -pre --confirm || $(throw "Choco install failed.")
}

"`n- Uploading v$version to chocolatey"
# choco push gsudo.$($version).nupkg
