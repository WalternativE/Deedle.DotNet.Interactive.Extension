# Deedle Formatting Extension For DotNet Interactive

This repo contains a MVP implementation of a possible Deelde `Series` and `Frame` formatter.

## Problems

### It won't load
For some reason loading the extension doesn't work when in a notebook. Might have something to do with the
fact, that it was written in F#, might have something to do, that I did a quick and dirty
[MyGet upload](https://www.myget.org/feed/gregs-experimental-packages/package/nuget/Deedle.DotNet.Interactive.Extension).
I'll have to investigate and maybe open an issue with the dotnet interactive repo.

### Dependencies are...odd
The extension mechanism (and dotnet interactive in general - at least for the F# bits) depend on some packages, that are not on NuGet.
One of those feeds contains the [private bits of F# scripting](https://dotnet.myget.org/feed/fsharp/package/nuget/FSharp.Compiler.Private.Scripting).
The other one I haven't had any luck finding is a quite recent version of `System.CommandLine`. Older versions (like two months) will work
but nothing more recent.