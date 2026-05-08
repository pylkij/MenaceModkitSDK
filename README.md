# MenaceModkitSDK

A fork of [MenaceAssetPacker](https://github.com/p0ss/MenaceAssetPacker) focused on the modding SDK and API for the game [Menace](https://store.steampowered.com/app/2432860/MENACE/) by Overhype Studios.

## About

The upstream project ships its SDK bundled together with the modpack loader under the name `Menace.ModpackLoader`. While modpack loading remains part of this codebase, it is not the focus here — this fork exists to develop and maintain the **SDK and API layer** that mod authors build against.

That includes systems for:

- Entity manipulation (actors, skills, tiles)
- Combat and AI hooks
- Strategy and tactical event hooks
- Custom maps
- Lua scripting
- UI injection and settings
- Localization
- REPL and runtime compilation
- Visual editor / graph interpreter

## Building

See [TUTORIAL.md](TUTORIAL.md) for full setup and build instructions.

Quick start (assumes dependencies are in place):

```
cd Menace.ModpackLoader
dotnet build --configuration Release
```

Output: `Menace.ModpackLoader/bin/Release/net6.0/Menace.ModpackLoader.dll`

## Requirements

- .NET 6 SDK
- MelonLoader v0.7.2+
- Menace (via Steam) with MelonLoader installed and run at least once

## Relationship to Upstream

This is a fork of the `Menace.ModpackLoader` component from the MenaceAssetPacker project. Changes made here are focused on the SDK surface — bug fixes, API additions, and improvements for mod authors. Credits to the original MenaceModkit team for the foundational work.