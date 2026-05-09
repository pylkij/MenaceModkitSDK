using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Menace.SDK;

using SharpGLTF.Schema2;

using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Color = UnityEngine.Color;
using Matrix4x4 = UnityEngine.Matrix4x4;
using UnityMesh = UnityEngine.Mesh;
using UnityMaterial = UnityEngine.Material;
using GltfMesh = SharpGLTF.Schema2.Mesh;
using GltfMaterial = SharpGLTF.Schema2.Material;

namespace Menace.ModpackLoader;

public static class GlbLoader
{
    private static readonly MelonLogger.Instance _log = new("GlbLoader");
    private static readonly string[] ModelSearchPatterns = { "*.glb", "*.gltf" };
    private static readonly string[] ShaderNames =
    {
        "HDRP/Lit",
        "HDRenderPipeline/Lit",
        "HD/Lit",
        "Shader Graphs/Lit",
        "Universal Render Pipeline/Lit",
        "Standard",
        "Diffuse",
        "Unlit/Color"
    };

    private const string SETTINGS_NAME = "Modpack Loader";
    private const string SETTING_KEY_ENABLED = "GlbLoader";

    private static bool _enabled = true;
    private static bool _initialized = false;
    private static Shader _resolvedShader;

    public static bool IsEnabled => _enabled;

    public static void Initialize()
    {
        if (_initialized) return;

        _enabled = ModSettings.Get<bool>(SETTINGS_NAME, SETTING_KEY_ENABLED);
        _initialized = true;

        if (!_enabled)
        {
            _log.Msg("GLB loading disabled (enable in Settings > Modpack Loader)");
        }
        else
        {
            _log.Msg("GLB loading enabled");
        }
    }

    public class LoadedModel
    {
        public string Name { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public List<UnityMesh> Meshes { get; set; } = new();
        public List<UnityMaterial> Materials { get; set; } = new();
        public List<Texture2D> Textures { get; set; } = new();
        public GameObject RootPrefab { get; set; }
    }

    private static readonly List<LoadedModel> _loadedModels = new();
    private static readonly Dictionary<string, LoadedModel> _loadedModelsByPath = new(StringComparer.OrdinalIgnoreCase);
    public static int LoadedCount => _loadedModels.Count;

    public static void LoadModpackModels(string modpackPath, string modpackName = null)
    {
        if (!_enabled)
        {
            return;
        }

        var modelsDir = Path.Combine(modpackPath, "models");
        if (!Directory.Exists(modelsDir))
            return;

        var modelFiles = ModelSearchPatterns
            .SelectMany(pattern => Directory.EnumerateFiles(modelsDir, pattern, SearchOption.AllDirectories))
            .OrderBy(path => path);

        foreach (var glbPath in modelFiles)
        {
            try
            {
                LoadAndRegisterGlb(glbPath, modpackName ?? Path.GetFileName(modpackPath));
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to load GLB {Path.GetFileName(glbPath)}: {ex.Message}");
            }
        }
    }

    public static LoadedModel LoadAndRegisterGlb(string glbPath, string modpackName = "Runtime")
    {
        if (!_enabled)
            return null;

        if (string.IsNullOrWhiteSpace(glbPath))
        {
            _log.Warning("GLB file path was empty.");
            return null;
        }

        var fullPath = Path.GetFullPath(glbPath);
        if (_loadedModelsByPath.TryGetValue(fullPath, out var cached))
            return cached;

        var model = LoadGlb(fullPath);
        if (model == null)
            return null;

        if (model.RootPrefab == null)
        {
            _log.Warning($"  RootPrefab is null for {model.Name}, cannot register");
            return null;
        }

        modpackName = string.IsNullOrWhiteSpace(modpackName) ? "Runtime" : modpackName;

        // Avoid registering child meshes/materials by generic names such as "geometry_0".
        BundleLoader.RegisterAsset(model.Name, model.RootPrefab, "GameObject", modpackName);
        _log.Msg($"  Registered prefab '{model.Name}' as GameObject with BundleLoader");

        _loadedModels.Add(model);
        _loadedModelsByPath[fullPath] = model;

        _log.Msg($"Loaded GLB: {Path.GetFileName(fullPath)} ({model.Meshes.Count} mesh(es), {model.Materials.Count} material(s))");
        return model;
    }

