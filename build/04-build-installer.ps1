pushd $PSScriptRoot\..
function _exit {exit $args[0] }

if ($env:version) {
	"- Getting version from env:version"
	$version = $env:version
	$version_MajorMinorPatch=$env:version_MajorMinorPatch
} else {
	"- Getting version using GitVersion"
	$env:version = $version = $(gitversion /showvariable LegacySemVer)
	$env:version_MajorMinorPatch = $version_MajorMinorPatch=$(gitversion /showvariable MajorMinorPatch)
}
"- Using version number v$version / v$version_MajorMinorPatch"

"- Packaging v$version"
Get-ChildItem .\artifacts\ -File | Remove-Item                  # Remove files on artifacts root
Get-ChildItem .\artifacts\ -Filter *.pdb -Recurse | Remove-Item # Remove *.pdb on subfolders

Compress-Archive -Path ./artifacts/x86,./artifacts/x64,./artifacts/arm64,./artifacts/net46-AnyCpu -DestinationPath "artifacts/gsudo.portable.zip" -force -CompressionLevel Optimal
(Get-FileHash artifacts\gsudo.portable.zip).hash > artifacts\gsudo.portable.zip.sha256

"- Cleaning bin & obj folders"
Get-Item ".\src\gsudo.Installer\bin\", ".\src\gsudo.Installer\obj\" -ErrorAction Ignore | Remove-Item -Recurse -Force

(gc src\gsudo.Installer\Constants.Template.wxi) -replace '#VERSION#', "$version" | Out-File -encoding UTF8 src\gsudo.Installer\Constants.wxi

"- Building Installer arm64"
dotnet build .\src\gsudo.Installer\gsudomsi.wixproj -c Release -o .\artifacts -p:Platform=arm64 -v minimal -p:WarningLevel=0 || $(popd && _exit 1)
"- Building Installer x64"
dotnet build .\src\gsudo.Installer\gsudomsi.wixproj -c Release -o .\artifacts -p:Platform=x64   -v minimal -p:WarningLevel=0 || $(popd && _exit 1)
"- Building Installer x86"
dotnet build .\src\gsudo.Installer\gsudomsi.wixproj -c Release -o .\artifacts -p:Platform=x86   -v minimal -p:WarningLevel=0 || $(popd && _exit 1)

Remove-Item .\artifacts\*.wixpdb

"- Code Sign Installer"

& $PSScriptRoot\03-sign.ps1 artifacts\*.msi || $(popd && _exit 1)
Get-Item .\artifacts\*.msi | % {(Get-FileHash $_).Hash > "$_.sha256"}

popd