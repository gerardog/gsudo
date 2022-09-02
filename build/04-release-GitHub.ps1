pushd $PSScriptRoot\..

if (! $env:version) {
	"- Getting version using GitVersion"
	$env:version = $(gitversion /showvariable SemVer)
	$env:version_MajorMinorPatch=$(gitversion /showvariable MajorMinorPatch)
}

Get-ChildItem .\artifacts\ -File | Remove-Item

"- Packaging v$env:version"
Compress-Archive -Path ./artifacts/x86,./artifacts/x64,./artifacts/net46-AnyCpu -DestinationPath "artifacts/gsudo.v$($env:version).zip" -force -CompressionLevel Optimal
(Get-FileHash artifacts\gsudo.v$($env:version).zip).hash > artifacts\gsudo.v$($env:version).zip.sha256

$msbuild = &"${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe

$outdir = "$PSScriptRoot\..\artifacts"

"- Building Installer"
& $msbuild /t:Rebuild /p:Configuration=Release src\gsudo.Installer.sln /v:Minimal /p:OutputPath=$outdir || (popd && exit 1)
rm .\artifacts\gsudoSetup.wixpdb 

"- Code Signing Installer"
& $PSScriptRoot\03-sign.ps1 artifacts\gsudoSetup.msi || (popd && exit 1)
(Get-FileHash artifacts\gsudoSetup.msi).hash > artifacts\gsudoSetup.msi.sha256

# Cleanup
$env:version=$env:version_MajorMinorPatch=$null; #force recalc
popd