pushd $PSScriptRoot\..

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

"-- Cleaning bin & obj folders"
Get-Item ".\src\gsudo\bin\", ".\src\gsudo\obj\" -ErrorAction Ignore | Remove-Item -Recurse -Force
"-- Building net8.0 win-arm64"
dotnet publish .\src\gsudo\gsudo.csproj -c Release -o .\artifacts\arm64  -f net8.0 -r win-arm64 --sc -p:IlcOptimizationPreference=Size -v minimal -p:WarningLevel=0 || $(exit $LASTEXITCODE)
"-- Building net8.0 win-x64"
dotnet publish .\src\gsudo\gsudo.csproj -c Release -o .\artifacts\x64    -f net8.0 -r win-x64   --sc -p:IlcOptimizationPreference=Size -v minimal -p:WarningLevel=0 || $(exit $LASTEXITCODE)
"-- Building net8.0 win-x86"
dotnet publish .\src\gsudo\gsudo.csproj -c Release -o .\artifacts\x86    -f net8.0 -r win-x86   --sc -p:PublishReadyToRun=true -p:PublishSingleFile=true -v minimal -p:WarningLevel=0 || $(exit $LASTEXITCODE)
"-- Building net4.6 AnyCpu"
dotnet publish .\src\gsudo\gsudo.csproj -c Release -o .\artifacts\net46-AnyCpu\unmerged -f net46 -p:Platform=AnyCpu -v minimal -p:WarningLevel=0 || $(exit $LASTEXITCODE)

"-- Repacking net4.6 AnyCpu into a single EXE"

ilrepack .\artifacts\net46-AnyCpu\unmerged\gsudo.exe .\artifacts\net46-AnyCpu\unmerged\*.dll /out:.\artifacts\net46-AnyCpu\gsudo.exe /target:exe /targetplatform:v4 /ndebug /wildcards || $(exit $LASTEXITCODE)

if ($?) {
	rm artifacts\net46-AnyCpu\unmerged -Recurse
	echo "artifacts\net46-AnyCpu\unmerged -> ilmerge -> artifacts\net46-AnyCpu\"
}

cp .\src\gsudo.Wrappers\* .\artifacts\x86
cp .\src\gsudo.Wrappers\* .\artifacts\x64
cp .\src\gsudo.Wrappers\* .\artifacts\arm64
cp .\src\gsudo.Wrappers\* .\artifacts\net46-AnyCpu

# Set Module version number.
Get-ChildItem .\artifacts\ -Filter gsudoModule.psd1 -Recurse | % { (Get-Content $_) -replace """0.1""", """$version_MajorMinorPatch""" | Set-Content $_.FullName }

popd