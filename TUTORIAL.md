# Forking Menace.ModpackLoader — Complete Setup Tutorial

**Who this is for:** programmers comfortable editing files but new to C# build tooling.
**What you end up with:** a minimal repo that produces a drop-in `Menace.ModpackLoader.dll` with one command.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Get the Original Source Code](#2-get-the-original-source-code)
3. [Create Your Fork Repository](#3-create-your-fork-repository)
4. [Copy the Required Files](#4-copy-the-required-files)
5. [Gather the External Dependencies](#5-gather-the-external-dependencies)
6. [Edit the Project Files](#6-edit-the-project-files)
7. [Create the Minimal versions.json](#7-create-the-minimal-versionsjson)
8. [Verify Your Folder Structure](#8-verify-your-folder-structure)
9. [Build the DLL](#9-build-the-dll)
10. [Troubleshooting Common Errors](#10-troubleshooting-common-errors)
11. [Making and Testing Your Changes](#11-making-and-testing-your-changes)
12. [Deploying Your DLL](#12-deploying-your-dll)

---

## 1. Prerequisites

You need three tools installed before anything else. All are free.

### 1.1 — Git

Git is used to download source code and track your changes.

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

## 2. Get the Original Source Code

Clone the original repo so you have the source files to copy from. Do **not** try to recreate any files manually.

```
git clone https://github.com/ORIGINAL-OWNER/ORIGINAL-REPO.git original-menace
```

> Replace the URL with the actual Menace repository URL — click the green **Code** button on GitHub and copy the HTTPS link.

This creates an `original-menace/` folder. You will not build it directly; it's just a source to copy from.

---

## 3. Create Your Fork Repository

```
mkdir my-modpackloader-fork
cd my-modpackloader-fork
git init
```

All subsequent steps happen inside `my-modpackloader-fork/` unless stated otherwise.

---

## 4. Copy the Required Files

You need exactly three things from the original project.

### 4.1 — The project directory

**Mac/Linux:**
```
cp -r ../original-menace/Menace.ModpackLoader ./Menace.ModpackLoader
```
**Windows:**
```
xcopy /E /I ..\original-menace\Menace.ModpackLoader Menace.ModpackLoader
```

### 4.2 — The Shared directory

**Mac/Linux:**
```
cp -r ../original-menace/Shared ./Shared
```
**Windows:**
```
xcopy /E /I ..\original-menace\Shared Shared
```

### 4.3 — The root Directory.Build.props

The child `Directory.Build.props` inside `Menace.ModpackLoader/` imports a **parent** one from the repo root. You need that parent.

**Mac/Linux:**
```
cp ../original-menace/Directory.Build.props ./Directory.Build.props
```
**Windows:**
```
copy ..\original-menace\Directory.Build.props Directory.Build.props
```

> **What is this file?** MSBuild automatically searches up the folder tree for `Directory.Build.props` files. The one you place at your repo root satisfies the import in the child project with no code changes needed.

---

## 5. Gather the External Dependencies

The project references DLLs from MelonLoader and from the game itself. Create the folders to hold them:

**Mac/Linux:**
```
mkdir -p third_party/MelonLoader
mkdir -p third_party/GameAssemblies
```
**Windows:**
```
mkdir third_party\MelonLoader
mkdir third_party\GameAssemblies
```

### 5.1 — MelonLoader DLLs

You need version **0.7.2** to match the project.

**If MelonLoader is already installed on your game:** navigate to `<Your Game Folder>/MelonLoader/` and copy these four files into `third_party/MelonLoader/`:

- `MelonLoader.dll`
- `0Harmony.dll`
- `Il2CppInterop.Runtime.dll`
- `Il2CppInterop.Common.dll`

**If not yet installed:** download from https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.2, run the installer pointed at your game, then copy the four files above.

### 5.2 — Game Assembly DLLs

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

## 6. Edit the Project Files

One block needs to be removed from the `.csproj` — a post-build step that only makes sense inside the original full repository.

Open `Menace.ModpackLoader/Menace.ModpackLoader.csproj` in your editor and find this block near the bottom:

```xml
<!-- Auto-sync built DLL to bundled directory after successful build -->
<Target Name="SyncToBundled" AfterTargets="Build">
  <PropertyGroup>
    <BundledDestination>$(MSBuildThisFileDirectory)..\..\third_party\bundled\ModpackLoader\</BundledDestination>
  </PropertyGroup>
  <Copy
    SourceFiles="$(TargetPath)"
    DestinationFolder="$(BundledDestination)"
    SkipUnchangedFiles="false" />
  <Message Importance="high" Text="✓ Synced $(TargetFileName) to bundled directory" />
</Target>
```

Delete this entire block from the opening comment through the closing `</Target>` tag. Save the file.

> **Why?** This step tries to copy your DLL to `third_party/bundled/` — a folder used by the original repo's release packaging. That folder doesn't exist in your fork, causing the build to fail even after a successful compile.

---

## 7. Create the Minimal versions.json

The build reads a `versions.json` to stamp version numbers into the compiled code. You only need the one entry it actually uses.

Create `third_party/versions.json` with exactly this content:

```json
{
  "ModpackLoader": {
    "version": "35.0.0"
  }
}
```

> **Customising:** This value gets compiled into constants like `BuildNumber` and `LoaderFull` inside the DLL. To make your fork's builds distinguishable, consider `"35.0.0-fork"` — it will appear in log output and version checks.

---

## 8. Verify Your Folder Structure

```
my-modpackloader-fork/
│
├── Directory.Build.props                  ← copied from original repo root
│
├── Menace.ModpackLoader/
│   ├── Directory.Build.props              ← already present in the source copy
│   ├── Menace.ModpackLoader.csproj        ← edited: SyncToBundled removed
│   └── (all .cs source files)
│
├── Shared/
│   ├── GenerateVersion.targets
│   └── ModkitVersion.cs
│
└── third_party/
    ├── versions.json                      ← 5-line file you created
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
- [ ] `Directory.Build.props` exists at the repo root (not only inside `Menace.ModpackLoader/`)
- [ ] `Menace.ModpackLoader.csproj` no longer contains the `SyncToBundled` target
- [ ] All four MelonLoader DLLs are in `third_party/MelonLoader/`
- [ ] All eleven game assembly DLLs are in `third_party/GameAssemblies/`
- [ ] `third_party/versions.json` exists with a `ModpackLoader.version` entry

---

## 9. Build the DLL

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

## 10. Troubleshooting Common Errors

**"Could not find file '...versions.json'"**
The file is missing or misplaced. It must be at `third_party/versions.json` relative to your repo root — not inside `Menace.ModpackLoader/`. Also check the filename has an `s` (`versions`, not `version`).

**"Could not extract version from versions.json"**
The file exists but parsing failed. Common causes: a trailing comma after the version string, lowercase `"modpackloader"` key (must be `"ModpackLoader"`), or extra content that broke the JSON structure.

**"The referenced component '...' could not be found" — MelonLoader DLLs**
A DLL is missing from `third_party/MelonLoader/`. On Linux filenames are case-sensitive — confirm `0Harmony.dll` starts with the digit `0` not the letter `O`, and `MelonLoader.dll` has capital M and L.

**"The referenced component '...' could not be found" — Game Assembly DLLs**
A DLL is missing from `third_party/GameAssemblies/`. If the `Il2CppAssemblies/` folder looks incomplete, MelonLoader may not have finished its first-run generation — launch the game, let it reach the main menu, quit, then check again.

**"error CS0234: The type or namespace name '...' does not exist"**
A source file references a type the compiler can't find. This usually means game assembly DLLs from the wrong game version. Make sure you're copying from the correct game installation and that MelonLoader has fully processed it.

**"Could not import project '..\Shared\GenerateVersion.targets'"**
The `Shared/` directory is missing or in the wrong location. It must be at the same level as `Menace.ModpackLoader/`, not nested inside it.

**Build succeeds with a warning about `InternalsVisibleTo`**
Harmless. The `.csproj` grants internal access to a test project you didn't copy. It has no effect on the built DLL.

---

## 11. Making and Testing Your Changes

### Editing source files

All C# source files are inside `Menace.ModpackLoader/`. Open the repo root in VS Code:
```
code .
```

Make your changes, then rebuild with `dotnet build` from inside `Menace.ModpackLoader/`. The compiler reports errors with exact filenames and line numbers.

### Saving your work with Git

From your repo root:
```
git add .
git commit -m "Short description of what you changed"
```

Write specific commit messages — e.g. `"Fix null reference in ModpackManager.LoadAsync"` rather than `"fix stuff"`. You will thank yourself when tracking down a bug introduced three weeks ago.

### Keep a changelog

Create a `CHANGES.md` at your repo root listing what you changed from the original and why. When the upstream project updates and you need to re-apply your fixes to new source code, this file is your roadmap.

---

## 12. Deploying Your DLL

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

> **Rolling back:** If something goes wrong, rename `Menace.ModpackLoader.dll.backup` to `Menace.ModpackLoader.dll` to instantly restore the original.

---

## Summary

Your fork requires no other part of the original project to build, produces a drop-in DLL with a single command, and is tracked in its own Git repository. The only ongoing maintenance is keeping `third_party/MelonLoader/` and `third_party/GameAssemblies/` current if the game or MelonLoader updates — just copy fresh DLLs from your game installation and rebuild.