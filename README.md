# gsudo - a sudo for Windows

gsudo is a `sudo` for Windows that tries to bring a similar user-experience as *nix sudo.

When you call gsudo for the first time, it launches itself elevated in `service mode`. This will open the Windows UAC pop-up. The requested command is then ran by the elevated service and streamed to the user-level console. The service stays running for 1 minute in case you need to elevate again, and then shutdowns. Calls to gsudo before such time-out, will not show the UAC pop-up.

```gsudo```
Opens an elevated CMD in the current console.

```gsudo [command] [arguments]```
Executes the specified command, elevated, and returns.

![gsudo demo](demo.gif)

## Features

- Elevated commands are shown in the user-level console, as `*nix sudo` does, instead of opening the command in a new window.
- Does not shows the UAC pop-up every time.
- Suport for CMD commands as `*nix sudo` does, like `gsudo copy SomeOrigin SomeDestination` instead of `gsudo cmd /c copy SomeOrigin SomeDestination`

# Known issues

- This project was made in a few hours. It is more of a Proof of concept at this point. Logging, argument parsing, configurability, are in the backlog.
- Windows legacy Console is very limited, which explains some of the issues, and `gsudo` still does not support ConPTY.
- When you spawn an elevated cmd, the `<TAB>` key auto complete doesn't work as expected.
- Elevating git-bash does not work. But Powershell and Cmd does. Under investigation.
