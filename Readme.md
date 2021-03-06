# Deedle Formatting Extension For DotNet Interactive

This repo contains a MVP implementation of a possible Deelde `Series` and `Frame` formatter.

## Demo Notebook

The [Jupyter Notebook](DeedleFormatterTest.ipynb) documents how to build and include the package locally.
Apart from that it shows how it renders `Series` and `Frame` objects and some ways to interface with
other extension distributed in the `Microsoft.DotNet.Interactive.ExtensionLab` package.

## How to get these bits? Gimme!

I have a feed for experimental packages [on MyGet](https://www.myget.org/feed/Packages/gregs-experimental-packages) there you can find the
[Deedle.DotNet.Interactive.Extension](https://www.myget.org/feed/gregs-experimental-packages/package/nuget/Deedle.DotNet.Interactive.Extension). Add the feed to your NuGet source list and reference the extension in your notebook.

```
#r "nuget: Deedle.DotNet.Interactive.Extension,0.1.0-alpha3
```

## Something blew up!

Great, you found a bug 🐛 Please add an issue and I'll take a look at it 🙇‍♂️