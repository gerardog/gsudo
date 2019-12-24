# gsudo Backlog

- Code signing. In progresss, see issue #1
- In Raw mode, open StdErr and StdOut in one pipe/file, to fix random ordering of stderr/stdout.
- gsudo --nocache (service will quit immediately after process ends and will not elevate other commands with cached credentials) (find better --syntax)
- gsudo --fullcache(?) (service will not quit and can be shared, clearly not very secure)
- Allow to specify other username. (RunAsUser verb)
- Console resize on vt mode doesnt work. 

## Other not so likely ideas

- Third consecutive Ctrl-C should ask if the child process must be kept running or killed. (currently killed in Raw and VT modes)
- Remote sudo. Run process on another machine / as in PSExec. (security?)

## Completed

- Chocolatey package / Scoop package / Release on github.
- VT console extended keys (F1-F12, CTRL+?, HOME,PAGEUP)
- WinPty/VT100 support: When in VT mode, processes are spawn using a PseudoConsole. Rendering could be done using Windows Console ENABLE_VIRTUAL_TERMINAL processing flag but it is pretty [unstable](https://github.com/microsoft/terminal/issues/3765). So it is disabled by default unless you are running inside ConEmu/Cmder which are VT100 ready terminals.
  VT Mode is enabled automatically if you run inside a ConEmu/Cmder or if you use `--vt` flag.

- Detect if target process is windows app or console app. Different wait: a console app is awaited by default until the process ends, a windows app is not. Can be overrided if user specifies -w (wait) or -n (new window). 

- Better Ctrl-c handling (VT and Raw have quite different implementatios)

- Configuration settings persistent storage.

``` console
    gsudo config (show all current user config)
    gsudo config {setting} (return current value)
    gsudo config {setting} {value} (save new value)

    gsudo config CredentialsCacheDuration 0:0 (no cache)
    gsudo config CredentialsCacheDuration 5:00 (5 minutes)
    gsudo config Prompt "$P# " (elevated command prompt)
    gsudo config CredentialsCache disabled (disable service) -> not implemented
```
