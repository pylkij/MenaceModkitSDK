# Menace.ModpackLoader — Build from Source Tutorial

**Who this is for:** programmers comfortable editing files but new to C# build tooling.
**What you end up with:** a compiled `Menace.ModpackLoader.dll` you can drop into your game, built from the MenaceModkitSDK source with one command.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Clone the Repository](#2-clone-the-repository)
3. [Gather the External Dependencies](#3-gather-the-external-dependencies)
4. [Verify Your Folder Structure](#4-verify-your-folder-structure)
5. [Build the DLL](#5-build-the-dll)
6. [Troubleshooting Common Errors](#6-troubleshooting-common-errors)
7. [Making and Testing Your Changes](#7-making-and-testing-your-changes)
8. [Deploying Your DLL](#8-deploying-your-dll)

---

## 1. Prerequisites

You need three tools installed before anything else. All are free.

### 1.1 — Git

Git is used to download the source code and track your changes.

- **Windows:** Download from https://git-scm.com/download/win and run the installer. Accept all defaults.
- **Linux (Ubuntu/Debian):**
  ```
  sudo apt update && sudo apt install git
  ```
- **Linux (Fedora):**
  ```
  sudo dnf install git
  ```

Verify it worked:
```
git --version
```
Any version output (e.g. `git version 2.43.0`) means you're good.

### 1.2 — .NET 6 SDK

This is the compiler and build toolchain for C#.

> **Important:** You need the **SDK**, not just the Runtime. They are different downloads.

- Go to: https://dotnet.microsoft.com/en-us/download/dotnet/6.0
- Under **.NET 6.0 SDK**, download the installer for your OS and run it.

Verify it worked:
```
dotnet --version
```
You should see `6.0.x`. If you only see `7.x` or `8.x`, you still need to install 6.0 alongside it — multiple SDK versions coexist fine.

### 1.3 — A Code Editor

**Recommended: Visual Studio Code** (free, all platforms)
- Download from: https://code.visualstudio.com
- After installing, open VS Code → Extensions (Ctrl+Shift+X) → search **C#** → install the Microsoft extension.

**Alternative: Visual Studio 2022 Community** (Windows only, heavier)
- https://visualstudio.microsoft.com/vs/community/
- During install, select the **".NET desktop development"** workload.

---

## 2. Clone the Repository

Clone the MenaceModkitSDK repository to your machine:

```
git clone https://github.com/pylkij/MenaceModkitSDK.git
cd MenaceModkitSDK
```

This creates a `MenaceModkitSDK/` folder with all the source files. All subsequent steps happen inside it unless stated otherwise.

---

## 3. Gather the External Dependencies

The project references DLLs from MelonLoader and from the game itself. These cannot be included in the repository for licensing reasons, so you need to supply them from your own game installation.

The folders to place them in already exist in the repo:

```
third_party/MelonLoader/
third_party/GameAssemblies/
```

### 3.1 — MelonLoader DLLs

You need version **0.7.2** to match the project.

**If MelonLoader is already installed on your game:** navigate to `<Your Game Folder>/MelonLoader/` and copy these four files into `third_party/MelonLoader/`:

- `MelonLoader.dll`
- `0Harmony.dll`
- `Il2CppInterop.Runtime.dll`
- `Il2CppInterop.Common.dll`

**If not yet installed:** download from https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.2, run the installer pointed at your game, then copy the four files above.

### 3.2 — Game Assembly DLLs

MelonLoader generates these the first time it runs. Launch your game with MelonLoader installed, let it reach the main menu, then quit. Look for:

```
<Your Game Folder>/MelonLoader/Il2CppAssemblies/
```

Copy all of the following into `third_party/GameAssemblies/`:

- `UnityEngine.CoreModule.dll`
- `UnityEngine.ImageConversionModule.dll`
- `Il2Cppmscorlib.dll`
- `UnityEngine.AssetBundleModule.dll`
- `UnityEngine.AudioModule.dll`
- `UnityEngine.AnimationModule.dll`
- `UnityEngine.IMGUIModule.dll`
- `UnityEngine.InputLegacyModule.dll`
- `UnityEngine.TextRenderingModule.dll`
- `Assembly-CSharp.dll`
- `UnityEngine.UIElementsModule.dll`

> **Why these?** The mod loader compiles against the game's own types. Without them, the compiler has no knowledge of the game's classes and methods.

---

## 4. Verify Your Folder Structure

Before building, confirm the repo looks like this:

```
MenaceModkitSDK/
│
├── Directory.Build.props
│
├── Menace.ModpackLoader/
│   ├── Directory.Build.props
│   ├── Menace.ModpackLoader.csproj
│   └── (all .cs source files)
│
├── Shared/
│   ├── GenerateVersion.targets
│   └── ModkitVersion.cs
│
└── third_party/
    ├── versions.json
    ├── MelonLoader/
    │   ├── MelonLoader.dll
    │   ├── 0Harmony.dll
    │   ├── Il2CppInterop.Runtime.dll
    │   └── Il2CppInterop.Common.dll
    └── GameAssemblies/
        ├── Assembly-CSharp.dll
        ├── Il2Cppmscorlib.dll
        ├── UnityEngine.CoreModule.dll
        └── (remaining Unity DLLs...)
```

**Pre-build checklist:**
- [ ] All four MelonLoader DLLs are in `third_party/MelonLoader/`
- [ ] All eleven game assembly DLLs are in `third_party/GameAssemblies/`
- [ ] `third_party/versions.json` is present (it should already be; do not delete it)

---

## 5. Build the DLL

Navigate into the project directory and build:

```
cd Menace.ModpackLoader
dotnet build
```

### Successful output looks like

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Generated ModkitVersion.cs (v35 from versions.json)
```

Your DLL will be at:
```
Menace.ModpackLoader/bin/Debug/net6.0/Menace.ModpackLoader.dll
```

### Release build (for deployment)

```
dotnet build --configuration Release
```

Output at:
```
Menace.ModpackLoader/bin/Release/net6.0/Menace.ModpackLoader.dll
```

---

## 6. Troubleshooting Common Errors

**"Could not find file '...versions.json'"**
The file is missing or has been accidentally deleted. It should already be present in the repo at `third_party/versions.json`. Check the filename has an `s` (`versions`, not `version`). If it's gone, restore it with `git checkout third_party/versions.json`.

**"Could not extract version from versions.json"**
The file exists but parsing failed. Common causes: a trailing comma after the version string, lowercase `"modpackloader"` key (must be `"ModpackLoader"`), or extra content that broke the JSON structure. Restore the original with `git checkout third_party/versions.json`.

**"The referenced component '...' could not be found" — MelonLoader DLLs**
A DLL is missing from `third_party/MelonLoader/`. On Linux filenames are case-sensitive — confirm `0Harmony.dll` starts with the digit `0` not the letter `O`, and `MelonLoader.dll` has capital M and L.

**"The referenced component '...' could not be found" — Game Assembly DLLs**
A DLL is missing from `third_party/GameAssemblies/`. If the `Il2CppAssemblies/` folder looks incomplete, MelonLoader may not have finished its first-run generation — launch the game, let it reach the main menu, quit, then check again.

**"error CS0234: The type or namespace name '...' does not exist"**
A source file references a type the compiler can't find. This usually means game assembly DLLs from the wrong game version. Make sure you're copying from the correct game installation and that MelonLoader has fully processed it.

**"Could not import project '..\Shared\GenerateVersion.targets'"**
The `Shared/` directory is missing or has been moved. It must be at the same level as `Menace.ModpackLoader/`. Restore it with `git checkout Shared/`.

**Build succeeds with a warning about `InternalsVisibleTo`**
Harmless. The `.csproj` grants internal access to a test project. It has no effect on the built DLL.

---

## 7. Making and Testing Your Changes

### Editing source files

All C# source files are inside `Menace.ModpackLoader/`. Open the repo root in VS Code:
```
code .
```

Make your changes, then rebuild with `dotnet build` from inside `Menace.ModpackLoader/`. The compiler reports errors with exact filenames and line numbers.

### Pulling upstream updates

To incorporate new changes from the repository:

```
git pull
```

Then rebuild. If new game assembly DLLs are required after an upstream update, the build will tell you which are missing.

### Saving your own changes with Git

If you're maintaining a personal set of modifications, commit them after each logical change:

```
git add .
git commit -m "Short description of what you changed"
```

Write specific commit messages — e.g. `"Fix null reference in ModpackManager.LoadAsync"` rather than `"fix stuff"`. You will thank yourself when tracking down a bug introduced three weeks ago.

---

## 8. Deploying Your DLL

1. Build for release from inside `Menace.ModpackLoader/`:
   ```
   dotnet build --configuration Release
   ```

2. Your DLL is at:
   ```
   Menace.ModpackLoader/bin/Release/net6.0/Menace.ModpackLoader.dll
   ```

3. Find the installed DLL in your game folder — typically somewhere like:
   ```
   <Game Folder>/Mods/ModpackLoader/Menace.ModpackLoader.dll
   ```

4. **Back up the original first:**
   ```
   # Mac/Linux
   cp Menace.ModpackLoader.dll Menace.ModpackLoader.dll.backup

   # Windows
   copy Menace.ModpackLoader.dll Menace.ModpackLoader.dll.backup
   ```

5. Copy your built DLL into that location.

6. Launch the game and watch the MelonLoader console (the terminal window that appears at startup) for any loading errors.

> **Rolling back:** If something goes wrong, rename `Menace.ModpackLoader.dll.backup` to `Menace.ModpackLoader.dll` to instantly restore the previous version.

---

## Summary

Cloning MenaceModkitSDK gives you everything needed to build except the game-specific DLLs, which cannot be redistributed. Once you've dropped those into `third_party/`, the project builds to a drop-in DLL with a single command. The only ongoing maintenance is keeping `third_party/MelonLoader/` and `third_party/GameAssemblies/` current if the game or MelonLoader updates — just copy fresh DLLs from your game installation and rebuild.
