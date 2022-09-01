pushd $PSScriptRoot\.. 
signtool.exe sign /f $env:cert_path /p $env:cert_key /fd SHA256 /t http://timestamp.digicert.com artifacts\net70-x64\*.exe artifacts\net70-x64\*.p* artifacts\net70-x86\*.exe artifacts\net70-x86\*.p* artifacts\net46-AnyCpu\*.exe artifacts\net46-AnyCpu\*.p*

if (! $?) {
popd
exit 1	
}

popd