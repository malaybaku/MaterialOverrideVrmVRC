using System;
using System.Linq;
using Baku.MaterialOverridableVrm;
using Baku.MaterialOverridableVrm.Editor;
using UniGLTF;
using UniVRM10;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Baku.MaterialOverrideVrmVRC
{
    // Owns the display scene and restores the settings and VRM import changed for preview.
    internal sealed class MouvMaterialPreview : IDisposable
    {
        Scene scene;
        GameObject model;
        RenderPipelineAsset previousDefault, previousQuality;
        int previousAntiAliasing;
        string reimportedVrmPath;
        bool pipelineChanged, refreshLilToon, disposed;
        internal bool IsActive => !disposed && model;

        internal static MouvMaterialPreview Start(UnityEngine.Object vrm, MaterialSnapshot snapshot, RenderPipelineAsset pipeline)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Start preview in Edit Mode after compilation and import finish.");
            var vrmPath = AssetDatabase.GetAssetPath(vrm);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(vrmPath);
            if (!prefab || !snapshot) throw new ArgumentException("Select an exported VRM and material snapshot.");
            var preview = new MouvMaterialPreview
            {
                previousDefault = GraphicsSettings.defaultRenderPipeline,
                previousQuality = QualitySettings.renderPipeline,
                previousAntiAliasing = QualitySettings.antiAliasing
            };
            try
            {
                var activeScene = SceneManager.GetActiveScene();
                preview.scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(activeScene);
                var source = preview.InstantiateSource(prefab, vrm, snapshot);
                var needsReimport = source.RendererBindings.Where(UsesBuiltInMaterial).ToArray();
                if (needsReimport.Length != 0)
                {
                    if (needsReimport.Any(b => AssetDatabase.GetAssetPath(b.TargetMaterial) != vrmPath))
                        throw new InvalidOperationException("A retained VRM slot uses an external Built-in material. Assign a URP material in the VRM import settings or turn off Use Vrm Material for that slot.");
                    if (AssetImporter.GetAtPath(vrmPath) is not VrmScriptedImporter importer ||
                        importer.RenderPipeline != ImporterRenderPipelineTypes.Auto)
                        throw new InvalidOperationException("Set the VRM importer's Render Pipeline to Auto or Universal Render Pipeline and apply the setting before previewing its materials.");
                }

                if (!MaterialOverrideRenderPipeline.IsUniversal(MaterialOverrideRenderPipeline.ConfiguredPipeline))
                {
                    if (MaterialOverrideRenderPipeline.ConfiguredPipeline)
                        throw new InvalidOperationException("Use the Built-in Render Pipeline or URP for preview.");
                    if (!pipeline) pipeline = MaterialOverrideRenderPipeline.FindUrpAssets().FirstOrDefault();
                    if (!pipeline) pipeline = MouvExportWindow.CreateUrp();
                    preview.pipelineChanged = true;
                    MaterialOverrideRenderPipeline.ApplyUrp(pipeline);
                }

                if (needsReimport.Length != 0)
                {
                    UnityEngine.Object.DestroyImmediate(preview.model);
                    preview.reimportedVrmPath = vrmPath;
                    ReimportVrm(vrmPath);
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(vrmPath);
                    source = preview.InstantiateSource(prefab, prefab, snapshot);
                    if (source.RendererBindings.Any(UsesBuiltInMaterial))
                        throw new InvalidOperationException("The selected VRM slots still use Built-in materials after reimport. Check the VRM import settings.");
                }
                preview.model.name = "Material preview: " + prefab.name;
                // Keep the preview apart from the source avatar in other loaded scenes.
                preview.model.transform.position = new Vector3(5, 0, 0);
                var lightObject = new GameObject("Preview light");
                SceneManager.MoveGameObjectToScene(lightObject, preview.scene);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1;
                light.transform.rotation = Quaternion.Euler(40, -30, 0);
                var overrides = source.RendererBindings.Where(binding => binding.ReplacementMaterial).ToArray();
                foreach (var group in overrides.GroupBy(binding => binding.Renderer))
                {
                    var materials = group.Key.sharedMaterials;
                    foreach (var binding in group) materials[binding.MaterialSlot] = binding.ReplacementMaterial;
                    group.Key.sharedMaterials = materials;
                }
                preview.refreshLilToon = overrides.Any(binding => binding.ReplacementMaterial.shader &&
                    binding.ReplacementMaterial.shader.name.IndexOf("lil", StringComparison.OrdinalIgnoreCase) >= 0);
                if (preview.pipelineChanged && preview.refreshLilToon) LilToonExportExtension.Refresh();
                Selection.activeGameObject = preview.model;
                SceneView.FrameLastActiveSceneView();
                AssemblyReloadEvents.beforeAssemblyReload += preview.Dispose;
                EditorApplication.quitting += preview.Dispose;
                EditorApplication.playModeStateChanged += preview.OnPlayModeChanged;
                EditorApplication.update += preview.Update;
                return preview;
            }
            catch { preview.Dispose(); throw; }
        }

        MaterialOverrideSource InstantiateSource(GameObject prefab, UnityEngine.Object vrm, MaterialSnapshot snapshot)
        {
            if (!prefab) throw new InvalidOperationException("Could not load the VRM prefab after reimport.");
            model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var source = model.AddComponent<MaterialOverrideSource>();
            source.SourceVrm = vrm;
            source.RefreshRendererBindings();
            MouvMaterialCapture.Apply(snapshot, source);
            return source;
        }

        static bool UsesBuiltInMaterial(RendererMaterialOverrideBinding binding)
        {
            if (binding.ReplacementMaterial || !binding.TargetMaterial) return false;
            // These are UniVRM's Built-in MToon and glTF PBR/default shaders.
            return binding.TargetMaterial.shader?.name is "VRM10/MToon10" or "Standard" or "Standard (Specular setup)";
        }

        static void ReimportVrm(string path) =>
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        void Update()
        {
            if (!model || !scene.IsValid() || !scene.isLoaded) Dispose();
        }

        void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            EditorApplication.quitting -= Dispose;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= Update;
            try
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            finally
            {
                if (pipelineChanged)
                {
                    GraphicsSettings.defaultRenderPipeline = previousDefault;
                    QualitySettings.renderPipeline = previousQuality;
                    QualitySettings.antiAliasing = previousAntiAliasing;
                    AssetDatabase.SaveAssets();
                    try
                    {
                        if (!string.IsNullOrEmpty(reimportedVrmPath)) ReimportVrm(reimportedVrmPath);
                    }
                    finally
                    {
                        if (refreshLilToon) LilToonExportExtension.Refresh();
                    }
                }
                SceneView.RepaintAll();
            }
        }
    }
}
