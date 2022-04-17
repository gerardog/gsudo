---
sidebar_position: 5
title: Security Considerations
---

## Why Windows does not have a `sudo` command?

To answer this question, we first have to take a look back at the history.

In August 2002, Chris Paget released a white paper describing a form of attack against event-driven systems that he termed Shatter Attack. It allowed processes in the same session to bypass security restrictions by abussing Windows Message loop.

Microsoft response was to add "User Interface Privilege Isolation" (UIPI) and the "User Access Control" (UAC) popup to the next mayor release: Windows Vista. Privileged processes would then ran "elevated", out of reach of lower non-admin processes.

I assume that at this point Microsoft decided not to make a `sudo` tool for windows. It would be an unwanted bridge between two intentionally (supposedly) isolated worlds..

But, let me [quote Raymond Chen from Microsoft](https://devblogs.microsoft.com/oldnewthing/20160816-00/?p=94105):

> There’s a setting that lets you specify how often you want to be prompted by UAC. You can set any of four levels:
>
> - Always notify
> - Notify only when apps try to change settings, use the secure desktop `(Default mode)`
> - Notify only when apps try to change settings, don’t use the secure desktop
> - Never notify
>
> Although it looks like there are four settings, in a theoretical sense, there really are only two settings.
>
> - Always notify
> - Meh
>
> The reason why all the other options collapse into Meh is that the `Notify only when apps try to change settings` option can be subverted by any app simply by (...)

And, up to this day, Windows 10 & 11, defaults to "Meh". Why? User convenience (against security).

Later, it was assumed publicily that `UIPI` in default mode is not a security boundary. I will [quote Microsoft documentation](https://docs.microsoft.com/en-us/troubleshoot/windows-server/windows-security/disable-user-account-control#:~:text=More%20important%2C%20Same%2Ddesktop%20Elevation,be%20considered%20a%20convenience%20feature.) :

> Same-desktop Elevation in UAC isn't a security boundary. It can be hijacked by unprivileged software that runs on the same desktop. Same-desktop Elevation should be considered a convenience feature.

This literally means: <b>UAC is a tool to protect you from shooting yourself in the foot. The only thing that protects you from malicious processes is your Anti Virus software</b>.

This contradicts the premises on which they decided not to create a Windows `sudo`, because UAC is not really a complete isolation that `sudo` will break. And if you want to avoid shooting yourself in the foot (running as admin something not intended to be admin), Window isolation is not always the best alternative, because:

- You loose important time by switching between elevated and unelevated windows, which leads to running non-admin stuff as admin to avoid context switching...
- ... or even worse: you suffer from "elevation fatigue", which is, you tend to elevate everything 'just in case' one future command could require admin privileges.

Therefore, in my opinion, it is fine to run a sudo tool for windows. A tool that allows you to cherry-pick which commands to elevate.

## Is it safe to run gsudo?

To answer this question, lets explore how gsudo could be used as an attack vector for escalation of privileges.
