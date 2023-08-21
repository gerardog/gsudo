@gsudo status IsElevated --no-output || (gsudo "%~f0" & exit /b)
:: You are elevated here. Do admin stuff.
