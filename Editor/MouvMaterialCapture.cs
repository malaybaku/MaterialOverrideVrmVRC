using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Baku.MaterialOverridableVrm;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Baku.MaterialOverrideVrmVRC
{
    public static class MouvMaterialCapture
    {
        public static string Normalize(string name)
        {
            while (name.EndsWith("(Clone)", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 7).TrimEnd();
            return name;
        }

        public const string SnapshotRoot = "Assets/MaterialOverrideVrm/Generated/MaterialSnapshots";
        internal const string WorkRoot = "Assets/MaterialOverrideVrm/Generated/MaterialSnapshotWork";

        public static string GetSnapshotFolder(string avatarName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safeName = new string(avatarName.Where(c => !invalid.Contains(c) && c != '/' && c != '\\').Take(48).ToArray()).Trim(' ', '.');
            if (string.IsNullOrEmpty(safeName)) safeName = "Avatar";
            using (var hash = System.Security.Cryptography.SHA256.Create())
            {
                var suffix = BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(avatarName))).Replace("-", "").Substring(0, 12).ToLowerInvariant();
                return SnapshotRoot + "/" + safeName + "_" + suffix;
            }
        }

        static void MoveFolder(string from, string to)
        {
            var error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
        }

        static void CreateSavedAsset(Object asset, string path)
        {
            AssetDatabase.CreateAsset(asset, path);
            if (AssetDatabase.GetAssetPath(asset) != path || !File.Exists(path))
                throw new IOException("Could not save capture asset: " + path);
        }

        public static event Action<MaterialSnapshot> Captured;

        public static MaterialSnapshot Capture(GameObject root, GameObject sourceAvatar = null, string temporaryFolder = null)
        {
            if (temporaryFolder != null && (!temporaryFolder.StartsWith(WorkRoot + "/Temporary_", StringComparison.Ordinal) ||
                !Guid.TryParseExact(temporaryFolder.Substring((WorkRoot + "/Temporary_").Length), "N", out _) ||
                Directory.Exists(temporaryFolder) || File.Exists(temporaryFolder + ".meta")))
                throw new ArgumentException("A new unique temporary capture folder is required.", nameof(temporaryFolder));
            Directory.CreateDirectory(SnapshotRoot);
            Directory.CreateDirectory(WorkRoot);
            AssetDatabase.Refresh();
            var capturedUtc = DateTime.UtcNow;
            var captureId = Guid.NewGuid().ToString("N");
            var avatarName = sourceAvatar ? sourceAvatar.name : Normalize(root.name);
            var destination = temporaryFolder ?? GetSnapshotFolder(avatarName);
            var folder = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(WorkRoot, "Pending_" + captureId));
            if (string.IsNullOrEmpty(folder)) throw new IOException("Could not create material capture folder.");
            var materials = new Dictionary<Material, Material>();
            var textures = new Dictionary<Texture, Texture>();
            var entries = new List<MaterialSnapshot.Entry>();
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                    var slots = renderer.sharedMaterials;
                    for (int slot = 0; slot < slots.Length; slot++)
                    {
                        var original = slots[slot];
                        if (!original) continue;
                        if (!materials.TryGetValue(original, out var saved))
                        {
                            saved = new Material(original) { name = Normalize(original.name) };

                            materials.Add(original, saved);
                            // Also remap saved texture properties no longer exposed by the current shader.
                            var serialized = new SerializedObject(saved);
                            var property = serialized.GetIterator();
                            while (property.Next(true))
                            {
                                if (property.propertyType != SerializedPropertyType.ObjectReference || !(property.objectReferenceValue is Texture texture)) continue;
                                if (!textures.TryGetValue(texture, out var copy))
                                {
                                    copy = CopyTexture(texture);
                                    copy.name = texture.name;
                                    copy.hideFlags = HideFlags.None;
                                    CreateSavedAsset(copy, folder + "/Texture_" + textures.Count + ".asset");
                                    textures.Add(texture, copy);
                                }
                                property.objectReferenceValue = copy;
                            }
                            serialized.ApplyModifiedPropertiesWithoutUndo();
                            CreateSavedAsset(saved, folder + "/Material_" + (materials.Count - 1) + ".mat");
                        }
                        entries.Add(new MaterialSnapshot.Entry {
                            RendererPath = AnimationUtility.CalculateTransformPath(renderer.transform, root.transform),
                            RendererType = renderer.GetType().FullName, Slot = slot,
                            OriginalMaterialName = Normalize(original.name), Material = saved });
                    }
                }
                var snapshot = ScriptableObject.CreateInstance<MaterialSnapshot>();
                snapshot.name = avatarName;
                snapshot.AvatarName = avatarName;
                snapshot.CapturedUtc = capturedUtc.ToString("O");
                snapshot.CaptureId = captureId;
                snapshot.SetSourceAvatar(sourceAvatar);
                snapshot.Entries = entries.ToArray();
                CreateSavedAsset(snapshot, folder + "/Snapshot.asset");
                AssetDatabase.SaveAssets();
                // Finish all new assets before touching the previous capture.
                foreach (var assetPath in AssetDatabase.GetDependencies(folder + "/Snapshot.asset", true)
                    .Where(p => p.StartsWith(folder + "/", StringComparison.Ordinal)))
                    if (!File.Exists(assetPath) || new FileInfo(assetPath).Length == 0)
                        throw new IOException("Capture asset was not saved: " + assetPath);
                var backup = WorkRoot + "/Backup_" + captureId;
                var hadPrevious = AssetDatabase.IsValidFolder(destination);
                if (hadPrevious) MoveFolder(destination, backup);
                try { MoveFolder(folder, destination); }
                catch
                {
                    if (hadPrevious) MoveFolder(backup, destination);
                    throw;
                }
                // The new capture is committed. A cleanup failure must not roll it back.
                if (hadPrevious && !AssetDatabase.DeleteAsset(backup))
                    Debug.LogWarning("Old capture backup could not be removed: " + backup);
                snapshot = AssetDatabase.LoadAssetAtPath<MaterialSnapshot>(destination + "/Snapshot.asset");
                Debug.Log("Material information saved to folder: " + destination, AssetDatabase.LoadAssetAtPath<Object>(destination));
                // UI observers must not make a successful capture fail or delete its assets.
                if (Captured != null)
                    foreach (Action<MaterialSnapshot> observer in Captured.GetInvocationList())
                        try { observer(snapshot); } catch (Exception exception) { Debug.LogException(exception); }
                return snapshot;
            }
            catch { if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder); throw; }
        }

        static Texture CopyTexture(Texture texture)
        {
            if (texture is RenderTexture render)
            {
                if (render.dimension != UnityEngine.Rendering.TextureDimension.Tex2D)
                    throw new NotSupportedException("Only 2D RenderTextures can be captured: " + render.name);
                var previous = RenderTexture.active;
                var previousSrgbWrite = GL.sRGBWrite;
                var temporary = RenderTexture.GetTemporary(render.width, render.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                try
                {
                    GL.sRGBWrite = false;
                    Graphics.Blit(render, temporary);
                    RenderTexture.active = temporary;
                    var result = new Texture2D(render.width, render.height, TextureFormat.RGBAFloat, false, true);
                    result.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
                    result.Apply();
                    result.wrapMode = render.wrapMode;
                    result.filterMode = render.filterMode;
                    return result;
                }
                finally { RenderTexture.active = previous; GL.sRGBWrite = previousSrgbWrite; RenderTexture.ReleaseTemporary(temporary); }
            }
            if (!(texture is Texture2D) && !(texture is Cubemap) && !(texture is Texture2DArray) && !(texture is Texture3D) && !(texture is CubemapArray))
                throw new NotSupportedException("Unsupported texture: " + texture.GetType().Name);
            return Object.Instantiate(texture);
        }

        public static void Apply(MaterialSnapshot snapshot, MaterialOverrideSource source)
        {
            if (!snapshot || !source) throw new ArgumentNullException();
            // UniVRM may put the exported avatar under a wrapper alongside nodes without meshes.
            var avatarRoot = snapshot.Entries.Any(e => e.UseVrmMaterial)
                ? source.transform.Cast<Transform>().FirstOrDefault(child =>
                    source.RendererBindings.All(b => b.Renderer.transform.IsChildOf(child)))
                : null;
            int unresolved = 0;
            foreach (var binding in source.RendererBindings)
            {
                var path = AnimationUtility.CalculateTransformPath(binding.Renderer.transform, source.transform);
                var named = binding.TargetMaterial
                    ? snapshot.Entries.Where(e => e.OriginalMaterialName == Normalize(binding.TargetMaterial.name)).ToArray()
                    : Array.Empty<MaterialSnapshot.Entry>();
                var sameSlot = named.Where(e => e.RendererType == binding.Renderer.GetType().FullName && e.Slot == binding.MaterialSlot).ToArray();
                var exact = sameSlot.Where(e => e.RendererPath == path).ToArray();
                var hasNamedOptOut = named.Any(e => e.UseVrmMaterial);
                if (exact.Length == 0 && hasNamedOptOut && avatarRoot)
                {
                    var avatarPath = AnimationUtility.CalculateTransformPath(binding.Renderer.transform, avatarRoot);
                    exact = sameSlot.Where(e => e.RendererPath == avatarPath).ToArray();
                }
                if (exact.Length == 1 && exact[0].UseVrmMaterial)
                {
                    binding.ReplacementMaterial = null;
                    continue;
                }
                Material candidate = exact.Length == 1 ? exact[0].Material : null;
                // A name match alone cannot identify which slot opted out.
                if (!candidate && (exact.Length == 1 || !hasNamedOptOut))
                {
                    var materials = named.Where(e => !e.UseVrmMaterial).Select(e => e.Material).Where(m => m).Distinct().ToArray();
                    if (materials.Length == 1) candidate = materials[0];
                }
                binding.ReplacementMaterial = candidate;
                if (!candidate) unresolved++;
            }
            EditorUtility.SetDirty(source);
            if (unresolved != 0) throw new InvalidOperationException($"保存済み対応表で{unresolved}スロットを解決できません。Inspectorで修正してください。");
        }
    }
}
