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

Get-ChildItem .\artifacts\ -File | Remove-Item

"- Packaging v$version"
Compress-Archive -Path ./artifacts/x86,./artifacts/x64,./artifacts/net46-AnyCpu -DestinationPath "artifacts/gsudo.v$($version).zip" -force -CompressionLevel Optimal
(Get-FileHash artifacts\gsudo.v$($version).zip).hash > artifacts\gsudo.v$($version).zip.sha256

$msbuild = &"${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe

$outdir = "$PSScriptRoot\..\artifacts"

"- Building Installer"
(gc src\gsudo.Installer\Constants.Template.wxi) -replace '#VERSION#', "$version" | Out-File -encoding UTF8 src\gsudo.Installer\Constants.wxi
& $msbuild /t:Rebuild /p:Configuration=Release src\gsudo.Installer.sln /v:Minimal /p:OutputPath=$outdir || (popd && exit 1)
rm .\artifacts\gsudoSetup.wixpdb

"- Code Signing Installer"
& $PSScriptRoot\03-sign.ps1 artifacts\gsudoSetup.msi || (popd && exit 1)
(Get-FileHash artifacts\gsudoSetup.msi).hash > artifacts\gsudoSetup.msi.sha256

popd