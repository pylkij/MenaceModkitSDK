# Your First Mod

```csharp
using System;

using MelonLoader;
using HarmonyLib;

using Menace.ModpackLoader;
using Menace.SDK;

namespace YourBasicMod;

public class Plugin: IModpackPlugin
{
    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    { 
    	SdkLogger.Msg("Congratulations one successfully writing your first mod!");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName) { }
    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```