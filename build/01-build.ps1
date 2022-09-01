pushd $PSScriptRoot\..
dotnet publish .\src\gsudo\gsudo.csproj -c Release -o .\artifacts\net46-AnyCpu\unmerged -f net46 -p:Platform=AnyCpu || $(exit $LASTEXITCODE)
#dotnet publish .\src\gsudo\gsudo.csproj -c Release -o .\artifacts\net70-arm64  -f net7.0 -r win-arm64 --sc -p:PublishReadyToRun=true -p:PublishSingleFile=true || $(exit $LASTEXITCODE)
dotnet publish .\src\gsudo\gsudo.csproj -c Release -o .\artifacts\net70-x86    -f net7.0 -r win-x86   --sc -p:PublishReadyToRun=true -p:PublishSingleFile=true || $(exit $LASTEXITCODE)
dotnet publish .\src\gsudo\gsudo.csproj -c Release -o .\artifacts\net70-x64    -f net7.0 -r win-x64   --sc -p:PublishAot=true -p:IlcOptimizationPreference=Size -v minimal -p:WarningLevel=0 || $(exit $LASTEXITCODE)

ilmerge .\artifacts\net46-AnyCpu\unmerged\gsudo.exe .\artifacts\net46-AnyCpu\unmerged\*.dll /out:.\artifacts\net46-AnyCpu\gsudo.exe /target:exe /targetplatform:v4,"C:\Windows\Microsoft.NET\Framework\v4.0.30319" /ndebug /wildcards || $(exit $LASTEXITCODE)

if ($?) {
	rm artifacts\net46-AnyCpu\unmerged -Recurse
	echo "ilmerge -> artifacts\net46-AnyCpu\"
}

cp .\src\gsudo.Wrappers\* .\artifacts\net46-AnyCpu
#cp .\src\gsudo.Wrappers\* .\artifacts\net70-arm64
cp .\src\gsudo.Wrappers\* .\artifacts\net70-x86
cp .\src\gsudo.Wrappers\*	 .\artifacts\net70-x64

popd