pushd $PSScriptRoot\..
"-- Cleaning bin & obj folders"
Get-Item ".\src\gsudo\bin\", ".\src\gsudo\obj\" -ErrorAction Ignore | Remove-Item -Recurse -Force 
"-- Building net7.0 win-arm64"
dotnet publish .\src\gsudo\gsudo.csproj -c Release -o .\artifacts\arm64  -f net7.0 -r win-arm64 --sc -p:PublishAot=true -p:IlcOptimizationPreference=Size -v minimal -p:WarningLevel=0 || $(exit $LASTEXITCODE)
"-- Building net7.0 win-x64"
dotnet publish .\src\gsudo\gsudo.csproj -c Release -o .\artifacts\x64    -f net7.0 -r win-x64   --sc -p:PublishAot=true -p:IlcOptimizationPreference=Size -v minimal -p:WarningLevel=0 || $(exit $LASTEXITCODE)
"-- Building net7.0 win-x86"
dotnet publish .\src\gsudo\gsudo.csproj -c Release -o .\artifacts\x86    -f net7.0 -r win-x86   --sc -p:PublishReadyToRun=true -p:PublishSingleFile=true -v minimal -p:WarningLevel=0 || $(exit $LASTEXITCODE)

cp .\src\gsudo.Wrappers\* .\artifacts\x86
cp .\src\gsudo.Wrappers\* .\artifacts\x64
cp .\src\gsudo.Wrappers\* .\artifacts\arm64

popd