    public static LoadedModel LoadGlb(string glbPath)
    {
        if (string.IsNullOrWhiteSpace(glbPath) || !File.Exists(glbPath))
        {
            _log.Warning($"GLB file not found: {glbPath}");
            return null;
        }

        glbPath = Path.GetFullPath(glbPath);
        var modelRoot = ModelRoot.Load(glbPath);
        var modelName = Path.GetFileNameWithoutExtension(glbPath);

        var result = new LoadedModel { Name = modelName, SourcePath = glbPath };

        var textureMap = new Dictionary<int, Texture2D>();
        foreach (var image in modelRoot.LogicalImages)
        {
            var tex = LoadTexture(image, modelName);
            if (tex != null)
            {
                textureMap[image.LogicalIndex] = tex;
                result.Textures.Add(tex);
            }
        }

        var materialMap = new Dictionary<int, UnityMaterial>();
        foreach (var gltfMat in modelRoot.LogicalMaterials)
        {
            var mat = CreateMaterial(gltfMat, textureMap, modelName);
            materialMap[gltfMat.LogicalIndex] = mat;
            result.Materials.Add(mat);
        }

        if (materialMap.Count == 0)
        {
            var defaultMat = new UnityMaterial(ResolveShader());
            defaultMat.name = $"{modelName}_DefaultMaterial";
            defaultMat.hideFlags = HideFlags.DontUnloadUnusedAsset;
            materialMap[-1] = defaultMat;
            result.Materials.Add(defaultMat);
        }

        var holder = new GameObject($"{modelName}_Holder");
        UnityEngine.Object.DontDestroyOnLoad(holder);
        holder.hideFlags = HideFlags.DontUnloadUnusedAsset;
        holder.transform.position = new Vector3(0, -10000, 0);

        var rootGO = new GameObject(modelName);
        rootGO.transform.SetParent(holder.transform, false);
        rootGO.hideFlags = HideFlags.DontUnloadUnusedAsset;

        var modelContainer = new GameObject("ModelContainer");
        modelContainer.transform.SetParent(rootGO.transform, false);
        modelContainer.hideFlags = HideFlags.DontUnloadUnusedAsset;

        // Keeps the prefab root usable as an attachment point while correcting Blender weapon exports.
        modelContainer.transform.localRotation = Quaternion.Euler(-90f, 0f, -90f);

        _log.Msg($"Created prefab GameObject: {modelName}");

        foreach (var gltfMesh in modelRoot.LogicalMeshes)
        {
            try
            {
                var mesh = CreateMesh(gltfMesh, modelName, out var materialIndices);
                if (mesh != null)
                {
                    result.Meshes.Add(mesh);
                    _log.Msg($"  Created mesh: {mesh.name} ({mesh.vertexCount} vertices)");

                    var meshGO = new GameObject(mesh.name);
                    meshGO.transform.SetParent(modelContainer.transform, false);
                    meshGO.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    var materials = ResolveMaterials(materialIndices, materialMap);

                    var skin = modelRoot.LogicalSkins.FirstOrDefault(s =>
                        modelRoot.LogicalNodes.Any(n => n.Mesh == gltfMesh && n.Skin == s));

                    if (skin != null)
                    {
                        _log.Msg($"  Setting up skinned mesh with {skin.JointsCount} bones");
                        var smr = meshGO.AddComponent<SkinnedMeshRenderer>();
                        smr.sharedMesh = mesh;
                        ApplyMaterials(smr, materials);

                        try
                        {
                            SetupBones(smr, skin, modelRoot);
                        }
                        catch (Exception boneEx)
                        {
                            _log.Warning($"  Bone setup failed: {boneEx.Message}");
                        }
                    }
                    else
                    {
                        var mf = meshGO.AddComponent<MeshFilter>();
                        mf.sharedMesh = mesh;

                        var mr = meshGO.AddComponent<MeshRenderer>();
                        ApplyMaterials(mr, materials);
                    }
                }
            }
            catch (Exception meshEx)
            {
                _log.Error($"  Failed to process mesh {gltfMesh.Name}: {meshEx.Message}");
            }
        }

        if (result.Meshes.Count == 0)
        {
            _log.Warning($"GLB '{modelName}' did not create any meshes.");
            return null;
        }

        result.RootPrefab = rootGO;
        return result;
    }

