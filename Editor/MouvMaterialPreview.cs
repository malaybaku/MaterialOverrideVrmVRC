using System;
using System.Linq;
using Baku.MaterialOverridableVrm;
using Baku.MaterialOverridableVrm.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Baku.MaterialOverrideVrmVRC
{
    // Owns only the display scene and the pipeline settings changed for this preview.
    internal sealed class MouvMaterialPreview : IDisposable
    {
        Scene scene;
        GameObject model;
        RenderPipelineAsset previousDefault, previousQuality;
        bool pipelineChanged, refreshLilToon, disposed;
        internal bool IsActive => !disposed && model;

        internal static MouvMaterialPreview Start(UnityEngine.Object vrm, MaterialSnapshot snapshot, RenderPipelineAsset pipeline)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Start preview in Edit Mode after compilation and import finish.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(vrm));
            if (!prefab || !snapshot) throw new ArgumentException("Select an exported VRM and material snapshot.");
            var preview = new MouvMaterialPreview
            {
                previousDefault = GraphicsSettings.defaultRenderPipeline,
                previousQuality = QualitySettings.renderPipeline
            };
            try
            {
                var activeScene = SceneManager.GetActiveScene();
                preview.scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(activeScene);
                preview.model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview.scene);
                preview.model.name = "Material preview: " + prefab.name;
                // Keep the preview apart from the source avatar in other loaded scenes.
                preview.model.transform.position = new Vector3(5, 0, 0);
                var lightObject = new GameObject("Preview light");
                SceneManager.MoveGameObjectToScene(lightObject, preview.scene);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1;
                light.transform.rotation = Quaternion.Euler(40, -30, 0);
                var source = preview.model.AddComponent<MaterialOverrideSource>();
                source.SourceVrm = vrm;
                source.RefreshRendererBindings();
                MouvMaterialCapture.Apply(snapshot, source);
                foreach (var group in source.RendererBindings.GroupBy(binding => binding.Renderer))
                {
                    var materials = group.Key.sharedMaterials;
                    foreach (var binding in group) materials[binding.MaterialSlot] = binding.ReplacementMaterial;
                    group.Key.sharedMaterials = materials;
                }
                preview.refreshLilToon = source.RendererBindings.Any(binding => binding.ReplacementMaterial.shader &&
                    binding.ReplacementMaterial.shader.name.IndexOf("lil", StringComparison.OrdinalIgnoreCase) >= 0);
                if (!MaterialOverrideRenderPipeline.IsUniversal(MaterialOverrideRenderPipeline.ConfiguredPipeline))
                {
                    if (MaterialOverrideRenderPipeline.ConfiguredPipeline)
                        throw new InvalidOperationException("Use the Built-in Render Pipeline or URP for preview.");
                    if (!pipeline) pipeline = MaterialOverrideRenderPipeline.FindUrpAssets().FirstOrDefault();
                    if (!pipeline) pipeline = MouvExportWindow.CreateUrp();
                    preview.pipelineChanged = true;
                    MaterialOverrideRenderPipeline.ApplyUrp(pipeline);
                    if (preview.refreshLilToon) LilToonExportExtension.Refresh();
                }
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
                    AssetDatabase.SaveAssets();
                    if (refreshLilToon) LilToonExportExtension.Refresh();
                }
                SceneView.RepaintAll();
            }
        }
    }
}
