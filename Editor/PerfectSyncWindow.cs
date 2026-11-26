using System;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Baku.MaterialOverrideVrmVRC
{
    internal sealed class PerfectSyncWindow : EditorWindow
    {
        const string UsageUrl = "https://github.com/malaybaku/MaterialOverrideVrmVRC/blob/main/README_usage.md#perfect-sync-expressions";
        enum Language { Japanese, English }
        [SerializeField] Language language;
        [SerializeField] GameObject avatar;
        [SerializeField] bool useClips;
        [SerializeField] bool keepExisting;
        [SerializeField] bool showDetails;
        string result;
        (int defined, int effective)? counts;
        string summaryError;
        Vector2 scroll;
        GUIStyle content;

        string Text(string japanese, string english) => language == Language.Japanese ? japanese : english;

        const string WindowTitle = "VRM Perfect Sync Setup";

        void OnEnable() => titleContent = new GUIContent(WindowTitle);

        [MenuItem("Tools/Material Override VRM/For VRChat (Util): " + WindowTitle + "...", false, 992)]
        static void Open()
        {
            var window = GetWindow<PerfectSyncWindow>(WindowTitle);
            for (var target = Selection.activeTransform; target; target = target.parent)
            {
                if (!PerfectSyncSetup.IsSceneAvatar(target.gameObject)) continue;
                window.avatar = target.gameObject;
                window.result = null;
                window.RefreshSummary();
                break;
            }
        }

        void OnFocus() => RefreshSummary();

        void RefreshSummary()
        {
            counts = null;
            summaryError = null;
            var description = PerfectSyncSetup.Description(avatar);
            if (!description) return;
            try { counts = PerfectSyncSetup.CountExpressions(description); }
            catch (InvalidOperationException exception) { summaryError = exception.Message; }
        }

        void OnGUI()
        {
            using var view = new EditorGUILayout.ScrollViewScope(scroll);
            scroll = view.scrollPosition;
            content ??= new GUIStyle { padding = new RectOffset(15, 15, 6, 6) };
            using var panel = new EditorGUILayout.VerticalScope(content);
            var previous = language;
            language = MouvWindowHeader.DrawControls(Text("使い方を見る(GitHub)", "Guide (GitHub)"), UsageUrl, language);
            if (language != previous) result = null;
            MouvWindowHeader.Draw(Text("VRM出力用のパーフェクトシンク表情設定", "Perfect Sync Expression Settings for VRM Export"));
            DrawSetup();
        }

        void DrawSetup()
        {
            var current = avatar ? avatar.GetComponent<VRCAvatarDescriptor>() : null;
            var avatarRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight + 4);
            var selected = (VRCAvatarDescriptor)EditorGUI.ObjectField(avatarRect, Text("対象アバター", "Target avatar"),
                current, typeof(VRCAvatarDescriptor), true);
            if (selected != current && (!selected || PerfectSyncSetup.IsSceneAvatar(selected.gameObject)))
            {
                avatar = selected ? selected.gameObject : null;
                result = null;
                RefreshSummary();
            }
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField(Text("セットアップ方法", "Setup method"));
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(15);
                using var options = new EditorGUILayout.VerticalScope();
                useClips = GUILayout.SelectionGrid(useClips ? 1 : 0,
                    new[] { " " + Text("BlendShape を直接割り当てる (推奨)", "Assign BlendShapes directly (Recommended)"), " " + Text("AnimationClip を作成する", "Create AnimationClips") }, 1, EditorStyles.radioButton) == 1;
                EditorGUILayout.HelpBox(useClips
                    ? Text("表情ごとのクリップと編集用 Animator Controller を作成します。クリップの0フレーム目で強度や組み合わせを調整できます。",
                        "Creates a clip for each expression and a controller for editing. Adjust weights and combine shapes at frame 0.")
                    : Text("強度100%で割り当てます。対象のシェイプは VRM Export Description で変更できます。",
                        "Assigns shapes at 100% weight. Change the shape in VRM Export Description."), MessageType.Info);
            }
            if (EditorGUI.EndChangeCheck()) result = null;
            var description = PerfectSyncSetup.Description(avatar);
            showDetails = EditorGUILayout.Foldout(showDetails, Text("詳細", "Details"), true);
            if (showDetails)
            {
                using var row = new EditorGUILayout.HorizontalScope();
                GUILayout.Space(15);
                using var details = new EditorGUILayout.VerticalScope();
                EditorGUI.BeginChangeCheck();
                keepExisting = EditorGUILayout.ToggleLeft(Text("定義済みのExpressionは変更しない", "Keep existing expressions unchanged"), keepExisting);
                if (EditorGUI.EndChangeCheck()) result = null;
                using var disabled = new EditorGUI.DisabledScope(!description || (counts?.defined ?? 0) == 0 || EditorApplication.isPlayingOrWillChangePlaymode);
                if (GUILayout.Button(Text("パーフェクトシンク用 Expression を削除", "Delete Perfect Sync expressions")))
                    DeleteExpressions(description);
            }
            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            bool ready = description;
            if (!PerfectSyncSetup.IsSceneAvatar(avatar))
                EditorGUILayout.HelpBox(Text("VRC Avatar Descriptor が付いたシーン上のアバターを指定してください。",
                    "Select an avatar in the scene that has a VRC Avatar Descriptor."), MessageType.Info);
            else if (PerfectSyncSetup.DescriptionType == null)
                EditorGUILayout.HelpBox(Text("NDMF VRM Exporter の導入状態を確認してください。",
                    "Check that NDMF VRM Exporter is installed."), MessageType.Warning);
            else if (!ready)
                EditorGUILayout.HelpBox(Text("対象アバターに VRM Export Description を追加してください。",
                    "Add a VRM Export Description to the target avatar."), MessageType.Info);
            using (new EditorGUI.DisabledScope(!ready || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button(Text("Expressionを作成", "Create Expressions")))
                {
                    CreateExpressions();
                    RefreshSummary();
                }
            }
            if (ready) DrawSummary();
            if (string.IsNullOrEmpty(result)) return;
            GUILayout.Space(4);
            var resultStyle = new GUIStyle(EditorStyles.helpBox);
            resultStyle.padding.top += 4;
            resultStyle.padding.bottom += 4;
            var resultRect = GUILayoutUtility.GetRect(new GUIContent(result), resultStyle, GUILayout.ExpandWidth(true));
            EditorGUI.SelectableLabel(resultRect, result, resultStyle);
            GUILayout.Space(4);
        }

        void DeleteExpressions(Component description)
        {
            if (!EditorUtility.DisplayDialog(Text("表情の削除", "Delete expressions"),
                    Text("VRM Export Description からパーフェクトシンク用 Expression の定義を削除しますか？\n削除後もモデル本体の BlendShape は維持されます。",
                        "Delete the Perfect Sync expression definitions from VRM Export Description?\nThe model will keep its blend shapes."),
                    Text("削除する", "Delete"), Text("キャンセル", "Cancel"))) return;
            try
            {
                var removed = PerfectSyncSetup.DeleteExpressions(description);
                result = Text($"パーフェクトシンク用 Expression を {removed} 件削除しました。", $"Deleted {removed} Perfect Sync expression definitions.");
                RefreshSummary();
            }
            catch (InvalidOperationException exception)
            {
                EditorUtility.DisplayDialog(Text("表情を削除できません", "Could not delete expressions"), exception.Message, "OK");
            }
        }

        void DrawSummary()
        {
            if (summaryError != null) EditorGUILayout.HelpBox(summaryError, MessageType.Warning);
            if (!counts.HasValue) return;

            var (defined, effective) = counts.Value;
            GUILayout.Space(6);
            EditorGUILayout.LabelField(Text($"パーフェクトシンク用のExpression数: {defined}/52",
                $"Defined Perfect Sync expressions: {defined}/52"), EditorStyles.wordWrappedLabel);
            var style = new GUIStyle(EditorStyles.wordWrappedLabel);
            if (effective == 52)
                style.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.48f, 0.87f, 0.52f) : new Color(0.09f, 0.42f, 0.16f);
            using (new EditorGUI.IndentLevelScope())
                EditorGUILayout.LabelField(Text($"有効なセットアップのExpression数: {effective}/52",
                    $"Expressions with a valid binding: {effective}/52"), style);

            int unassigned = defined - effective;
            if (unassigned == 0) return;
            var message = Text($"定義済み{defined}件のうち、{unassigned}件に有効な割り当てがありません。",
                $"{unassigned} of {defined} defined expressions have no usable binding.");
            if (unassigned * 2 > defined)
            {
                message += Text(
                    "\n以下のいずれかの方法でセットアップを行ってください。\n" +
                    "・ARKit に準拠した名称のブレンドシェイプをモデルに適用してから、Expressionを作成する\n" +
                    "・VRM Export Description で BlendShape を割り当てるか、AnimationClip で操作対象のブレンドシェイプを選択する",
                    "\nChoose one of the following setup methods.\n" +
                    "• Add blend shapes with ARKit names to the model, then create expressions.\n" +
                    "• Assign a BlendShape in VRM Export Description, or select the blend shapes to control in the AnimationClip.");
                style.normal.textColor = EditorGUIUtility.isProSkin ? new Color(1f, 0.71f, 0.35f) : new Color(0.59f, 0.27f, 0.04f);
            }
            EditorGUILayout.LabelField(message, style);
        }

        void CreateExpressions()
        {
            string parent = null;
            if (useClips)
            {
                parent = EditorUtility.OpenFolderPanel(Text("表情クリップの保存先（親フォルダ）を選択", "Choose a parent folder for expression clips"), Application.dataPath, "");
                if (string.IsNullOrEmpty(parent)) return;
            }
            try
            {
                result = PerfectSyncSetup.Run(avatar, parent, keepExisting ? (Func<bool>)null : ConfirmOverwrite, language == Language.Japanese);
            }
            catch (Exception exception)
            {
                result = null;
                EditorUtility.DisplayDialog(Text("Expressionを作成できません", "Could not create expressions"), exception.Message, "OK");
            }
        }

        bool ConfirmOverwrite() => EditorUtility.DisplayDialog(Text("表情の上書き", "Overwrite expressions"),
            Text("選択した方式で既存のパーフェクトシンク用 Expression の設定を上書きしますか？",
                "Overwrite the existing Perfect Sync expressions using the selected setup method?"),
            Text("上書きする", "Overwrite"), Text("キャンセル", "Cancel"));
    }
}
