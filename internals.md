# gsudo Internals

## Streaming the elevated process to the non-elevated console

- Three modes the elevated process is shown in the user console: Attached, Raw (Piped), or VT (experimental full VT100 PTY)
  - **Attached** (default)
    - The elevated process is attached to the caller console using AttachConsole Win32 Api. 
    - Fastest and best experience. Console colors, keys, just works.
  - **Raw**
    - The elevated process is internally created with redirected StdIn/Out/Err. The elevated process can only append lines to the console, and read lines, but not keys.
    - This mode is used only if the caller is already redirected (`gsudo dir > outputfile.txt`), or if `--raw` parameter is specified.
    - Colored outputs shown in plain black & white. All StdErr is shown in Red.
    - In this mode, the console auto complete (arrows/<kbd>TAB</kbd> key) doesn't work as the user would expected, because they are handled by the non-elevated console host.
  - **VT** (Experimental!)
    - This mode is only used if `--vt` parameter is specified, future possible use when doing sudo remoting.
    - The elevated process is created with a ConPTY PseudoConsole and has two VT100 pipes for I/O.
    - Colors and the <kbd>TAB</kbd> key auto complete works as expected (handled by the elevated command, file autocomplete, etc).
    - Disabled by default on the default windows console host (ConHost), because `ENABLE_VIRTUAL_TERMINAL_PROCESSING` is pretty [unstable](https://github.com/microsoft/terminal/issues/3765). Works very well in Cmder/ConEmu/new Windows Terminal.

## Installers

- On Scoop, two shims are created, for `gsudo` and `sudo`. Scoop shims are currently messing with the CTRL-C key. 
- On Chocolatey, the shims do not forward the CTRL-C command to gsudo. The shim dies, but gsudo stays alive. Therefore I made choco installer to create symbolic links instead for both `gsudo` and `sudo`. (I could do this because Chocolatey requires admin privledges, which I need to create the symlinks)
- To support symbolic links I did a small'ish hack to [redirect assembly loading to the ](src/gsudo/Helpers/SymbolicLinkSupport.cs) on CurrentDomain_AssemblyResolve.

## Security

- gsudo credentials cache makes only one UAC popup required to run several commands, but it creates a security challenge: Another proces may wait until gsudo is used and then try to connect to the elevated service and execute an elevated command without the user knowlegde. 
  This is how gsudo protects this scenario from happening:
  - Named pipes channel: The gsudo elevated service and non elevated client instances currently communicate using named pipes, which provides authentication on both ends on the pipe, and allows to identify the specific Process ID in the other end.  
  - User check: The service verifies that the connecting client is the same user as the service.
  - Only gsudo can connect: The service verifies that the connecting client is the same binary (`gsudo.exe`) as the service.
  - Spoofing: A malicios process, running with the same user, may connect to the service and try to run an elevated command. gsudo protects this scenario, by only allowing the original Process ID (or their child processes) to connect. Example:
    - CMD.EXE (PID 1001) starts -> `gsudo command1` (PID 1002) -> first usage, UAC popup.
    - If 'gsudo' is a shim, it starts the real `gsudo` -> (PID 1003).
    - Under this scenario, `gsudo` finds the real parent PID (1001) and allows connection from 1001 and childs, so...
    - CMD.EXE (PID 1001) starts -> 'gsudo command2' => PID 1001, alllowed.
    - CMD.EXE (PID 1001) starts -> `powershell`, which starts -> 'gsudo command3' => child of 1001 allowed.
    - Another CMD.EXE (PID 2000) starts `gsudo` -> not allowed, another UAC popup is shown.

Don't hesitate to contact me if you find any criticism or suggestions.
