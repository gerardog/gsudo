SetKeyDelay,100
Send cls
Send {enter}
Sleep 3000
Send scoop install gsudo{enter}
Sleep 4000
Send gsudo -v
Sleep 300
Send {enter}
Sleep 500
Send sudo -v
Sleep 500
Send {enter}
Sleep 500
SetKeyDelay,40
SendRaw, ......... You can use "gsudo"  or the "sudo"  alias.
Sleep 1000
Send ^C
SetKeyDelay,100
Sleep 1000
Send type MySecret.txt
Send {enter}
Sleep 1500
SendRaw, sudo type MySecret.txt
Send {enter}
Sleep 4000
SetKeyDelay,40
SendRaw, ......... Note UAC popup appeared and the command ran elevated!
SetKeyDelay,100
Sleep 1000
Send ^C
Send sudo type MySecret.txt
Send {enter}
Sleep 1000
SetKeyDelay,40
SendRaw, ......... Note no UAC popup appeared on additional commands!
SetKeyDelay,100
Sleep 1200
Send ^C
Send sudo{enter}
Sleep 1000
SetKeyDelay,40
SendRaw, ......... Elevated shell. Note: Tab key autocomplete works
SetKeyDelay,100
Sleep 1000
Send ^C
SendRaw, echo MyNewSecret1234 > my
Sleep 200
Send {tab}
Sleep 500
Send {tab}
Sleep 500
Send {enter}
Send exit{enter}
Send powershell{enter}
SetKeyDelay,40
SendRaw, #......... Powershell commands can also be elevated!
Send {enter}
Sleep 1000
SetKeyDelay,100
SendRaw, Get-FileHash MySecret.txt
Send {enter}
Sleep 1000
aa:
SendRaw, $hash = sudo '(Get-FileHash MySecret.txt).hash' ' 
Send {enter}
Sleep 1000
SetKeyDelay,150
SendRaw, $hash
Sleep 500
Send {enter}
Sleep 5000
SetKeyDelay,50
Send exit{enter}