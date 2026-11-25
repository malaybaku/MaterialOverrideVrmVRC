using UnityEditor;
using UnityEngine;

namespace Baku.MaterialOverrideVrmVRC
{
    public static class MouvWindowHeader
    {
        public static T DrawControls<T>(string guideLabel, string guideUrl, T language) where T : System.Enum
        {
            using var row = new EditorGUILayout.HorizontalScope();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Language", GUILayout.ExpandWidth(false));
            language = (T)EditorGUILayout.EnumPopup(language, GUILayout.Width(85));
            GUILayout.Space(6);
            GUILayout.Label("/", GUILayout.ExpandWidth(false));
            GUILayout.Space(6);
            var guide = new GUIContent(guideLabel, guideUrl);
            if (EditorGUILayout.LinkButton(guide, GUILayout.Width(EditorStyles.linkLabel.CalcSize(guide).x)))
                Application.OpenURL(guideUrl);
            return language;
        }

        public static void Draw(string title)
        {
            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            GUILayout.Label("Material Override VRM for VRChat", new GUIStyle(EditorStyles.miniLabel) { fontSize = 12 });
            GUILayout.Label(title, new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, wordWrap = true });
            GUILayout.Space(EditorGUIUtility.singleLineHeight);
        }
    }
}
