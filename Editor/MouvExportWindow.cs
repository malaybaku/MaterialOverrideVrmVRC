using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Baku.MaterialOverridableVrm;
using Baku.MaterialOverridableVrm.Editor;
using nadena.dev.ndmf.platform;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRC.SDK3.Avatars.Components;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Baku.MaterialOverrideVrmVRC
{
    public sealed class MouvExportWindow : EditorWindow
    {
        const string WindowTitle = "Export: Material Override VRM for VRChat";
        const string UsageUrl = "https://github.com/malaybaku/MaterialOverrideVrmVRC/blob/main/README_usage.md";
        enum Language { Japanese, English }
        enum ExportMethod { Batch, Individual }
        [SerializeField] Language language;
        [SerializeField] ExportMethod exportMethod;
        [SerializeField] string batchVrmPath = "";
        // Temporary preview control; remove after checking the unavailable-API layout.
        [SerializeField] GameObject avatar;
        [SerializeField] UnityEngine.Object vrm;
        [SerializeField] RenderPipelineAsset urp;
        [SerializeField] MaterialSnapshot snapshot;
        [SerializeField] MaterialSnapshot automaticSnapshot, manualSnapshot;
        [SerializeField] bool keepUrp;
        bool KeepMaterialInformation
        {
            get => SessionState.GetBool("Mouv.Export.KeepMaterialInformation", false);
            set => SessionState.SetBool("Mouv.Export.KeepMaterialInformation", value);
        }
        bool KeepProjectVrm
        {
            get => SessionState.GetBool("Mouv.Export.KeepProjectVrm", false);
            set => SessionState.SetBool("Mouv.Export.KeepProjectVrm", value);
        }
        [SerializeField] bool useLatestSnapshot = true;
        bool busy, showAdvanced;
        MouvMaterialPreview materialPreview;
        BuildUIElement exporterUI;
        VisualElement exporterHost;
        Label exporterCaption, exporterUnavailable;
        Label vrmSaveHelp, batchApiHelp;
        VisualElement batchContent, individualContent;
        MouvVrmExporterBridge batchBridge;
        bool BatchApiAvailable => batchBridge != null;
        string messageJapanese, messageEnglish;
        MessageType messageType;
        string Text(string japanese, string english) => language == Language.Japanese ? japanese : english;
        internal static string DisplayOutputPath(string path) => Path.GetFullPath(path).Replace('\\', '/');

        sealed class LocalizedExportException : InvalidOperationException
        {
            public readonly string EnglishMessage;
            public LocalizedExportException(string japanese, string english) : base(japanese) => EnglishMessage = english;
        }
        void SetMessage(string japanese, string english, MessageType type = MessageType.Info)
        { messageJapanese = japanese; messageEnglish = english; messageType = type; }
        void SetError(Exception exception) => SetMessage(exception.Message,
            exception is LocalizedExportException localized ? localized.EnglishMessage :
            exception is MouvBatchExportException batch ? batch.EnglishMessage : exception.Message, MessageType.Warning);

        [MenuItem("Tools/Material Override VRM/For VRChat: Export Avatar", false, 991)]
        public static void Open() => GetWindow<MouvExportWindow>(WindowTitle);

        void OnEnable()
        {
            if (!useLatestSnapshot && !manualSnapshot) manualSnapshot = snapshot;
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(600, 480);
            MouvMaterialCapture.Captured -= OnSnapshotCaptured;
            MouvMaterialCapture.Captured += OnSnapshotCaptured;
            RefreshBatchApi();
        }

        void OnDisable()
        {
            MouvMaterialCapture.Captured -= OnSnapshotCaptured;
            StopMaterialPreview();
        }

        void StopMaterialPreview()
        {
            var preview = materialPreview;
            materialPreview = null;
            preview?.Dispose();
        }

        void StartMaterialPreview()
        {
            StopMaterialPreview();
            materialPreview = MouvMaterialPreview.Start(vrm, snapshot, urp);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.contentContainer.style.paddingLeft = 15;
            scroll.contentContainer.style.paddingRight = 15;
            scroll.contentContainer.style.paddingTop = 6;
            scroll.contentContainer.style.paddingBottom = 6;
            rootVisualElement.Add(scroll);
            scroll.Add(new IMGUIContainer(DrawHeader));
            batchApiHelp = new Label();
            batchApiHelp.style.whiteSpace = WhiteSpace.Normal;
            batchApiHelp.style.unityFontStyleAndWeight = FontStyle.Bold;
            batchApiHelp.style.color = EditorGUIUtility.isProSkin
                ? new Color(1f, 0.7f, 0.3f) : new Color(0.65f, 0.32f, 0f);
            batchApiHelp.style.marginTop = 8;
            batchApiHelp.style.marginBottom = 8;
            scroll.Add(batchApiHelp);
            batchContent = new VisualElement();
            batchContent.Add(new IMGUIContainer(DrawBatch));
            scroll.Add(batchContent);
            individualContent = new VisualElement();
            scroll.Add(individualContent);
            individualContent.Add(new IMGUIContainer(DrawStep1));
            exporterHost = new VisualElement();
            exporterHost.style.marginLeft = 15;
            exporterHost.style.marginTop = EditorGUIUtility.singleLineHeight * 0.2f;
            exporterHost.style.marginBottom = 12;
            individualContent.Add(exporterHost);
            BuildExporterUI();
            individualContent.Add(new IMGUIContainer(DrawStep2));
            RefreshBatchApi();
            ApplyExportMethod();
            RefreshSnapshotSelection();
        }

        void BuildExporterUI()
        {
            exporterHost.Clear();
            exporterUI = null;
            exporterUnavailable = null;
            exporterCaption = new Label();
            exporterCaption.style.whiteSpace = WhiteSpace.Normal;
            exporterCaption.style.marginBottom = 4;
            exporterHost.Add(exporterCaption);
            try
            {
                if (PlatformRegistry.PlatformProviders.TryGetValue("com.github.hkrn.ndmf-vrm-exporter", out var provider))
                    exporterUI = provider.CreateBuildUI();
                if (exporterUI != null)
                {
                    exporterUI.style.flexGrow = 0;
                    exporterHost.Add(exporterUI);
                }
            }
            catch (Exception exception) { SetError(exception); Debug.LogException(exception); }
            if (exporterUI == null)
            {
                exporterUnavailable = new Label();
                exporterUnavailable.style.whiteSpace = WhiteSpace.Normal;
                exporterHost.Add(exporterUnavailable);
            }
            vrmSaveHelp = new Label();
            vrmSaveHelp.style.whiteSpace = WhiteSpace.Normal;
            // exporterHost already provides one level of indentation.
            vrmSaveHelp.style.marginLeft = 0;
            vrmSaveHelp.style.marginTop = 8;
            vrmSaveHelp.style.marginBottom = 4;
            exporterHost.Add(vrmSaveHelp);
            UpdateExporterLabels();
            RefreshExporterAvatar();
        }

        void UpdateExporterLabels()
        {
            if (exporterCaption != null)
                exporterCaption.text = Text("NDMF VRM ExporterでVRMを出力", "Export VRM with NDMF VRM Exporter");
            if (exporterUnavailable != null)
                exporterUnavailable.text = Text("NDMF VRM Exporterの導入状態を確認してください。", "Check that NDMF VRM Exporter is installed.");
            if (vrmSaveHelp != null)
                vrmSaveHelp.text = Text("* Step2でVRMを参照するため、Assets以下のフォルダにVRMをエクスポートしてください。",
                    "* Export the VRM to a folder under Assets so it can be selected in Step2.");
        }

        void RefreshExporterAvatar()
        {
            if (exporterUI == null) return;
            exporterUI.AvatarRoot = IsSceneAvatar(avatar) ? avatar : null;
            exporterUI.SetEnabled(!busy && IsSceneAvatar(avatar) && MouvCapturePlugin.IsCaptureEnabled(avatar));
        }

        void OnFocus()
        {
            if (busy) return;
            RefreshBatchApi();
            RefreshExporterAvatar();
            RefreshSnapshotSelection();
        }

        void OnProjectChange()
        {
            if (!busy)
            {
                RefreshBatchApi();
                RefreshSnapshotSelection();
            }
            Repaint();
        }

        void OnSnapshotCaptured(MaterialSnapshot captured)
        {
            if (busy) return;
            RefreshSnapshotSelection();
            Repaint();
        }

        void RefreshSnapshotSelection()
        {
            var path = AssetDatabase.GetAssetPath(vrm);
            var prefab = string.Equals(Path.GetExtension(path), ".vrm", StringComparison.OrdinalIgnoreCase)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            var instance = prefab ? prefab.GetComponent<UniVRM10.Vrm10Instance>() : null;
            var avatarName = instance && instance.Vrm ? instance.Vrm.Meta.Name : null;
            automaticSnapshot = MaterialSnapshot.FindLatestByAvatarName(avatarName);
            var selected = useLatestSnapshot ? automaticSnapshot : manualSnapshot;
            snapshot = selected;
        }

        void RefreshBatchApi()
        {
            MouvVrmExporterBridge.TryCreate(out batchBridge, out _);
            ApplyExportMethod();
        }

        void ApplyExportMethod()
        {
            if (batchContent == null || individualContent == null || batchApiHelp == null) return;
            var batch = exportMethod == ExportMethod.Batch;
            batchContent.style.display = batch ? DisplayStyle.Flex : DisplayStyle.None;
            individualContent.style.display = batch ? DisplayStyle.None : DisplayStyle.Flex;
            batchContent.SetEnabled(BatchApiAvailable && !busy);
            individualContent.SetEnabled(!busy);
            batchApiHelp.style.display = batch && !BatchApiAvailable ? DisplayStyle.Flex : DisplayStyle.None;
            batchApiHelp.text = Text("現在の本パッケージとNDMF VRM Exporterで部分的に互換性がないため、このエクスポート方法は使用できません。\nエクスポート方法を「ステップごとにエクスポート」に変更してエクスポートを行ってください。",
                "This export method is unavailable because the current version of this package is partially incompatible with NDMF VRM Exporter.\nPlease change Export method to \"Export step by step\" and export your files.");
        }

        void DrawHeader()
        {
            using (new EditorGUI.DisabledScope(busy))
            {
                var usageLinkStyle = new GUIStyle(EditorStyles.linkLabel) { wordWrap = true };
                var usageLink = new GUIContent(Text("使い方を見る(GitHub)", "View usage guide (GitHub)"), UsageUrl);
                var usageLinkRect = GUILayoutUtility.GetRect(usageLink, usageLinkStyle, GUILayout.ExpandWidth(false));
                EditorGUIUtility.AddCursorRect(usageLinkRect, MouseCursor.Link);
                if (GUI.Button(usageLinkRect, usageLink, usageLinkStyle))
                    Application.OpenURL(UsageUrl);
                GUILayout.Space(EditorGUIUtility.singleLineHeight * 0.3f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Language", GUILayout.Width(65));
                    GUILayout.FlexibleSpace();
                    var previous = language;
                    language = (Language)EditorGUILayout.EnumPopup(language, GUILayout.Width(125));
                    if (previous != language) { UpdateExporterLabels(); ApplyExportMethod(); }
                }
                GUILayout.Space(EditorGUIUtility.singleLineHeight * 0.7f);
                var previousMethod = exportMethod;
                exportMethod = (ExportMethod)EditorGUILayout.Popup(Text("エクスポート方法", "Export method"), (int)exportMethod,
                    new[] { Text("まとめてエクスポート", "Export all at once"), Text("ステップごとにエクスポート", "Export step by step") });
                if (previousMethod != exportMethod) { StopMaterialPreview(); ApplyExportMethod(); }
            }
            GUILayout.Space(EditorGUIUtility.singleLineHeight * 0.7f);
        }

        void DrawAvatarField()
        {
            var previous = avatar;
            var selected = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(Text("元アバター", "Source avatar"),
                avatar ? avatar.GetComponent<VRCAvatarDescriptor>() : null, typeof(VRCAvatarDescriptor), true);
            if (!selected) avatar = null;
            else if (IsSceneAvatar(selected.gameObject)) avatar = selected.gameObject;
            else SetMessage("VRC Avatar Descriptorが付いたシーン上のオブジェクトを指定してください。",
                "Select a scene object with a VRC Avatar Descriptor.", MessageType.Warning);
            if (previous != avatar)
            {
                RefreshSnapshotSelection();
                RefreshExporterAvatar();
            }
        }


        void DrawStep1()
        {
            RefreshExporterAvatar();
            EditorGUILayout.LabelField(Text("Step1: VRM (.vrm) を出力", "Step1: Export VRM (.vrm)"), EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(busy))
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(15);
                using (new EditorGUILayout.VerticalScope())
                {
                    var caption = Text("NDMF VRM ExporterでVRMを出力", "Export VRM with NDMF VRM Exporter");
                    var captionWidth = EditorStyles.label.CalcSize(new GUIContent(caption)).x;
                    if (exporterCaption != null && exporterCaption.panel != null)
                    {
                        var measured = exporterCaption.MeasureTextSize(caption, 0, VisualElement.MeasureMode.Undefined,
                            0, VisualElement.MeasureMode.Undefined).x;
                        if (!float.IsNaN(measured) && !float.IsInfinity(measured))
                            captionWidth = Mathf.Max(captionWidth, measured);
                    }
                    var avatarLabelWidth = Mathf.Max(EditorGUIUtility.labelWidth, captionWidth + 8);
                    var previousLabelWidth = EditorGUIUtility.labelWidth;
                    try
                    {
                        EditorGUIUtility.labelWidth = avatarLabelWidth;
                        DrawAvatarField();
                    }
                    finally { EditorGUIUtility.labelWidth = previousLabelWidth; }
                    var captureReady = MouvCapturePlugin.IsCaptureEnabled(avatar);
                    var captureStatusStyle = new GUIStyle(EditorStyles.label);
                    if (IsSceneAvatar(avatar)) captureStatusStyle.normal.textColor = captureReady
                        ? (EditorGUIUtility.isProSkin ? new Color(0.4f, 0.85f, 0.45f) : new Color(0.1f, 0.5f, 0.17f))
                        : (EditorGUIUtility.isProSkin ? new Color(1f, 0.7f, 0.3f) : new Color(0.65f, 0.32f, 0f));
                    using (new EditorGUI.DisabledScope(!IsSceneAvatar(avatar)))
                    {
                        var row = EditorGUILayout.GetControlRect();
                        var statusRect = new Rect(row.x, row.y, avatarLabelWidth - 2, row.height);
                        var buttonRect = new Rect(row.x + avatarLabelWidth, row.y,
                            Mathf.Max(0, row.width - avatarLabelWidth), row.height);
                        GUI.Label(statusRect, IsSceneAvatar(avatar)
                            ? Text("マテリアルの保存: ", "Material capture: ") +
                                (captureReady ? Text("セットアップ済み", "Ready") : Text("有効化が必要です", "Enable required"))
                            : Text("出力元アバターを指定してください。", "Select a source avatar."), captureStatusStyle);
                        if (GUI.Button(buttonRect, Text("マテリアル保存を有効にする", "Enable material capture")))
                        {
                            MouvCapturePlugin.EnableCapture(avatar);
                            RefreshExporterAvatar();
                        }
                    }
                }
            }
        }

        void DrawAdvancedSettings(bool batch = false)
        {
            EditorGUILayout.Space();
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, Text("詳細設定", "Advanced settings"), true);
            if (!showAdvanced) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(15);
                using (new EditorGUILayout.VerticalScope())
                {
                    var previousLabelWidth = EditorGUIUtility.labelWidth;
                    try
                    {
                        if (batch)
                        {
                            EditorGUIUtility.labelWidth = Mathf.Max(previousLabelWidth,
                                EditorStyles.label.CalcSize(new GUIContent(Text("エクスポートしたマテリアル情報をプロジェクトにも残す",
                                    "Keep exported material information in the project too"))).x + 8);
                            KeepProjectVrm = EditorGUILayout.Toggle(Text("VRMをプロジェクトにも残す", "Keep VRM in the project too"), KeepProjectVrm);
                            using (new EditorGUI.DisabledScope(!KeepProjectVrm))
                            {
                                batchVrmPath = EditorGUILayout.TextField(Text("VRMの保存先 (Assets 以下のパス)", "VRM output path (under Assets)"), batchVrmPath);
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    GUILayout.Space(15);
                                    GUILayout.Label(Text("* 未指定の場合、MaterialOverrideVrm/ 以下に既存ファイルと重複しない名前で保存します。",
                                        "* If empty, the file is saved under MaterialOverrideVrm/ with a name that does not conflict with existing files."), EditorStyles.wordWrappedLabel);
                                }
                            }
                            KeepMaterialInformation = EditorGUILayout.Toggle(Text("エクスポートしたマテリアル情報をプロジェクトにも残す", "Keep exported material information in the project too"), KeepMaterialInformation);
                        }
                        keepUrp = EditorGUILayout.Toggle(Text("エクスポート後もURPを維持", "Keep URP after export"), keepUrp);
                        urp = (RenderPipelineAsset)EditorGUILayout.ObjectField(Text("エクスポート用URP", "URP for export"), urp, typeof(RenderPipelineAsset), false);
                    }
                    finally { EditorGUIUtility.labelWidth = previousLabelWidth; }
                }
            }
        }

        void DrawBatch()
        {
            using (new EditorGUI.DisabledScope(!BatchApiAvailable || busy))
            {
                DrawAvatarField();
                DrawAdvancedSettings(true);
                GUILayout.Space(EditorGUIUtility.singleLineHeight * 0.5f);
                var hasDescription = batchBridge != null && batchBridge.HasExportDescription(avatar);
                if (IsSceneAvatar(avatar) && !hasDescription)
                {
                    var warningStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                    warningStyle.normal.textColor = EditorGUIUtility.isProSkin
                        ? new Color(1f, 0.7f, 0.3f) : new Color(0.65f, 0.32f, 0f);
                    GUILayout.Label(Text("元アバターにVRM Export Descriptionを追加してください。",
                        "Add VRM Export Description to the source avatar."), warningStyle);
                }
                using (new EditorGUI.DisabledScope(!IsSceneAvatar(avatar) || !hasDescription))
                    if (GUILayout.Button(Text("エクスポート", "Export"))) ExportBatch();
                GUILayout.Label(Text("* VRMは、.mouvの保存先の拡張子を .vrm に変更したファイルとして保存します。",
                    "* The VRM is saved to the .mouv output path with its extension changed to .vrm."), EditorStyles.wordWrappedLabel);
            }
            DrawMessage();
        }

        void DrawMessage()
        {
            var message = Text(messageJapanese, messageEnglish);
            if (!string.IsNullOrEmpty(message))
            {
                GUILayout.Space(EditorGUIUtility.singleLineHeight * 0.5f);
                EditorGUILayout.HelpBox(message, messageType);
            }
        }

        async void ExportBatch()
        {
            if (busy || !BatchApiAvailable) return;
            try
            {
                batchBridge.ValidateAvatar(avatar);
                if (KeepProjectVrm) MouvBatchExporter.ResolveVrmPath(batchVrmPath, avatar.name);
                var output = EditorUtility.SaveFilePanel(Text(".mouvをエクスポート", "Export .mouv"), "", avatar.name, "mouv");
                if (string.IsNullOrEmpty(output)) return;
                busy = true;
                ApplyExportMethod();
                RefreshExporterAvatar();
                var result = await MouvBatchExporter.ExportAsync(avatar, batchVrmPath, output, urp, keepUrp, !KeepMaterialInformation, KeepProjectVrm,
                    japanese: language == Language.Japanese);
                SetMessage("エクスポート完了\nVRM: " + DisplayOutputPath(result.VrmPath) + "\n.mouv: " + DisplayOutputPath(output),
                    "Export complete\nVRM: " + DisplayOutputPath(result.VrmPath) + "\n.mouv: " + DisplayOutputPath(output));
                EditorUtility.DisplayDialog("Material Override VRM for VRChat", "Avatar file (.mouv) exported successfully.", "OK");
            }
            catch (OperationCanceledException) { SetMessage("エクスポートをキャンセルしました。", "Export cancelled."); }
            catch (Exception exception) { SetError(exception); Debug.LogException(exception); }
            finally { busy = false; ApplyExportMethod(); RefreshExporterAvatar(); Repaint(); }
        }

        void DrawStep2()
        {
            EditorGUILayout.LabelField(Text("Step2: Material Override VRM (.mouv) を出力", "Step2: Export Material Override VRM (.mouv)"), EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(15);
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUI.DisabledScope(busy))
                    {
                        var previousVrm = vrm;
                        var vrmPrefab = vrm ? AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(vrm)) : null;
                        var selectedVrm = (GameObject)EditorGUILayout.ObjectField(Text("出力済みVRM", "Exported VRM"),
                            vrmPrefab, typeof(GameObject), false);
                        if (!selectedVrm) vrm = null;
                        else if (IsExportedVrmAsset(selectedVrm)) vrm = selectedVrm;
                        else if (selectedVrm != vrmPrefab)
                            SetMessage("VRM10Instanceが付いた、インポート済みの.vrmアセットを指定してください。",
                                "Select an imported .vrm asset with a VRM10Instance component.", MessageType.Warning);
                        if (vrm != previousVrm) { StopMaterialPreview(); RefreshSnapshotSelection(); }
                        var previousAutomatic = useLatestSnapshot;
                        useLatestSnapshot = EditorGUILayout.Popup(Text("マテリアルの指定方法", "Material selection method"), useLatestSnapshot ? 0 : 1,
                            new[] { Text("自動で選択 (VRMのアバター名と一致する最新)", "Automatic (latest matching the VRM avatar name)"),
                                Text("手動で選択", "Manual") }) == 0;
                        if (previousAutomatic != useLatestSnapshot) { StopMaterialPreview(); RefreshSnapshotSelection(); }
                        using (new EditorGUI.DisabledScope(!useLatestSnapshot))
                        {
                            var rowRect = EditorGUILayout.GetControlRect();
                            var labelRect = new Rect(rowRect.x, rowRect.y, EditorGUIUtility.labelWidth - 2, rowRect.height);
                            // PrefixLabel can defer drawing until ObjectField, inheriting its disabled state.
                            GUI.Label(labelRect, Text("自動選択マテリアル", "Automatic materials"), EditorStyles.label);
                            var fieldRect = new Rect(rowRect.x + EditorGUIUtility.labelWidth, rowRect.y,
                                Mathf.Max(0, rowRect.width - EditorGUIUtility.labelWidth), rowRect.height);
                            using (new EditorGUI.DisabledScope(true))
                                EditorGUI.ObjectField(fieldRect, automaticSnapshot, typeof(MaterialSnapshot), false);
                        }
                        using (new EditorGUI.DisabledScope(!useLatestSnapshot))
                            DrawSnapshotMetadata(automaticSnapshot);
                        if (!useLatestSnapshot)
                        {
                            GUILayout.Space(EditorGUIUtility.singleLineHeight * 0.2f);
                            var selected = (MaterialSnapshot)EditorGUILayout.ObjectField(Text("手動選択マテリアル", "Manual materials"), manualSnapshot, typeof(MaterialSnapshot), false);
                            if (selected != manualSnapshot) { StopMaterialPreview(); manualSnapshot = selected; RefreshSnapshotSelection(); }
                            DrawSnapshotMetadata(manualSnapshot);
                        }
                        GUILayout.Space(EditorGUIUtility.singleLineHeight * 0.5f);
                        if (materialPreview != null && materialPreview.IsActive)
                        {
                            var previewHelpStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                            previewHelpStyle.normal.textColor = EditorGUIUtility.isProSkin
                                ? new Color(1f, 0.7f, 0.3f) : new Color(0.65f, 0.32f, 0f);
                            GUILayout.Label(Text("シーン上にマテリアルを適用したモデルをプレビュー表示中です。\n「Material preview:」オブジェクトの外観を確認してください。\n「プレビューを終了」でプレビューを終了します。",
                                "A model with the materials applied is being previewed in the scene.\nCheck the appearance of the \"Material preview:\" object.\nSelect \"Stop preview\" to end the preview."), previewHelpStyle);
                        }
                        using (new EditorGUI.DisabledScope(!vrm || !snapshot || EditorApplication.isPlayingOrWillChangePlaymode))
                        {
                            if (GUILayout.Button(materialPreview != null && materialPreview.IsActive
                                ? Text("プレビューを終了", "Stop preview") : Text("URPでプレビューを開始", "Start preview in URP")))
                            {
                                try
                                {
                                    if (materialPreview != null && materialPreview.IsActive) StopMaterialPreview();
                                    else StartMaterialPreview();
                                }
                                catch (Exception error) { SetError(error); Debug.LogException(error); }
                            }
                        }

                        DrawAdvancedSettings();
                        GUILayout.Space(EditorGUIUtility.singleLineHeight * 0.5f);
                        using (new EditorGUI.DisabledScope(!vrm || !snapshot))
                            if (GUILayout.Button(Text("エクスポート", "Export"))) Export();
                    }
                    if (!vrm || !snapshot)
                    {
                        var guidanceStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                        guidanceStyle.normal.textColor = EditorGUIUtility.isProSkin
                            ? new Color(1f, 0.7f, 0.3f) : new Color(0.65f, 0.32f, 0f);
                        GUILayout.Label(!vrm
                            ? Text("エクスポートをするには出力済みVRMを指定してください。", "Select an exported VRM to export.")
                            : Text("エクスポートをするにはマテリアル情報を設定してください。", "Set the material information to export."), guidanceStyle);
                    }
                    DrawMessage();
                }
            }
        }

        void DrawSnapshotMetadata(MaterialSnapshot information)
        {
            var metadataStyle = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
            var avatarLabel = Text("アバター名:", "Avatar name:");
            var timeLabel = Text("作成時刻:", "Created at:");
            var labelWidth = Mathf.Max(EditorStyles.label.CalcSize(new GUIContent(avatarLabel)).x,
                EditorStyles.label.CalcSize(new GUIContent(timeLabel)).x) + 8;
            var time = information
                ? (information.TryGetCapturedTime(out var captured) ? captured.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : information.CapturedUtc)
                : "-";
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(15);
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(avatarLabel, EditorStyles.label, GUILayout.Width(labelWidth));
                        GUILayout.Label(information ? information.AvatarName : "-", metadataStyle);
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(timeLabel, EditorStyles.label, GUILayout.Width(labelWidth));
                        GUILayout.Label(time, metadataStyle);
                    }
                }
            }
        }

        static bool IsExportedVrmAsset(GameObject candidate) => candidate && EditorUtility.IsPersistent(candidate) &&
            candidate.GetComponent<UniVRM10.Vrm10Instance>() &&
            string.Equals(Path.GetExtension(AssetDatabase.GetAssetPath(candidate)), ".vrm", StringComparison.OrdinalIgnoreCase);

        static bool IsSceneAvatar(GameObject candidate) => candidate &&
            candidate.GetComponent<VRCAvatarDescriptor>() && !EditorUtility.IsPersistent(candidate) &&
            candidate.scene.IsValid() && candidate.scene.isLoaded && PrefabStageUtility.GetPrefabStage(candidate) == null;

        MaterialOverrideSource Prepare(out GameObject instance)
        {
            if ((!avatar && !snapshot) || !vrm) throw new LocalizedExportException("VRMと、元アバターまたは保存済みマテリアルを指定してください。", "Select a VRM and either a source avatar or saved materials.");
            if (avatar && !IsSceneAvatar(avatar))
                throw new LocalizedExportException("VRC Avatar Descriptorが付いたシーン上のオブジェクトを指定してください。",
                    "Select a scene object with a VRC Avatar Descriptor.");
            var path = AssetDatabase.GetAssetPath(vrm);
            if (!string.Equals(Path.GetExtension(path), ".vrm", StringComparison.OrdinalIgnoreCase))
                throw new LocalizedExportException(".vrmアセットを指定してください。", "Select a .vrm asset.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) throw new LocalizedExportException("VRMのインポート完了を待ってください。", "Wait for the VRM import to finish.");
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            var prepared = instance.AddComponent<MaterialOverrideSource>();
            prepared.SourceVrm = vrm;
            prepared.RefreshRendererBindings();
            if (snapshot)
            {
                MouvMaterialCapture.Apply(snapshot, prepared);
                return prepared;
            }
            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
            var allMaterials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m).Distinct().ToArray();
            int unresolved = 0;
            foreach (var binding in prepared.RendererBindings)
            {
                var relative = AnimationUtility.CalculateTransformPath(binding.Renderer.transform, instance.transform);
                var matches = renderers.Where(r => AnimationUtility.CalculateTransformPath(r.transform, avatar.transform) == relative).ToArray();
                Material candidate = null;
                if (matches.Length == 1 && binding.MaterialSlot < matches[0].sharedMaterials.Length)
                {
                    var material = matches[0].sharedMaterials[binding.MaterialSlot];
                    if (material && binding.TargetMaterial && material.name == binding.TargetMaterial.name) candidate = material;
                }
                if (!candidate && binding.TargetMaterial)
                {
                    var named = allMaterials.Where(m => m.name == binding.TargetMaterial.name).ToArray();
                    if (named.Length == 1) candidate = named[0];
                }
                binding.ReplacementMaterial = candidate;
                if (!candidate) unresolved++;
            }
            EditorUtility.SetDirty(prepared);
            if (unresolved > 0) throw new LocalizedExportException($"{unresolved}スロットが未解決です。設定済みVRMのInspectorで修正してください。", $"{unresolved} material slots are unresolved. Fix them in the prepared VRM Inspector.");
            return prepared;
        }

        async Task ExportIndividualAsync(string output)
        {
            StopMaterialPreview();
            GameObject instance = null;
            try
            {
                var prepared = Prepare(out instance);
                await ExportAsync(prepared, output, urp, keepUrp, snapshot);
            }
            finally { if (instance) DestroyImmediate(instance); }
        }

        async void Export()
        {
            if (busy) return;
            RefreshSnapshotSelection();
            busy = true;
            ApplyExportMethod();
            RefreshExporterAvatar();
            try
            {
                if (!snapshot) throw new LocalizedExportException("マテリアル情報を指定してください。", "Select material information.");
                var output = EditorUtility.SaveFilePanel(Text(".mouvをエクスポート", "Export .mouv"), "", avatar ? avatar.name : "avatar", "mouv");
                if (string.IsNullOrEmpty(output)) return;
                await ExportIndividualAsync(output);
                SetMessage("エクスポート完了: " + DisplayOutputPath(output), "Export complete: " + DisplayOutputPath(output));
                EditorUtility.DisplayDialog("Material Override VRM for VRChat", "Avatar file (.mouv) exported successfully.", "OK");
            }
            catch (Exception e) { SetError(e); Debug.LogException(e); }
            finally { busy = false; ApplyExportMethod(); RefreshExporterAvatar(); Repaint(); }
        }

        public static async Task ExportAsync(MaterialOverrideSource source, string output, RenderPipelineAsset pipeline, bool keepPipeline,
            MaterialSnapshot snapshot = null)
        {
            var previousDefault = GraphicsSettings.defaultRenderPipeline;
            var previousQuality = QualitySettings.renderPipeline;
            var switched = !MaterialOverrideRenderPipeline.IsUniversal(MaterialOverrideRenderPipeline.ConfiguredPipeline);
            var context = new MouvExportContext(snapshot, source, output, switched);
            var extensions = new MouvExportExtensions.Session();
            var success = false;
            try
            {
                if (switched)
                {
                    if (MaterialOverrideRenderPipeline.ConfiguredPipeline)
                        throw new LocalizedExportException("Built-inまたはURPで実行してください。", "Use the Built-in Render Pipeline or URP.");
                    if (!pipeline)
                    {
                        pipeline = MaterialOverrideRenderPipeline.FindUrpAssets().FirstOrDefault();
                        if (!pipeline) pipeline = CreateUrp();
                    }
                    MaterialOverrideRenderPipeline.ApplyUrp(pipeline);
                    await Task.Yield();
                }
                await extensions.Prepare(context);
                if (context.Snapshot)
                {
                    EditorUtility.SetDirty(context.Snapshot);
                    AssetDatabase.SaveAssets();
                }
                await MaterialOverrideExporter.ExportAsync(source, output);
                success = true;
                context.ExportSucceeded = true;
                Debug.Log("Material Override VRM exported: " + DisplayOutputPath(output));
            }
            finally
            {
                try
                {
                    if (switched && (!success || !keepPipeline))
                    {
                        GraphicsSettings.defaultRenderPipeline = previousDefault;
                        QualitySettings.renderPipeline = previousQuality;
                        context.PipelineRestored = true;
                        AssetDatabase.SaveAssets();
                    }
                }
                finally { await extensions.Finish(context); }
            }
        }

        internal static RenderPipelineAsset CreateUrp()
        {
            Directory.CreateDirectory("Assets/MouvSettings");
            AssetDatabase.Refresh();
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, AssetDatabase.GenerateUniqueAssetPath("Assets/MouvSettings/Renderer.asset"));
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, AssetDatabase.GenerateUniqueAssetPath("Assets/MouvSettings/URP.asset"));
            AssetDatabase.SaveAssets();
            return pipeline;
        }
    }
}
