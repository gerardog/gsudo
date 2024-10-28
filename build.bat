@echo off

pushd src\gsudo
rmdir /q /s bin
dotnet clean
dotnet publish --nologo
popd

rmdir /q /s output
mkdir output
move "src\gsudo\bin\net8.0\win-x64\publish\UniGetUI Elevator.exe" output\
move "src\gsudo\bin\net8.0\win-x64\publish\getfilesiginforedist.dll" output\

%SIGNCOMMAND% "%cd%\output\UniGetUI Elevator.exe"

echo "Resulting files from build available at %cd%\output"
pause