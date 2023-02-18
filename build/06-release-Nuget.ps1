pushd $PSScriptRoot\.. 

Get-Item .\artifacts\x64\gsudo.exe > $null || $(throw "Missing binaries/artifacts")

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

"- Cleaning Nuget template folder"
git clean .\Build\Nuget\gsudo -xf

"- Generate gsudo.nuspec"
(Get-Content  Build\Nuget\gsudo.nuspec.template) -replace '#VERSION#', "$version" | Out-File -encoding UTF8 .\Build\Nuget\gsudo.nuspec


"- Packing v$version to nuget"
mkdir Artifacts\Nuget -Force > $null
& nuget pack .\Build\Nuget\gsudo.nuspec -OutputDirectory "$((get-item Artifacts\Nuget).FullName)" || $(throw "Nuget pack failed.")

"`n- Uploading v$version to Nuget"
nuget push artifacts\nuget\gsudo.$($version).nupkg -Source https://api.nuget.org/v3/index.json || $(throw "Nuget push failed.")

	
"- Success"
popd