    private static Texture2D LoadTexture(SharpGLTF.Schema2.Image image, string modelName)
    {
        try
        {
            var content = image.Content.Content.ToArray();
            if (content == null || content.Length == 0)
                return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.name = image.Name ?? $"{modelName}_Texture_{image.LogicalIndex}";
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;

            var il2cppBytes = new Il2CppStructArray<byte>(content);
            if (ImageConversion.LoadImage(tex, il2cppBytes))
                return tex;

            _log.Warning($"Failed to decode texture: {tex.name}");
            return null;
        }
        catch (Exception ex)
        {
            _log.Error($"Error loading texture from GLB: {ex.Message}");
            return null;
        }
    }

    private static UnityMaterial CreateMaterial(GltfMaterial gltfMat,
        Dictionary<int, Texture2D> textures, string modelName)
    {
        var mat = new UnityMaterial(ResolveShader());
        mat.name = gltfMat.Name ?? $"{modelName}_Material_{gltfMat.LogicalIndex}";
        mat.hideFlags = HideFlags.DontUnloadUnusedAsset;

        var baseColorChannel = gltfMat.FindChannel("BaseColor");
        if (baseColorChannel.HasValue)
        {
            var color = baseColorChannel.Value.Color;
            mat.color = new Color(color.X, color.Y, color.Z, color.W);

            var tex = baseColorChannel.Value.Texture;
            if (tex?.PrimaryImage != null && textures.TryGetValue(tex.PrimaryImage.LogicalIndex, out var unityTex))
                mat.mainTexture = unityTex;
        }

        var mrChannel = gltfMat.FindChannel("MetallicRoughness");
        if (mrChannel.HasValue)
        {
            foreach (var param in mrChannel.Value.Parameters)
            {
                try
                {
                    if (param.Name == "MetallicFactor" && mat.HasProperty("_Metallic"))
                        mat.SetFloat("_Metallic", Convert.ToSingle(param.Value, CultureInfo.InvariantCulture));
                    else if (param.Name == "RoughnessFactor" && mat.HasProperty("_Glossiness"))
                        mat.SetFloat("_Glossiness", 1.0f - Convert.ToSingle(param.Value, CultureInfo.InvariantCulture));
                }
                catch (Exception ex)
                {
                    _log.Warning($"Failed to read GLB material parameter '{param.Name}': {ex.Message}");
                }
            }
        }

        var normalChannel = gltfMat.FindChannel("Normal");
        if (normalChannel.HasValue)
        {
            var tex = normalChannel.Value.Texture;
            if (tex?.PrimaryImage != null &&
                textures.TryGetValue(tex.PrimaryImage.LogicalIndex, out var normalTex) &&
                mat.HasProperty("_BumpMap"))
                mat.SetTexture("_BumpMap", normalTex);
        }

        var emissiveChannel = gltfMat.FindChannel("Emissive");
        if (emissiveChannel.HasValue)
        {
            var color = emissiveChannel.Value.Color;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(color.X, color.Y, color.Z));
            }
        }

