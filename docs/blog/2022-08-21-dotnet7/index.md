---
slug: gsudo-dotnet-7
title: Migrating gsudo to .NET 7
authors: [gerardog]
tags: []
---


The first gsudo versions were made in 2019, around the release of .NET Core 3. I tried to figure out how .NET Core's redistribution model worked, but it didn't worked for me. One should either redistribute the full runtime, or ask the user to manually download and install it. There was another option: to build a self-contained app (with runtime embedded) but that increased the output size by more than 80mb!

The alternative was to target .NET Framework v4.6. This version is bundled with every Windows 10/11. When targeting it, gsudo build output size was around 100kb. The app on v4.6 loaded faster, so targeting v4.6 seemed logical. It was small, fast, and the installation didn't required any big runtime redistribution or additional steps.

Recently, things have changed a little with [.NET 7 Preview 3 announcement](https://devblogs.microsoft.com/dotnet/announcing-dotnet-7-preview-3/#faster-lighter-apps-with-native-aot) and NativeAOT: "Publishing your app as native AOT produces an app that is self-contained and that has been ahead-of-time (AOT) compiled to native code. Native AOT apps start up very quickly and use less memory. Users of the application can run it on a machine that doesn't have the .NET runtime installed."

I'm almost sold! Let's try it out... *(sounds of frenetic typing)*... Ok, that was [18 files changed](https://github.com/gerardog/gsudo/compare/971ea97c...7c0c1b71). Now I have a multi-target project that can compile for v4.6 or v7. Not bad. 

But not everyone's cup of tea: NativeAOT builds for each specific platform, so instead of having one AnyCPU build, now I have two (x64 and x86). But also, NativeAOT is only supported on x64. The closest for x86 is a self-contained build with Ready2Run.

Let's do some test the performance. So first let´s build for each platform:

``` powershell
# Build .NET Framework v4.6
msbuild /t:Restore /p:RestorePackagesConfig=true src\gsudo.sln /v:Minimal /p:TargetFrameworkVersion=v4.6
msbuild /t:Rebuild /p:Configuration=Release src\gsudo\gsudo.csproj /v:Minimal /p:WarningLevel=0 /p:TargetFrameworkVersion=v4.6
# Build .NET 7.0 x64 build with NativeAOT
dotnet clean .\src\gsudo\gsudo.csproj -f net7.0 | Out-null
dotnet publish .\src\gsudo\gsudo.csproj -c Release -f net7.0 -r win-x64 --sc -p:PublishAot=true -p:IlcOptimizationPreference=Size -v minimal -p:WarningLevel=0
# Build .NET 7.0 x86 build with Ready2Run
dotnet clean .\src\gsudo\gsudo.csproj -f net7.0 -r win-x86 | Out-null
dotnet publish .\src\gsudo\gsudo.csproj -c Release -f net7.0 -r win-x86 --sc -p:PublishReadyToRun=true -p:PublishSingleFile=true -v minimal -p:WarningLevel=0
```

Now let´s meassure 100 elevations using a preexisting credentials cache. I am using `gsudo -d` to force an elevation using `cmd.exe`, which loads much faster and predictable than `Powershell`. Then I repeated the tests with each different build.

``` powershell
$gsudo="C:\git\gsudo\src\gsudo\bin\net46\gsudo.exe"
& $gsudo cache on | out-null 	# start a cache session.
& $gsudo -d dir | out-null		# test the cache session.
Measure-Command { 1..100 | % { & $gsudo -d dir} } | select -Property TotalSeconds | Format-List
& $gsudo -k | out-null 			# close cache.
```

The first results were absurd. I found out Windows Defender takes extra time to verify unsigned apps in the cloud. So I disabled Defender and started over.

|                                 | v1.3.0 (Net 4.6 + ilmerge + CodeSigned) | Latest (Net 4.6 unsigned) | Net 7.0 x64 NativeAOT (unsigned) | Net 7.0 x86 Ready2Run (unsigned) |
| ------------------------------- | --------------------------------------- | ------------------------- | -------------------------------- | -------------------------------- |
| Size                            | 180.5 KB                                | 208.5 KB                  | 6.17 MB                          | 16.89 MB                         |
| Time to elevate 100 times (sum) | 12.75 s                                 | 12.62 s                   | 7.83 s                           | 10.94 s                          |
| Performance improvement         | (baseline)                              | 1.04%                     | 38.62%                           | 14.18%                           |

Wow! An improvement of 38.62% is significant enough to ignore the fact that the file size has increased 6 MB, or almost 30 times. 

## Should gsudo target .NET 7.0?

The problem is that the installer should provide for all platforms, and so far I´ve only focused on x64. What options do I have?

- I could fall back to Net 4.6 (targeting AnyCPU) for the remaining platforms. The down-side would be different prerequisites for each scenario. But the installer would only grow 6mb.
- Or include x86 Net 7.0 Read2Run version. It´s faster, but takes 16mb. The setup including both would  take 23 MB (actually 10mb when compressed)!  

## Current situation

Right now my code-signing certificate is expired. Once I´ve got a new certificate, I will be making a test release. I will update this post then.

