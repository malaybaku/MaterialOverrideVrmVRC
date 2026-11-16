using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Baku.MaterialOverridableVrm;
using nadena.dev.ndmf;
using nadena.dev.ndmf.platform;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace Baku.MaterialOverrideVrmVRC
{
    public static class MouvBatchExporter
    {
        public sealed class Result
        {
            public string VrmPath;
            public string SnapshotPath;
            public bool SnapshotWasTemporary;
            public MaterialSnapshot Snapshot => string.IsNullOrEmpty(SnapshotPath) ? null : AssetDatabase.LoadAssetAtPath<MaterialSnapshot>(SnapshotPath);
        }
        public static bool IsRunning { get; private set; }

        public static string ResolveVrmPath(string requestedPath, string avatarName)
        {
            var path = (requestedPath ?? "").Trim().Replace('\\', '/');
            var safeName = new string((avatarName ?? "Avatar").Where(c => !Path.GetInvalidFileNameChars().Contains(c) && c != '/' && c != '\\').Take(48).ToArray()).Trim(' ', '.');
            if (string.IsNullOrEmpty(safeName)) safeName = "Avatar";
            if (string.IsNullOrEmpty(path)) path = "Assets/MaterialOverrideVrm/" + safeName + ".vrm";
            else if (path.EndsWith("/") || Directory.Exists(path) || !Path.HasExtension(path)) path = path.TrimEnd('/') + "/" + safeName + ".vrm";
            var full = Path.GetFullPath(path);
            var assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(assets, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(full), ".vrm", StringComparison.OrdinalIgnoreCase))
                throw new MouvBatchExportException("VRM保存先にはAssets以下のフォルダ、または.vrmファイルのパスを指定してください。",
                    "Specify a folder or a .vrm file path under Assets.");
            path = "Assets/" + full.Substring(assets.Length).Replace('\\', '/');
            // GenerateUniqueAssetPath can return empty when the destination folder is not imported yet.
            var unique = path;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var stem = Path.GetFileNameWithoutExtension(path);
            for (var index = 1; File.Exists(unique) || Directory.Exists(unique) || File.Exists(unique + ".meta"); index++)
                unique = parent + "/" + stem + " " + index + ".vrm";
            return unique;
        }

        public static async Task<Result> ExportAsync(GameObject sourceAvatar, string requestedVrmPath,
            string mouvPath, RenderPipelineAsset pipeline, bool keepPipeline, bool temporarySnapshot = false, bool keepProjectVrm = false,
            bool japanese = true)
        {
            if (IsRunning || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new MouvBatchExportException("Unityの処理が完了してから再実行してください。", "Wait for Unity to finish its current operation.");
            if (!sourceAvatar || EditorUtility.IsPersistent(sourceAvatar) || !sourceAvatar.scene.IsValid() ||
                !sourceAvatar.scene.isLoaded || PrefabStageUtility.GetPrefabStage(sourceAvatar) != null ||
                !sourceAvatar.GetComponent<VRCAvatarDescriptor>())
                throw new MouvBatchExportException("VRC Avatar Descriptorが付いたシーン上の元アバターを指定してください。",
                    "Select a scene avatar with a VRC Avatar Descriptor.");
            if (string.IsNullOrWhiteSpace(mouvPath)) throw new ArgumentException("A .mouv output path is required.", nameof(mouvPath));
            if (!MouvVrmExporterBridge.TryCreate(out var bridge, out var reason))
                throw new MouvBatchExportException("一括出力用APIに対応していません: " + reason, "Batch export API unavailable: " + reason);
            bridge.ValidateAvatar(sourceAvatar);
            var importFolder = "Assets/MaterialOverrideVrm/Generated/VrmWork/" + Guid.NewGuid().ToString("N");
            var vrmPath = ResolveVrmPath(keepProjectVrm ? requestedVrmPath : importFolder + "/", sourceAvatar.name);
            var exportedVrmPath = Path.ChangeExtension(Path.GetFullPath(mouvPath), ".vrm");
            if (string.Equals(exportedVrmPath, Path.GetFullPath(mouvPath), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The output file must have a .mouv extension.", nameof(mouvPath));
            if (Directory.Exists(exportedVrmPath)) throw new IOException("A directory exists at the VRM output path: " + exportedVrmPath);
            var overwriteVrm = File.Exists(exportedVrmPath);
            if (overwriteVrm && !EditorUtility.DisplayDialog(japanese ? "VRMの上書き" : "Overwrite VRM",
                (japanese
                    ? "同名のVRMがあります。OKを選択すると既存のVRMを上書きして処理を続行します。キャンセルを選択するとエクスポートを中止します。"
                    : "A VRM with the same name already exists. Select OK to overwrite it and continue, or Cancel to stop the export.") + "\n\n" + MouvExportWindow.DisplayOutputPath(exportedVrmPath),
                "OK", japanese ? "キャンセル" : "Cancel"))
                throw new OperationCanceledException("VRM overwrite was declined.");
            bool completed = false;
            if (string.Equals(Path.GetFullPath(vrmPath), Path.GetFullPath(mouvPath), StringComparison.OrdinalIgnoreCase))
                throw new MouvBatchExportException("VRMと.mouvは別の保存先を指定してください。", "Use different paths for the VRM and .mouv files.");

            MouvCapturePlugin.EnableCapture(sourceAvatar);
            IsRunning = true;
            GameObject clone = null, instance = null;
            var workRoot = Path.GetFullPath("Library/MaterialOverrideVrmVRC").TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var work = Path.Combine(workRoot, Guid.NewGuid().ToString("N"));
            var temporaryFolder = temporarySnapshot ? MouvMaterialCapture.WorkRoot + "/Temporary_" + Guid.NewGuid().ToString("N") : null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(vrmPath));
                Directory.CreateDirectory(work);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                clone = Object.Instantiate(sourceAvatar);
                clone.name = sourceAvatar.name;
                // NDMF records the clone's scene in its error report; keep normal scene membership.
                BuildContext context;
                using (MouvCapturePlugin.AssociateSource(clone, sourceAvatar, temporaryFolder))
                using (new AmbientPlatform.Scope(bridge.Platform))
                using (new OverrideTemporaryDirectoryScope(null))
                    context = AvatarProcessor.ProcessAvatar(clone, bridge.Platform);
                if (!context.Successful)
                    throw new MouvBatchExportException("NDMFビルドにエラーがあります。NDMF Consoleを確認してください。", "NDMF reported build errors. Check the NDMF Console.");
                var snapshotPath = MouvCapturePlugin.GetSnapshotPath(context);
                var snapshot = string.IsNullOrEmpty(snapshotPath) ? null : AssetDatabase.LoadAssetAtPath<MaterialSnapshot>(snapshotPath);
                if (!snapshot)
                    throw new MouvBatchExportException("今回のビルドのマテリアル情報を取得できませんでした。", "Material information was not captured for this build.");
                // Keep the standard A export; invoke B after all NDMF processing has finished.
                if (File.Exists(vrmPath)) throw new IOException("The reserved VRM path is no longer empty: " + vrmPath);
                bridge.Export(clone, context, Path.ChangeExtension(vrmPath, null), work);
                if (!File.Exists(vrmPath) || new FileInfo(vrmPath).Length == 0)
                    throw new IOException("VRM export did not produce a file: " + vrmPath);
                Object.DestroyImmediate(clone); clone = null;

                AssetDatabase.ImportAsset(vrmPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var deadline = EditorApplication.timeSinceStartup + 60;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(vrmPath);
                while (!prefab && EditorApplication.timeSinceStartup < deadline)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) throw new OperationCanceledException("Play Mode started during export.");
                    await Task.Delay(100);
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(vrmPath);
                }
                if (!prefab)
                    throw new MouvBatchExportException("VRMのインポートが完了しませんでした。Consoleを確認してください: " + vrmPath,
                        "VRM import did not complete. Check the Console: " + vrmPath);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, sourceAvatar.scene);
                instance.hideFlags = HideFlags.HideAndDontSave;
                var source = instance.AddComponent<MaterialOverrideSource>();
                source.SourceVrm = prefab;
                source.RefreshRendererBindings();
                // Asset imports/refreshes can unload ScriptableObjects held only by managed locals.
                snapshot = AssetDatabase.LoadAssetAtPath<MaterialSnapshot>(snapshotPath);
                if (!snapshot) throw new IOException("Material capture asset is no longer available: " + snapshotPath);
                MouvMaterialCapture.Apply(snapshot, source);
                await MouvExportWindow.ExportAsync(source, mouvPath, pipeline, keepPipeline, snapshot);
                File.Copy(vrmPath, exportedVrmPath, overwriteVrm);
                Debug.Log("VRM exported: " + MouvExportWindow.DisplayOutputPath(exportedVrmPath));
                completed = true;
                return new Result { VrmPath = exportedVrmPath, SnapshotPath = temporarySnapshot ? null : snapshotPath, SnapshotWasTemporary = temporarySnapshot };
            }
            finally
            {
                try
                {
                    if (instance) Object.DestroyImmediate(instance);
                    if (clone) Object.DestroyImmediate(clone);
                    if (Directory.Exists(work) && Path.GetFullPath(work).StartsWith(workRoot, StringComparison.OrdinalIgnoreCase))
                        Directory.Delete(work, true);
                }
                finally
                {
                    try
                    {
                        try
                        {
                            if (completed && !keepProjectVrm && AssetDatabase.IsValidFolder(importFolder) && !AssetDatabase.DeleteAsset(importFolder))
                                throw new IOException("Could not remove imported VRM: " + importFolder);
                        }
                        finally
                        {
                            if (temporaryFolder != null && AssetDatabase.IsValidFolder(temporaryFolder) && !AssetDatabase.DeleteAsset(temporaryFolder))
                                throw new IOException("Could not remove temporary material capture: " + temporaryFolder);
                        }
                    }
                    finally { IsRunning = false; }
                }
            }
        }
    }
}
