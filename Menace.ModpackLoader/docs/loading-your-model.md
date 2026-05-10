# Loading Your Model Into the Game

A guide to getting a custom 3D model working with the MenaceModKit.

---

## Before You Start

You'll need a 3D model exported in the **GLB or GLTF format**. These are the only formats the mod kit understands. If your model is in a different format (like FBX or OBJ), you'll need to convert it first — Blender can do this for free.

> **Tip:** If you made your model in Blender, export it using **File → Export → glTF 2.0** and choose the `.glb` format. This bundles everything (textures, materials, etc.) into one file.

---

## Step 1: Set Up Your Modpack Folder

Your mod needs to live in a specific folder structure. The model loader looks for a folder called `models` inside your modpack directory. It won't find your file if it's anywhere else.

Your folder should look like this:

```
YourModpackName/
└── models/
    └── your_model.glb
```

You can have multiple models — just drop them all in the `models` folder. Subfolders are fine too:

```
YourModpackName/
└── models/
    ├── rifle.glb
    └── characters/
        └── enemy_scout.glb
```
---

## Step 2: Name Your Model Carefully

The game will register your model under the **filename** (without the `.glb` extension). This name is how other parts of your mod will refer to the model.

For example, a file called `enemy_scout.glb` will be registered as `enemy_scout`.

A few things to keep in mind:

- Keep the name simple and unique — if two mods have a file with the same name, they may conflict.
- Avoid spaces and special characters. Underscores work well.
- The name is **not** case-sensitive when looking up the model later, so `Enemy_Scout` and `enemy_scout` point to the same thing.

---

## Step 3: Check Your Model Has Geometry

The loader will silently skip any GLB file that doesn't contain actual mesh data (i.e. visible 3D geometry). If your model loads without errors but doesn't appear in-game, open it in Blender or a GLTF viewer and confirm it has visible geometry — not just an armature or empty object.

---

## What the Game Does With Your Model

Once your modpack is loaded, the game automatically:

1. Scans the `models` folder for any `.glb` or `.gltf` files.
2. Loads the geometry, materials, and textures from each file.
3. Registers the model under its filename so it can be spawned or attached in-game.

You don't need to write any code to make this happen. Just having the file in the right folder is enough.

---

## Material & Texture Support

The loader reads the following from your GLB file automatically:

- **Base color** (your main texture or flat color)
- **Normal maps** (for surface detail)
- **Metallic and roughness values**
- **Emission** (glow effects)

If your model has no material defined, the game will apply a default material automatically so it still appears — it just won't look the way you intended.

> **Tip:** Bake your textures and embed them in the GLB before exporting. This ensures everything travels with the file and nothing gets left behind.

---

## Skinned Models (With Bones/Animation)

If your model has a skeleton (an armature with bones), the loader supports that too. Make sure your skeleton is included in the GLB export. The game will detect it automatically and set up the bone hierarchy.

If bone setup fails for some reason, the game will log a warning but still load the mesh — it just won't animate correctly.

---

## Troubleshooting

**My model doesn't appear at all.**
- Make sure the file is inside a folder named exactly `models`.
- Open the game's log file and search for your model's filename — any load errors will be listed there.

**My model appears but looks wrong (wrong colors, pink/error material).**
- The game couldn't find a compatible shader. This is usually fine and resolves itself depending on your graphics settings, but double-check your material setup in your export.

**My model appears but faces are flipped (inside-out).**
- This is a coordinate system difference between Blender and the game engine. In Blender, make sure you apply all transforms (**Object → Apply → All Transforms**) before exporting.

**My model appears but is rotated 90 degrees.**
- This is a known quirk of Blender exports. The game automatically corrects for the standard Blender weapon/object rotation, so your model should come out oriented correctly. If it doesn't, try adjusting the rotation in Blender before exporting.

**The game logs say "did not create any meshes."**
- Your GLB file has no geometry the loader could read. Make sure you're exporting meshes, not just an empty scene or armature.

---

## Quick Checklist

Before loading the game with your new model, run through this list:

- [ ] Model is exported as `.glb` or `.gltf`
- [ ] File is placed in `YourModpackName/models/`
- [ ] Filename is simple, unique, and has no spaces
- [ ] Textures are embedded in the GLB (not separate files)
- [ ] You've applied all transforms in Blender before exporting

If all of the above are true and your model still isn't loading, check the game log for an error message next to your file's name — it will usually tell you exactly what went wrong.
