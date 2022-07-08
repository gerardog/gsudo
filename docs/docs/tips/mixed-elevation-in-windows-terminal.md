---
sidebar_position: 1
#id: usage
title: Windows Terminal
hide_title: true
---

## Mixed Elevation of tabs

You can have an elevated tab inside `Windows Terminal` using gsudo. Just add a new profile (or edit an existing one), edit your command and prepend `gsudo `. Or just put `gsudo cmd` or `gsudo pwsh` as your command.

However, I personally prefer to not do this and use the following alternative.

## Elevate on demand

Use gsudo's ability to elevate the current shell in the current window. Just run `gsudo` and it will invoke caller shell (CMD, PowerShell, etc) elevated. Or even better, prepend `gsudo` to elevate specific commands.
