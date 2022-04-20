---
sidebar_position: 6
title: How it works
---

## How does it work?

When gsudo is invoked, an elevated instance of `gsudo` in `service mode` is launched (with `Verb='RunAs'`). The service will allow one elevation (or many if the `credentials cache` is enabled).

## Elevation modes

To achieve the `sudo` functionallity, `gsudo` has 4 different mechanisms implemented.

In a way, each one superseded the previous one, leading to the current default `TokenSwitch`.

In the source code, each mode is implemented as one `IProcessRenderer` (running non-elevated) and a `IProcessHost` (running in the elevated service).

Communication between both is done via two Named Pipes in the `ProtectedPrefix\Administrators` namespace. One for data and another for control. Each elevation mode defines the "communication protocol", what is sent thru each pipe.

These are the elevations modes implemented in `gsudo`, in the order they were implemented:

### Piped mode

This is a naive mechanism, implemented first more like a proof of concept (that later prooved to be useful).

The elevated `PipedProcessHost` runs the command with it's I/O redirected (For example as in `(input) > Elevated Process > (output)`), and sends all I/O to the unelevated via named pipes. 

The `PipedProcessRenderer` captures input and display the output on screen. In this mode, keyboard input is managed by the client console (without knowledge of which app you are running), and not by the elevated command/shell, hence <kbd>TAB</kbd> key auto-completition doesn't work. The user experience is far from ideal. 

The process is created with the elevated context/Environment Variables.

Pros: 

- Redirection works

Cons:

- Apps always run in redirected mode.
- Input only support of chars, not key presses.
- Struggles with encoding, specially explicit code page changes.

### VT mode (PseudoConsole)

:::note
Side story: The day Microsoft [announced](https://devblogs.microsoft.com/commandline/windows-command-line-introducing-the-windows-pseudo-console-conpty/) Pseudo Consoles for Windows, I asked myself: What can pseudoconsoles be used for? a sudo for windows!!
:::

The elevated instance runs a VT PseudoConsole, and sends all I/O it to the unelevated via named pipes.

The process is created with the elevated context/Environment Variables.

Pros:

- Full colored screen
- Full input support.

Cons:

- Not the best performance compared with the following alternatives.
- Apps always runs in console mode (not redirected). Redirection could be done but the result would be more like console screen recording than process redirection.
- Struggles with encoding IF encoding or code page changes.
- Console screen resizing still not implemented.

### Attached mode

The elevated window ['attaches'](https://docs.microsoft.com/en-us/windows/console/attachconsole) to the non elevated. The bridge is actually done by Windows and ConHost. Doesn't work with I/O redirected.

The process is created with the elevated context/Environment Variables.

Pros:

- Native console functioning. Full speed, colors, input, etc.
- No encoding issues
  
Cons:

- Does not support redirection

### TokenSwitch Mode

This is the current default mode. Using undocumented `kernel32` Apis, the unelevated instance creates the new process in paused mode, then the elevated host replaces it's primary token, and its execution is resumed.

The process is created with the **non-elevated** context/Environment Variables.

Pros:

- Native console functioning. Full speed, colors, input, etc.
- No encoding issues
- Supports redirection
  
Cons:

- (debatable) The new process inherits the non-elevated environment variables.

This last item is a confirmed problem when the elevation is done as somebody else (i.e. the current user is not admin and `RunAs` asks for credentials). For this reason and only in this scenario, the mode falls back to Attached mode (if no redirection) or Piped mode (if redirection exists).

## Should the elevated process inherit the same environment variables?

Great question. It does not on Linux/Unix Sudo, unless '-E'.
`gsudo` provides `--copyEV` to copy all the vars, except %USERNAME%.

## How do I force one mode over the other?

You shouldn't. `gsudo` should do a better job choosing than you. Still reading? OMG... Ok, I will tell you. But this is marked as deprecated and can change anytime.

To force Attached mode use `--attached` or permanently with `gsudo config ForceAttachedConsole True`

To force Piped mode use `--piped` or permanently with `gsudo config ForcePipedConsole True`

To force Piped mode use `--vt` or permanently with `gsudo config ForceVTConsole True`

Only use one at the same time.

<!--

Or configurations:

**SecurityEnforceUacIsolation=true:** This is piped mode with a hack where the Input is closed, making theoretically impossible for an unelevated process to drive the elevated world. I don't have real proof that this is less exploitable than the default, hence I never publicily documented this setting.

**ForceNewWindow:** An idea (spec still pending), to add a config setting where all elevations are done in new windows, so no isolation is broken. If I/O is redirected, the result may be streamed to the unelevated. This is still only and idea because the user experience would probably be .

-->