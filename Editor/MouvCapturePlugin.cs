using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
[assembly: ExportsPlugin(typeof(Baku.MaterialOverrideVrmVRC.MouvCapturePlugin))]
namespace Baku.MaterialOverrideVrmVRC
{
    public sealed class MouvCapturePlugin : Plugin<MouvCapturePlugin>
    {
        public override string QualifiedName => "com.baxter.material-override-vrm-vrc.capture";
        public override string DisplayName => "Capture Mouv Materials";

        sealed class SourceState
        {
            public bool CaptureEnabled;
            public bool PreserveExtraMaterialSlots;
            public GameObject Avatar;
            public MaterialSnapshot Snapshot;
            public string SnapshotPath;
            public string TemporaryFolder;
            public SourceState() { }
        }

        static readonly Dictionary<GameObject, SourceState> Sources = new Dictionary<GameObject, SourceState>();
        internal static IDisposable AssociateSource(GameObject clone, GameObject source, string temporaryFolder = null)
        {
            Sources.Add(clone, new SourceState { Avatar = source, TemporaryFolder = temporaryFolder });
            return new SourceScope(clone);
        }
        sealed class SourceScope : IDisposable
        {
            readonly GameObject clone;
            public SourceScope(GameObject clone) => this.clone = clone;
            public void Dispose() => Sources.Remove(clone);
        }
        internal static string GetSnapshotPath(BuildContext context) => context.GetState<SourceState>().SnapshotPath;

        internal static bool IsCaptureEnabled(GameObject avatar)
        {
            var component = avatar ? avatar.GetComponent<CaptureMouvMaterials>() : null;
            return component && component.enabled && component.Capture;
        }

        internal static void EnableCapture(GameObject avatar)
        {
            if (IsCaptureEnabled(avatar)) return;
            var component = avatar.GetComponent<CaptureMouvMaterials>();
            if (!component) component = Undo.AddComponent<CaptureMouvMaterials>(avatar);
            Undo.RecordObject(component, "Enable material capture");
            component.enabled = true;
            component.Capture = true;
            EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        protected override void Configure()
        {
            // The exporter clones its input before creating BuildContext. Resolve the original
            // before other plugins can rename the clone. Ambiguous names are never guessed.
            InPhase(BuildPhase.FirstChance).OnPlatforms(new[] { "com.github.hkrn.ndmf-vrm-exporter" }, seq =>
                seq.Run("Identify material capture source", ctx => {
                    var root = ctx.AvatarRootObject;
                    var state = ctx.GetState<SourceState>();
                    state.CaptureEnabled = IsCaptureEnabled(root);
                    var component = root.GetComponent<CaptureMouvMaterials>();
                    state.PreserveExtraMaterialSlots = component && component.PreserveExtraMaterialSlots;
                    if (component) UnityEngine.Object.DestroyImmediate(component);
                    if (!state.CaptureEnabled) return;
                    if (Sources.TryGetValue(root, out var source))
                    {
                        ctx.GetState<SourceState>().Avatar = source.Avatar;
                        ctx.GetState<SourceState>().TemporaryFolder = source.TemporaryFolder;
                        return;
                    }
                    var candidates = root.scene.GetRootGameObjects()
                        .SelectMany(g => g.GetComponentsInChildren<VRCAvatarDescriptor>(true))
                        .Select(d => d.gameObject)
                        .Where(g => g != root && !EditorUtility.IsPersistent(g) &&
                            g.name == MouvMaterialCapture.Normalize(root.name))
                        .ToArray();
                    ctx.GetState<SourceState>().Avatar = candidates.Length == 1 ? candidates[0] : null;
                }));
            InPhase(BuildPhase.Optimizing).OnPlatforms(new[] { "com.github.hkrn.ndmf-vrm-exporter" }, seq =>
                seq.AfterPlugin("nadena.dev.modular-avatar")
                   .AfterPlugin("net.rs64.tex-trans-tool")
                   .AfterPlugin("com.anatawa12.avatar-optimizer")
                   .BeforePlugin("com.github.hkrn.ndmf-vrm-exporter")
                   .Run("Save materials before VRM export", ctx => {
                       var state = ctx.GetState<SourceState>();
                       if (!state.CaptureEnabled) return;
                       if (state.PreserveExtraMaterialSlots) ExpandExtraMaterialSlots(ctx);
                       state.Snapshot = MouvMaterialCapture.Capture(ctx.AvatarRootObject, state.Avatar, state.TemporaryFolder);
                       state.SnapshotPath = AssetDatabase.GetAssetPath(state.Snapshot);
                   }));
        }

        static void ExpandExtraMaterialSlots(BuildContext context)
        {
            foreach (var renderer in context.AvatarRootObject.GetComponentsInChildren<Renderer>(true))
            {
                var skinned = renderer as SkinnedMeshRenderer;
                MeshFilter filter = null;
                if (!skinned && (!(renderer is MeshRenderer) || !renderer.TryGetComponent(out filter))) continue;

                var mesh = skinned ? skinned.sharedMesh : filter.sharedMesh;
                if (!mesh || mesh.subMeshCount == 0) continue;
                var slotCount = renderer.sharedMaterials.Length;
                if (slotCount <= mesh.subMeshCount) continue;

                // Unity draws extra materials over the last submesh. The VRM exporter emits one
                // primitive per submesh, so make those draw calls explicit on the build clone.
                var lastSubmesh = mesh.subMeshCount - 1;
                var indices = mesh.GetIndices(lastSubmesh, applyBaseVertex: false);
                var topology = mesh.GetTopology(lastSubmesh);
                var baseVertex = (int)mesh.GetBaseVertex(lastSubmesh);
                var copy = UnityEngine.Object.Instantiate(mesh);
                copy.name = mesh.name;
                context.AssetSaver.SaveAsset(copy);
                copy.subMeshCount = slotCount;
                for (var slot = lastSubmesh + 1; slot < slotCount; slot++)
                    copy.SetIndices(indices, topology, slot, calculateBounds: false, baseVertex: baseVertex);

                if (skinned) skinned.sharedMesh = copy;
                else filter.sharedMesh = copy;
            }
        }
    }
}
