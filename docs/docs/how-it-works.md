---
sidebar_position: 6
title: How it works
---

## Implicit features:

When gsudo elevates, it:
- Starts the desired process with elevated permissions.
- Keeps as much of the user context as possible:
  - The current directory
  - Environment variables (*)
- Allows i/o redirection.
- Makes the elevated app show up in the non-elevated console (unless `-n`).
- If the command is a Windows App, does not wait for it to end (unless `-w`). If it is a console app, or opened in a new window, it waits until it ends and returns its exit code.
- Handles Ctrl-C properly
- Uses the current shell to interpet the command to elevate:
  - This makes sense because the user will prepend `gsudo` to elevate a command, which meaning depends on the current context (the current shell).
  - For example, in PowerShell `gsudo mkdir x` becames `pwsh -c "mkdir x"`, while in CMD it becames `cmd /c "mkdir x"`.
- Support worldwide encodings & codepages

## How does it work?

When gsudo is invoked, an elevated instance of `gsudo` is launched (with `Verb='RunAs'`) in `service mode`. The service will allow one elevation (or many if the `credentials cache` is enabled).

The elevation mode
 
## Elevation modes

To achieve a `sudo` functionallity (i.e. to launch elevated processes keeping as much of the caller context as possible) `gsudo` has 4 different mechanisms implemented.

In a way, each one superseded the previous one, leading to the current default `TokenSwitch`.

In the source code, each mode is implemented as one `IProcessRenderer` (running non-elevated) and a `IProcessHost` (running in the elevated service). Both  communicate via the afore mentioned named pipes.

Communication between both is done via two Named Pipes in the `ProtectedPrefix\Administrators` namespace. One for data and another for control. The each elevation mode defines the "communication protocol", what is sent thru each pipe.

### Piped mode:

This is a naive mechanism, and the one that was implemented first, more like a proof of concept (that later prooved to be useful).

The elevated `PipedProcessHost` runs the command with it's I/O redirected (For example as in `(input) > Elevated Process > (output)`), and sends all I/O to the unelevated via named pipes. 

The `PipedProcessRenderer` captures input and display the output on screen. In this mode, keyboard input is managed by the client console (without knowledge of which app you are running), and not by the elevated command/shell, hence <kbd>TAB</kbd> key auto-completition doesn't work. The user experience is far from ideal.

Pros: 
 - Redirection works

Cons:
 - Apps always run in redirected mode.
 - Input only support of chars, not key presses.
 - Struggles with encoding, specially explicit code page changes.

### VT mode (PseudoConsole)

:::note
Side story: It was actually when Microsoft [announced](https://devblogs.microsoft.com/commandline/windows-command-line-introducing-the-windows-pseudo-console-conpty/) of Pseudo Consoles for Windows, that I asked myself: What can pseudoconsoles be used for? And then the idea 
:::

This was actually the 

The elevated instance runs a VT pseudoconsole, and sends all I/O it to the unelevated via named pipes. Do not resize the window in this mode, please. 

### **Attached mode:** 
The elevated window 'attaches' to the non elevated. The bridge is actually done by Windows and ConHost. Doesn't work with I/O redirected (where Piped excels)

### **TokenSwitch Mode:** 
(current default) Using an undocumented api, the unelevated creates the new process in paused mode, then the elevated replaces it's token via WinApi, and its execution is resumed.

Or configurations:

**SecurityEnforceUacIsolation=true:** This is piped mode with a hack where the Input is closed, making theoretically impossible for an unelevated process to drive the elevated world. I don't have real proof that this is less exploitable than the default, hence I never publicily documented this setting.

**ForceNewWindow:** An idea (spec still pending), to add a config setting where all elevations are done in new windows, so no isolation is broken. If I/O is redirected, the result may be streamed to the unelevated. This is still only and idea because the user experience would probably be .