        return mat;
    }

    private static Shader ResolveShader()
    {
        if (_resolvedShader != null)
            return _resolvedShader;

        foreach (var name in ShaderNames)
        {
            var shader = Shader.Find(name);
            if (shader == null)
                continue;

            _resolvedShader = shader;
            _log.Msg($"Using shader: {name}");
            return _resolvedShader;
        }

        _log.Warning("No compatible shader found for GLB materials; Unity will use an error material.");
        return null;
    }

    private static UnityMaterial[] ResolveMaterials(
        IReadOnlyList<int> materialIndices,
        Dictionary<int, UnityMaterial> materialMap)
    {
        var fallback = materialMap.TryGetValue(-1, out var defaultMaterial)
            ? defaultMaterial
            : materialMap.Values.FirstOrDefault();

        if (materialIndices == null || materialIndices.Count == 0)
            return fallback != null ? new[] { fallback } : Array.Empty<UnityMaterial>();

        var materials = new UnityMaterial[materialIndices.Count];
        for (int i = 0; i < materialIndices.Count; i++)
        {
            if (!materialMap.TryGetValue(materialIndices[i], out var material))
                material = fallback;

            materials[i] = material;
        }

        return materials;
    }

    private static void ApplyMaterials(Renderer renderer, UnityMaterial[] materials)
    {
        if (renderer == null || materials == null || materials.Length == 0)
            return;

        if (materials.Length == 1)
            renderer.sharedMaterial = materials[0];
        else
            renderer.sharedMaterials = materials;
    }

    private static UnityMesh CreateMesh(GltfMesh gltfMesh, string modelName, out int[] materialIndices)
    {
        materialIndices = Array.Empty<int>();

        var mesh = new UnityMesh();
        mesh.name = gltfMesh.Name ?? $"{modelName}_Mesh_{gltfMesh.LogicalIndex}";
        mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;

        var allVertices = new List<Vector3>();
        var allNormals = new List<Vector3>();
        var allTangents = new List<Vector4>();
        var allUV0 = new List<Vector2>();
        var allColors = new List<Color>();
        var allIndices = new List<int>();
        var allBoneWeights = new List<BoneWeight>();
        var materialIndexList = new List<int>();
        var subMeshes = new List<(int start, int count)>();

        int vertexOffset = 0;

        foreach (var primitive in gltfMesh.Primitives)
        {
            var positionAccessor = primitive.GetVertexAccessor("POSITION");
            if (positionAccessor == null) continue;

            var positions = positionAccessor.AsVector3Array();
            foreach (var pos in positions)
            {
                allVertices.Add(new Vector3(pos.X, pos.Y, -pos.Z));
            }

            var normalAccessor = primitive.GetVertexAccessor("NORMAL");
            if (normalAccessor != null)
            {
                foreach (var n in normalAccessor.AsVector3Array())
                {
                    allNormals.Add(new Vector3(n.X, n.Y, -n.Z));
                }
            }

            var tangentAccessor = primitive.GetVertexAccessor("TANGENT");
            if (tangentAccessor != null)
            {
                foreach (var t in tangentAccessor.AsVector4Array())
                {
                    allTangents.Add(new Vector4(t.X, t.Y, -t.Z, -t.W));
                }
            }

            var uv0Accessor = primitive.GetVertexAccessor("TEXCOORD_0");
            if (uv0Accessor != null)
            {
                foreach (var uv in uv0Accessor.AsVector2Array())
                {
                    allUV0.Add(new Vector2(uv.X, 1 - uv.Y));
                }
            }

            var colorAccessor = primitive.GetVertexAccessor("COLOR_0");
            if (colorAccessor != null)
            {
                foreach (var c in colorAccessor.AsVector4Array())
                {
                    allColors.Add(new Color(c.X, c.Y, c.Z, c.W));
                }
            }

            var weightsAccessor = primitive.GetVertexAccessor("WEIGHTS_0");
            var jointsAccessor = primitive.GetVertexAccessor("JOINTS_0");
            if (weightsAccessor != null && jointsAccessor != null)
            {
                var weights = weightsAccessor.AsVector4Array();
                var joints = jointsAccessor.AsVector4Array();

                for (int i = 0; i < weights.Count; i++)
                {
                    var bw = new BoneWeight
                    {
                        weight0 = weights[i].X,
                        weight1 = weights[i].Y,
                        weight2 = weights[i].Z,
                        weight3 = weights[i].W,
                        boneIndex0 = (int)joints[i].X,
                        boneIndex1 = (int)joints[i].Y,
                        boneIndex2 = (int)joints[i].Z,
                        boneIndex3 = (int)joints[i].W
                    };
                    allBoneWeights.Add(bw);
                }
            }

            var indexAccessor = primitive.IndexAccessor;
            int indexStart = allIndices.Count;
            if (indexAccessor != null)
            {
                var indices = indexAccessor.AsIndicesArray();
                for (int i = 0; i + 2 < indices.Count; i += 3)
                {
                    allIndices.Add((int)indices[i] + vertexOffset);
                    allIndices.Add((int)indices[i + 2] + vertexOffset);
                    allIndices.Add((int)indices[i + 1] + vertexOffset);
                }
            }
            else
            {
                for (int i = 0; i + 2 < positions.Count; i += 3)
                {
                    allIndices.Add(i + vertexOffset);
                    allIndices.Add(i + 2 + vertexOffset);
                    allIndices.Add(i + 1 + vertexOffset);
                }
            }

            var indexCount = allIndices.Count - indexStart;
            if (indexCount > 0)
            {
                subMeshes.Add((indexStart, indexCount));
                materialIndexList.Add(primitive.Material?.LogicalIndex ?? -1);
            }

            vertexOffset += positions.Count;
        }

        if (allVertices.Count == 0 || allIndices.Count == 0)
            return null;

        if (allVertices.Count > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.vertices = allVertices.ToArray();

        if (allNormals.Count == allVertices.Count)
            mesh.normals = allNormals.ToArray();

        if (allTangents.Count == allVertices.Count)
            mesh.tangents = allTangents.ToArray();

        if (allUV0.Count == allVertices.Count)
            mesh.uv = allUV0.ToArray();

        if (allColors.Count == allVertices.Count)
            mesh.colors = allColors.ToArray();

        mesh.triangles = allIndices.ToArray();

        if (subMeshes.Count > 1)
        {
            mesh.subMeshCount = subMeshes.Count;
            for (int i = 0; i < subMeshes.Count; i++)
            {
                mesh.SetSubMesh(i, new UnityEngine.Rendering.SubMeshDescriptor(
                    subMeshes[i].start, subMeshes[i].count));
            }
        }

        if (allBoneWeights.Count == allVertices.Count)
        {
            mesh.boneWeights = allBoneWeights.ToArray();
        }

        if (allNormals.Count != allVertices.Count)
            mesh.RecalculateNormals();

        mesh.RecalculateBounds();
        materialIndices = materialIndexList.ToArray();

        return mesh;
    }

    private static void SetupBones(SkinnedMeshRenderer smr, Skin skin, ModelRoot model)
    {
        var bones = new Transform[skin.JointsCount];
        var bindPoses = new Matrix4x4[skin.JointsCount];

        var boneRoot = new GameObject("Armature").transform;
        boneRoot.SetParent(smr.transform.parent, false);
        boneRoot.gameObject.hideFlags = HideFlags.DontUnloadUnusedAsset;

        var nodeToTransform = new Dictionary<Node, Transform>();

        for (int i = 0; i < skin.JointsCount; i++)
        {
            var (joint, inverseBindMatrix) = skin.GetJoint(i);

            var boneGO = new GameObject(joint.Name ?? $"Bone_{i}");
            boneGO.hideFlags = HideFlags.DontUnloadUnusedAsset;
            bones[i] = boneGO.transform;
            nodeToTransform[joint] = boneGO.transform;

            bindPoses[i] = new Matrix4x4(
                new Vector4(inverseBindMatrix.M11, inverseBindMatrix.M12, -inverseBindMatrix.M13, inverseBindMatrix.M14),
                new Vector4(inverseBindMatrix.M21, inverseBindMatrix.M22, -inverseBindMatrix.M23, inverseBindMatrix.M24),
                new Vector4(-inverseBindMatrix.M31, -inverseBindMatrix.M32, inverseBindMatrix.M33, -inverseBindMatrix.M34),
                new Vector4(inverseBindMatrix.M41, inverseBindMatrix.M42, -inverseBindMatrix.M43, inverseBindMatrix.M44)
            );
        }

        Transform firstRootBone = null;
        for (int i = 0; i < skin.JointsCount; i++)
        {
            var (joint, _) = skin.GetJoint(i);
            var boneTransform = bones[i];

            var parentNode = joint.VisualParent;

            if (parentNode != null && nodeToTransform.TryGetValue(parentNode, out var parentTransform))
            {
                boneTransform.SetParent(parentTransform, false);
            }
            else
            {
                boneTransform.SetParent(boneRoot, false);
                if (firstRootBone == null)
                {
                    firstRootBone = boneTransform;
                }
            }

            var localTransform = joint.LocalTransform;
            var translation = localTransform.Translation;
            var rotation = localTransform.Rotation;
            var scale = localTransform.Scale;

            boneTransform.localPosition = new Vector3(translation.X, translation.Y, -translation.Z);
            boneTransform.localRotation = new Quaternion(rotation.X, rotation.Y, -rotation.Z, -rotation.W);
            boneTransform.localScale = new Vector3(scale.X, scale.Y, scale.Z);
        }

        smr.bones = bones;
        smr.sharedMesh.bindposes = bindPoses;
        smr.rootBone = firstRootBone ?? boneRoot;

        _log.Msg($"    Bone hierarchy set up with {skin.JointsCount} bones, root: {smr.rootBone.name}");
    }
}
