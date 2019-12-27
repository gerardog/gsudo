SetKeyDelay,100
Send cls
Send {enter}
Sleep 3000
Send scoop install gsudo{enter}
Sleep 3000
Send type MySecret.txt
Send {enter}
Sleep 1500
SendRaw gsudo type MySecret.txt
Send {enter}
Sleep 4000
SetKeyDelay,50
Send ......... Note UAC popup appeared and the command ran elevated!
SetKeyDelay,100
Sleep 1500
Send ^C
SendRaw gsudo type MySecret.txt
Send {enter}
Sleep 1000
SetKeyDelay,50
Send ......... Note no UAC popup appeared on additional commands!
SetKeyDelay,100
Sleep 1500
Send ^C
Send powershell 
Send {enter}
Sleep 1000
SendRaw Get-FileHash MySecret.txt
Send {enter}
Sleep 1000
SendRaw gsudo Get-FileHash MySecret.txt 
Send {enter}
Sleep 500
Send {enter}
SetKeyDelay,50
Send exit{enter}
Send ......... Powershell commands can also be elevated!
SetKeyDelay,100
Sleep 1500
Send ^C
Send gsudo{enter}
Sleep 1000
Send ......... Elevated shell!
Sleep 1000
Send ^C
Send del MySecret.txt{enter}
Sleep 1500
Send exit{enter}

