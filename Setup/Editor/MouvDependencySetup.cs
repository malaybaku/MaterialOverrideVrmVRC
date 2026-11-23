using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Baku.MaterialOverrideVrmVRC.Setup
{
    /// <summary>Read-only dependency checks, independent of the exporter assemblies.</summary>
    [InitializeOnLoad]
    public static class MouvDependencySetup
    {
        public sealed class Requirement
        {
            public readonly string Name, DisplayName, Minimum, ExclusiveMaximum, GitUrl;
            public Requirement(string name, string displayName, string minimum, string maximum = null, string gitUrl = null)
            { Name = name; DisplayName = displayName; Minimum = minimum; ExclusiveMaximum = maximum; GitUrl = gitUrl; }
            public bool Accepts(string version) => version != null && !version.Contains("-") &&
                Version.TryParse(version, out var parsed) && parsed >= Version.Parse(Minimum) &&
                (ExclusiveMaximum == null || parsed < Version.Parse(ExclusiveMaximum));
        }

        // The suggested install version is fixed; newer UniVRM versions have no upper bound.
        public static readonly Requirement[] Requirements = {
            new Requirement("com.vrmc.gltf", "UniGLTF", "0.131.4", gitUrl: "https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.4"),
            new Requirement("com.vrmc.vrm", "VRM-1.0", "0.131.4", gitUrl: "https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.4"),
            new Requirement("com.unity.render-pipelines.universal", "Universal RP", "14.0.10", "15.0.0"),
            new Requirement("com.baxter.material-override-vrm", "Material Overridable VRM (Core Library)", "0.3.4", "0.4.0"),
            new Requirement("nadena.dev.ndmf", "NDMF", "1.14.0", "2.0.0"),
            new Requirement("com.github.hkrn.ndmf-vrm-exporter", "NDMF VRM Exporter", "1.4.0"),
            new Requirement("com.vrchat.avatars", "VRChat SDK - Avatars", "3.7.0")
        };
        static PackageInfo[] packages = Array.Empty<PackageInfo>();
        public static string[] Blockers { get; private set; } = Array.Empty<string>();
        public static string[] EnglishBlockers { get; private set; } = Array.Empty<string>();
        public static bool IsReady { get; private set; }
        public static string InstalledVersion(string name) => packages.FirstOrDefault(p => p.name == name)?.version;

        static MouvDependencySetup() => EditorApplication.delayCall += Refresh;

        public static void Refresh()
        {
            packages = PackageInfo.GetAllRegisteredPackages() ?? Array.Empty<PackageInfo>();
            var blockers = new List<string>();
            var englishBlockers = new List<string>();
            foreach (var required in Requirements)
            {
                var installed = InstalledVersion(required.Name);
                if (installed != null && !required.Accepts(installed))
                {
                    blockers.Add(required.DisplayName + " " + installed + " は対応範囲外です。");
                    englishBlockers.Add(required.DisplayName + " " + installed + " is outside the supported range.");
                }
            }
            var legacy = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => new[] { "UniGLTF", "UniGLTF.Utils", "VRM", "VRM10", "MaterialOverridableVrm.Runtime", "MaterialOverridableVrm.Editor" }
                    .Contains(Path.GetFileNameWithoutExtension(p))).ToArray();
            if (legacy.Length > 0)
            {
                blockers.Add("Assets内にUniVRMまたはMaterialOverrideVrmがあります。重複導入せず、既存の導入方式を確認してください: " + string.Join(", ", legacy));
                englishBlockers.Add("UniVRM or MaterialOverrideVrm was found under Assets. Check the existing installation before adding duplicate packages: " + string.Join(", ", legacy));
            }
            Blockers = blockers.ToArray();
            EnglishBlockers = englishBlockers.ToArray();
            IsReady = blockers.Count == 0 && Requirements.All(r => r.Accepts(InstalledVersion(r.Name)));
        }

        public static void CopyGitUrl(Requirement requirement)
        {
            if (requirement == null || string.IsNullOrEmpty(requirement.GitUrl))
                throw new ArgumentException("UniVRMの導入URLを指定してください。", nameof(requirement));
            EditorGUIUtility.systemCopyBuffer = requirement.GitUrl;
        }
    }

    public sealed class MouvSetupWindow : EditorWindow
    {
        const string WindowTitle = "Setup: Material Override VRM for VRChat";
        const string UsageUrl = "https://github.com/malaybaku/MaterialOverrideVrmVRC/blob/main/README_usage.md";
        enum Language { Japanese, English }
        [SerializeField] Language language;
        Vector2 scroll;
        string copiedPackage;
        bool showUniVrmPackageInstructions;
        bool showUniVrmGitInstructions;
        bool showNdmfInstructions;
        string Text(string japanese, string english) => language == Language.Japanese ? japanese : english;

        [MenuItem("Tools/Material Override VRM/For VRChat: Setup", false, 990)]
        public static void Open()
        {
            MouvDependencySetup.Refresh();
            GetWindow<MouvSetupWindow>(WindowTitle);
        }
        void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(600, 500);
            MouvDependencySetup.Refresh();
        }
        void OnFocus() => MouvDependencySetup.Refresh();
        void OnInspectorUpdate() => Repaint();
        void OnGUI()
        {
            var windowPadding = new GUIStyle { padding = new RectOffset(15, 15, 6, 6) };
            var sectionPadding = new GUIStyle { padding = new RectOffset(15, 0, 0, 0) };
            using var windowScroll = new EditorGUILayout.ScrollViewScope(scroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            scroll = windowScroll.scrollPosition;
            using (new EditorGUILayout.VerticalScope(windowPadding))
            {
                var usageLink = new GUIContent(Text("使い方を見る(GitHub)", "View usage guide (GitHub)"), UsageUrl);
                if (EditorGUILayout.LinkButton(usageLink, GUILayout.Width(EditorStyles.linkLabel.CalcSize(usageLink).x)))
                    Application.OpenURL(UsageUrl);
                GUILayout.Space(EditorGUIUtility.singleLineHeight * 0.3f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Language", GUILayout.Width(65));
                    GUILayout.FlexibleSpace();
                    language = (Language)EditorGUILayout.EnumPopup(language, GUILayout.Width(125));
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(Text("セットアップ状態", "Setup status"), EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(sectionPadding))
                {
                    var setupStatusStyle = new GUIStyle(EditorStyles.boldLabel);
                    setupStatusStyle.normal.textColor = StatusColor(MouvDependencySetup.IsReady);
                    EditorGUILayout.LabelField(MouvDependencySetup.IsReady
                        ? Text("セットアップ完了", "Setup complete")
                        : Text("セットアップが必要です。", "Action Required: Setup incomplete"),
                        setupStatusStyle);
                    var buttonRect = EditorGUILayout.GetControlRect(false, 48);
                    buttonRect.width = (buttonRect.width - 8) / 2;
                    using (new EditorGUI.DisabledScope(!MouvDependencySetup.IsReady || EditorApplication.isCompiling || EditorApplication.isUpdating))
                    {
                        if (GUI.Button(buttonRect, Text("エクスポート画面を開く", "Open exporter")))
                            EditorApplication.ExecuteMenuItem("Tools/Material Override VRM/For VRChat: Export Avatar");
                    }
                }

                EditorGUILayout.Space();
                var uniVrmReady = PackagesReady("com.vrmc.gltf", "com.vrmc.vrm");
                DrawInstallHeader(Text("UniVRMをインストール", "Install UniVRM"), uniVrmReady);
                using (new EditorGUILayout.VerticalScope(sectionPadding))
                {
                    if (uniVrmReady)
                        EditorGUILayout.LabelField(Text("インストール済みです。", "Already installed."));

                    showUniVrmPackageInstructions = EditorGUILayout.Foldout(showUniVrmPackageInstructions,
                        Text("インストール手順 (unitypackage で導入)", "Installation instructions (unitypackage)"), true);
                    if (showUniVrmPackageInstructions)
                    {
                        using (new EditorGUILayout.VerticalScope(sectionPadding))
                            DrawUniVrmPackageInstructions();
                    }

                    GUILayout.Space(8);
                    showUniVrmGitInstructions = EditorGUILayout.Foldout(showUniVrmGitInstructions,
                        Text("インストール手順 (Git URL で導入)", "Installation instructions (Git URLs)"), true);
                    if (showUniVrmGitInstructions)
                    {
                        using (new EditorGUILayout.VerticalScope(sectionPadding))
                            DrawUniVrmGitInstructions();
                    }
                }

                EditorGUILayout.Space();
                var ndmfReady = PackagesReady("nadena.dev.ndmf", "com.github.hkrn.ndmf-vrm-exporter");
                DrawInstallHeader(Text("NDMF VRM Exporterをインストール", "Install NDMF VRM Exporter"), ndmfReady);
                using (new EditorGUILayout.VerticalScope(sectionPadding))
                {
                    if (ShowInstructions(ndmfReady, ref showNdmfInstructions))
                    {
                        using (new EditorGUILayout.VerticalScope(sectionPadding))
                            DrawNdmfInstructions();
                    }
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(Text("依存パッケージの状態", "Dependency status"), EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(sectionPadding))
                {
                    using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || EditorApplication.isUpdating))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var label = Text("パッケージを再確認", "Recheck packages");
                            if (GUILayout.Button(label, GUILayout.Width(GUI.skin.button.CalcSize(new GUIContent(label)).x + 16)))
                                MouvDependencySetup.Refresh();
                            GUILayout.FlexibleSpace();
                        }
                    }
                    DrawPackageStatus();
                    var blockers = language == Language.Japanese ? MouvDependencySetup.Blockers : MouvDependencySetup.EnglishBlockers;
                    foreach (var blocker in blockers) EditorGUILayout.HelpBox(blocker, MessageType.Warning);
                }
            }
        }

        bool PackagesReady(params string[] names) => names.All(name =>
            MouvDependencySetup.Requirements.First(r => r.Name == name).Accepts(MouvDependencySetup.InstalledVersion(name)));

        void DrawInstallHeader(string title, bool ready)
        {
            var style = new GUIStyle(EditorStyles.boldLabel);
            if (!ready)
            {
                style.normal.textColor = StatusColor(false);
                title += Text(" (操作が必要です)", " (Action required)");
            }
            EditorGUILayout.LabelField(title, style);
        }

        bool ShowInstructions(bool installed, ref bool expanded)
        {
            if (!installed)
            {
                expanded = false;
                return true;
            }
            EditorGUILayout.LabelField(Text("インストール済みです。", "Already installed."));
            expanded = EditorGUILayout.Foldout(expanded, Text("インストール手順", "Installation instructions"), true);
            return expanded;
        }

        static void DrawInstructionBox(params string[] lines) => DrawInstructionBox(null, lines);

        static void DrawInstructionBox(Action drawFirstLine, params string[] lines)
        {
            var boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(0, 0, 4, 8)
            };
            var textStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
            using (new EditorGUILayout.VerticalScope(boxStyle))
            {
                drawFirstLine?.Invoke();
                for (var i = 0; i < lines.Length; i++)
                {
                    if (i > 0 || drawFirstLine != null) GUILayout.Space(Mathf.Max(0, textStyle.lineHeight * 0.35f - EditorGUIUtility.standardVerticalSpacing));
                    // Let word wrapping determine the full height; height constraints can clip wrapped lines.
                    GUILayout.Label(lines[i], textStyle);
                }
            }
        }

        void DrawUniVrmPackageInstructions()
        {
            DrawInstructionBox(DrawReleasePageStep,
                Text("2. リリースページの「Installation > UnityPackage」にある「VRM 1.0: Download」から VRM-0.131.4_efd2.unitypackage をダウンロードします。",
                    "2. On the release page, go to Installation > UnityPackage and use the VRM 1.0: Download link to download VRM-0.131.4_efd2.unitypackage."),
                Text("3. ダウンロードしたファイルを Project ウィンドウにドラッグし、Importを実行します。",
                    "3. Drag the downloaded file into the Project window and click Import."));
        }

        void DrawReleasePageStep()
        {
            const string releaseUrl = "https://github.com/vrm-c/UniVRM/releases/tag/v0.131.4";
            var releaseLink = new GUIContent(Text("UniVRM v0.131.4 の公式リリース", "official UniVRM v0.131.4 release"), releaseUrl);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(Text("1.", "1. Open the"), GUILayout.ExpandWidth(false));
                if (EditorGUILayout.LinkButton(releaseLink, GUILayout.Width(EditorStyles.linkLabel.CalcSize(releaseLink).x)))
                    Application.OpenURL(releaseUrl);
                GUILayout.Label(Text("を開きます。", "."), GUILayout.ExpandWidth(false));
                GUILayout.FlexibleSpace();
            }
        }

        void DrawUniVrmGitInstructions()
        {
            DrawInstructionBox(
                Text("1. Window > Package Manager を開きます。", "1. Open Window > Package Manager."),
                Text("2. [+] > Add package from git URL... で、下記のURLを1つずつ追加します。",
                    "2. Use [+] > Add package from git URL... to add the URLs below one at a time."));
            foreach (var required in MouvDependencySetup.Requirements.Where(r => r.GitUrl != null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(required.DisplayName, GUILayout.Width(75));
                    EditorGUILayout.SelectableLabel(required.GitUrl, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (GUILayout.Button(Text("URLをコピー", "Copy URL"), GUILayout.Width(100)))
                    {
                        MouvDependencySetup.CopyGitUrl(required);
                        copiedPackage = required.DisplayName;
                    }
                }
            }
            if (!string.IsNullOrEmpty(copiedPackage))
                EditorGUILayout.LabelField(Text(copiedPackage + "のURLをコピーしました。", "Copied the " + copiedPackage + " URL."));
        }

        void DrawNdmfInstructions()
        {
            DrawInstructionBox(
                Text("VPM (VCC / ALCOM) から下記のパッケージを導入します。",
                    "Install the following packages via VPM (VCC / ALCOM)."),
                "• NDMF (>= 1.14)",
                "• NDMF VRM Exporter (>= 1.4)");
            const string exporterUrl = "https://github.com/hkrn/ndmf-vrm-exporter";
            using (new EditorGUILayout.HorizontalScope())
            {
                var prefix = Text("参考:", "Reference:");
                GUILayout.Label(prefix, GUILayout.Width(EditorStyles.label.CalcSize(new GUIContent(prefix)).x));
                if (EditorGUILayout.LinkButton(exporterUrl,
                    GUILayout.Width(EditorStyles.linkLabel.CalcSize(new GUIContent(exporterUrl)).x)))
                    Application.OpenURL(exporterUrl);
                GUILayout.FlexibleSpace();
            }
        }

        static Color StatusColor(bool ready) => ready
            ? (EditorGUIUtility.isProSkin ? new Color(0.4f, 0.85f, 0.45f) : new Color(0.1f, 0.5f, 0.17f))
            : (EditorGUIUtility.isProSkin ? new Color(1f, 0.7f, 0.3f) : new Color(0.65f, 0.32f, 0f));

        void DrawPackageStatus()
        {
            var statusStyle = new GUIStyle(EditorStyles.label);
            // Size the name column from its content instead of Unity's narrow default label width.
            var nameWidth = MouvDependencySetup.Requirements.Max(r => EditorStyles.label.CalcSize(new GUIContent(r.DisplayName)).x) + 12;
            foreach (var required in MouvDependencySetup.Requirements)
            {
                var installed = MouvDependencySetup.InstalledVersion(required.Name);
                var accepted = required.Accepts(installed);
                string status;
                if (installed == null) status = Text("- (未インストール)", "- (Not installed)");
                else if (accepted) status = installed + " (OK)";
                else
                {
                    var outdated = Version.TryParse(installed, out var version) && version < Version.Parse(required.Minimum);
                    status = installed + (outdated ? Text(" (要更新)", " (Update required)") : Text(" (対応外)", " (Unsupported)"));
                }
                statusStyle.normal.textColor = StatusColor(accepted);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent(required.DisplayName, required.Name), GUILayout.Width(nameWidth));
                    GUILayout.Label(status, statusStyle);
                }
            }
        }
    }
}
