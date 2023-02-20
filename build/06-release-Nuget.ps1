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

"- Packing v$version to nuget"
dotnet build .\Build\Nuget\gsudo.csproj /p:Version=$version -o artifacts\Nuget || $(throw "Nuget pack failed.")

"`n- Uploading v$version to Nuget"
gi "artifacts\nuget\gsudo.$($version).nupkg" || $(throw "Nuget push failed.")
nuget push artifacts\nuget\gsudo.$($version).nupkg -Source https://api.nuget.org/v3/index.json || $(throw "Nuget push failed.")

	
"- Success"
popd