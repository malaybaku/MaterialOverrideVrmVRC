using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Baku.MaterialOverrideVrmVRC
{
    internal static class PerfectSyncSetup
    {
        static readonly string[] Names = {
            "browInnerUp", "browDownLeft", "browDownRight", "browOuterUpLeft", "browOuterUpRight",
            "eyeLookUpLeft", "eyeLookUpRight", "eyeLookDownLeft", "eyeLookDownRight", "eyeLookInLeft", "eyeLookInRight",
            "eyeLookOutLeft", "eyeLookOutRight", "eyeBlinkLeft", "eyeBlinkRight", "eyeSquintLeft", "eyeSquintRight",
            "eyeWideLeft", "eyeWideRight", "cheekPuff", "cheekSquintLeft", "cheekSquintRight", "noseSneerLeft",
            "noseSneerRight", "jawOpen", "jawForward", "jawLeft", "jawRight", "mouthFunnel", "mouthPucker",
            "mouthLeft", "mouthRight", "mouthRollUpper", "mouthRollLower", "mouthShrugUpper", "mouthShrugLower",
            "mouthClose", "mouthSmileLeft", "mouthSmileRight", "mouthFrownLeft", "mouthFrownRight", "mouthDimpleLeft",
            "mouthDimpleRight", "mouthUpperUpLeft", "mouthUpperUpRight", "mouthLowerDownLeft", "mouthLowerDownRight",
            "mouthPressLeft", "mouthPressRight", "mouthStretchLeft", "mouthStretchRight", "tongueOut"
        };

        internal static readonly Type DescriptionType = TypeCache.GetTypesDerivedFrom<Component>()
            .FirstOrDefault(type => type.FullName == "com.github.hkrn.NdmfVrmExporterComponent");

        internal static bool IsSceneAvatar(GameObject avatar) => MouvExportWindow.IsSceneAvatar(avatar) &&
            !EditorSceneManager.IsPreviewScene(avatar.scene);

        internal static Component Description(GameObject avatar) => DescriptionType != null && IsSceneAvatar(avatar)
            ? avatar.GetComponent(DescriptionType)
            : null;

        static SerializedProperty Field(SerializedProperty row, string name, SerializedPropertyType type)
        {
            var field = row.FindPropertyRelative(name);
            if (field == null || field.propertyType != type)
                throw new InvalidOperationException("VRM Export Description の設定形式を確認できません: " + name);
            return field;
        }

        static string ExistingName(SerializedProperty row)
        {
            var name = Field(row, "expressionName", SerializedPropertyType.String).stringValue;
            if (!string.IsNullOrEmpty(name)) return name;
            return Field(row, "baseType", SerializedPropertyType.Enum).intValue == 0
                ? Field(row, "blendShapeName", SerializedPropertyType.String).stringValue
                : Field(row, "blendShapeAnimationClip", SerializedPropertyType.ObjectReference).objectReferenceValue?.name;
        }

        static SerializedProperty CustomExpressions(SerializedObject serialized)
        {
            var rows = serialized.FindProperty("expressionCustomBlendShapes");
            if (rows == null || !rows.isArray) throw new InvalidOperationException("Custom Expression の設定形式を確認できません。");
            return rows;
        }

        internal static int DeleteExpressions(Component description)
        {
            using var serialized = new SerializedObject(description);
            var rows = CustomExpressions(serialized);
            var originalSize = rows.arraySize;
            for (int i = originalSize - 1; i >= 0; i--)
                if (Names.Contains(ExistingName(rows.GetArrayElementAtIndex(i)), StringComparer.OrdinalIgnoreCase))
                    rows.DeleteArrayElementAtIndex(i);
            int removed = originalSize - rows.arraySize;
            if (removed > 0)
            {
                Undo.SetCurrentGroupName("Delete Perfect Sync Expressions");
                serialized.ApplyModifiedProperties();
            }
            return removed;
        }

        internal static (int defined, int effective) CountExpressions(Component description)
        {
            using var serialized = new SerializedObject(description);
            var rows = CustomExpressions(serialized);

            var shapes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var renderer in description.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh;
                if (!mesh) continue;
                for (int i = 0; i < mesh.blendShapeCount; i++) shapes.Add(mesh.GetBlendShapeName(i));
            }

            var defined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows.arraySize; i++)
            {
                var row = rows.GetArrayElementAtIndex(i);
                var name = ExistingName(row);
                if (!Names.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                defined.Add(name);
                if (HasEffectiveBind(row, shapes)) effective.Add(name);
            }
            return (defined.Count, effective.Count);
        }

        static bool HasEffectiveBind(SerializedProperty row, HashSet<string> shapes)
        {
            var mode = Field(row, "baseType", SerializedPropertyType.Enum);
            if (mode.enumValueIndex < 0 || mode.enumValueIndex >= mode.enumNames.Length) return false;
            switch (mode.enumNames[mode.enumValueIndex])
            {
                case "BlendShape":
                    var shape = Field(row, "blendShapeName", SerializedPropertyType.String).stringValue;
                    return !string.IsNullOrEmpty(shape) && shapes.Contains(shape);
                case "AnimationClip":
                    var clip = Field(row, "blendShapeAnimationClip", SerializedPropertyType.ObjectReference).objectReferenceValue as AnimationClip;
                    if (!clip) return false;
                    const string prefix = "blendShape.";
                    // Exporter 1.4 resolves target names across meshes and reads non-zero pose keys at time <= 0.
                    return AnimationUtility.GetCurveBindings(clip).Any(binding =>
                        binding.propertyName.StartsWith(prefix, StringComparison.Ordinal) &&
                        shapes.Contains(binding.propertyName.Substring(prefix.Length)) &&
                        AnimationUtility.GetEditorCurve(clip, binding).keys.Any(key => key.time <= 0 && !Mathf.Approximately(key.value, 0)));
                default:
                    return false;
            }
        }

        static string CreateFolder(string parent, string name, bool japanese)
        {
            var path = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(parent, name));
            if (string.IsNullOrEmpty(path)) throw new IOException((japanese ? "フォルダを作成できません: " : "Could not create folder: ") + parent + "/" + name);
            return path;
        }

        internal static string Run(GameObject avatar, string absoluteParent, Func<bool> confirmOverwrite = null, bool japanese = true)
        {
            var component = Description(avatar);
            if (!component) throw new InvalidOperationException(japanese ? "VRM Export Description があるシーン上のアバターを指定してください。" : "Select a scene avatar with a VRM Export Description.");
            string parent = null;
            if (absoluteParent != null)
            {
                var relative = Path.GetRelativePath(Application.dataPath, absoluteParent).Replace('\\', '/').TrimEnd('/');
                if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith("../", StringComparison.Ordinal))
                    throw new ArgumentException(japanese ? "このプロジェクトの Assets 内のフォルダを選んでください。" : "Choose a folder inside this project's Assets folder.");
                if (!Directory.Exists(absoluteParent))
                    throw new DirectoryNotFoundException(japanese ? "選択したフォルダが見つかりません。" : "The selected folder no longer exists.");
                parent = relative == "." ? "Assets" : "Assets/" + relative;
                // The native folder picker can create folders before Unity imports them.
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    if (!AssetDatabase.IsValidFolder(parent))
                        throw new IOException((japanese ? "Unity がフォルダを認識できません: " : "Unity could not import the folder: ") + parent);
                }
            }

            using var serialized = new SerializedObject(component);
            var rows = CustomExpressions(serialized);
            var existing = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var duplicates = new List<int>();
            for (int i = 0; i < rows.arraySize; i++)
            {
                var name = ExistingName(rows.GetArrayElementAtIndex(i));
                if (Names.Contains(name, StringComparer.OrdinalIgnoreCase) && !existing.TryAdd(name, i))
                    duplicates.Add(i);
            }
            bool overwrite = confirmOverwrite != null && existing.Count > 0;
            if (overwrite && !confirmOverwrite()) return null;
            var pending = Names.Where(name => overwrite || !existing.ContainsKey(name)).ToArray();
            if (pending.Length == 0) return japanese ? "既存のExpressionを52件保持しました。" : "Kept all 52 existing expressions.";

            var candidates = pending.ToDictionary(name => name, _ => new List<SkinnedMeshRenderer>(), StringComparer.Ordinal);
            foreach (var renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh;
                if (!mesh) continue;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                    if (candidates.TryGetValue(mesh.GetBlendShapeName(i), out var hits)) hits.Add(renderer);
            }

            var missing = new List<string>();
            var ambiguous = new List<string>();
            string folder = null;
            try
            {
                AnimatorController controller = null;
                string clips = null;
                if (parent != null)
                {
                    var safeName = string.Concat(avatar.name.Split(Path.GetInvalidFileNameChars())).Trim(' ', '.');
                    folder = CreateFolder(parent, string.IsNullOrEmpty(safeName) ? "Avatar" : safeName, japanese);
                    clips = CreateFolder(folder, "Clips", japanese);
                    controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/PerfectSync.controller");
                    if (!controller) throw new IOException(japanese ? "編集用 Animator Controller を作成できません。" : "Could not create the editing Animator Controller.");
                }

                foreach (var name in pending)
                {
                    var hits = candidates[name];
                    var renderer = hits.Count == 1 ? hits[0] : null;
                    if (hits.Count == 0) missing.Add(name);
                    if (hits.Count > 1) ambiguous.Add(name + " (" + string.Join(", ", hits.Select(r =>
                        AnimationUtility.CalculateTransformPath(r.transform, avatar.transform))) + ")");

                    AnimationClip clip = null;
                    if (controller)
                    {
                        clip = new AnimationClip { name = name };
                        if (renderer)
                        {
                            var path = AnimationUtility.CalculateTransformPath(renderer.transform, avatar.transform);
                            clip.SetCurve(path, typeof(SkinnedMeshRenderer), "blendShape." + name, new AnimationCurve(new Keyframe(0, 100)));
                        }
                        AssetDatabase.CreateAsset(clip, clips + "/" + name + ".anim");
                        if (!AssetDatabase.Contains(clip)) throw new IOException((japanese ? "クリップを作成できません: " : "Could not create clip: ") + name);
                        controller.AddMotion(clip);
                    }

                    // Unity duplicates the last serialized array element when growing the list.
                    if (!existing.TryGetValue(name, out var i)) i = rows.arraySize++;
                    var row = rows.GetArrayElementAtIndex(i);
                    Field(row, "expressionName", SerializedPropertyType.String).stringValue = name;
                    var mode = Field(row, "baseType", SerializedPropertyType.Enum);
                    var modeIndex = Array.IndexOf(mode.enumNames, clip ? "AnimationClip" : "BlendShape");
                    if (modeIndex < 0) throw new InvalidOperationException("未対応の Expression 形式です。");
                    mode.enumValueIndex = modeIndex;
                    Field(row, "gameObject", SerializedPropertyType.ObjectReference).objectReferenceValue = avatar;
                    Field(row, "skinnedMeshRenderer", SerializedPropertyType.ObjectReference).objectReferenceValue = clip ? null : renderer;
                    Field(row, "blendShapeName", SerializedPropertyType.String).stringValue = !clip && renderer ? name : "";
                    Field(row, "blendShapeAnimationClip", SerializedPropertyType.ObjectReference).objectReferenceValue = clip;
                    foreach (var field in new[] { "optionsFoldout", "isBinary", "isPreset" })
                        Field(row, field, SerializedPropertyType.Boolean).boolValue = false;
                    foreach (var field in new[] { "overrideBlink", "overrideLookAt", "overrideMouth" })
                        Field(row, field, SerializedPropertyType.Enum).enumValueIndex = 0;
                }
                if (overwrite)
                    for (int i = duplicates.Count - 1; i >= 0; i--) rows.DeleteArrayElementAtIndex(duplicates[i]);
                if (controller) AssetDatabase.SaveAssetIfDirty(controller);
                Undo.SetCurrentGroupName("Create Perfect Sync Expressions");
                serialized.ApplyModifiedProperties();
            }
            catch
            {
                if (folder != null) AssetDatabase.DeleteAsset(folder);
                throw;
            }
            var assigned = pending.Length - missing.Count - ambiguous.Count;
            var summary = japanese
                ? $"自動割り当て {assigned}件 ／ 未一致 {missing.Count}件 ／ 複数候補 {ambiguous.Count}件"
                : $"Assigned {assigned} / Unmatched {missing.Count} / Multiple matches {ambiguous.Count}";
            if (overwrite) summary += japanese ? $" ／ 上書き {existing.Count}件" : $" / Overwritten {existing.Count}";
            else summary += japanese ? $" ／ 既存を保持 {existing.Count}件" : $" / Kept {existing.Count}";
            if (missing.Count > 0) summary += (japanese ? "\n未一致:\n" : "\nUnmatched:\n") + string.Join("\n", missing);
            if (ambiguous.Count > 0) summary += (japanese ? "\n複数候補: " : "\nMultiple matches: ") + string.Join("\n", ambiguous);
            if (folder != null) summary += (japanese ? "\n保存先: " : "\nSaved to: ") + folder;
            return summary;
        }
    }
}
