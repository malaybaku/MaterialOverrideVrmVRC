using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Baku.MaterialOverrideVrmVRC
{
    public sealed class MaterialSnapshot : ScriptableObject
    {
        public string AvatarName;
        public string CapturedUtc;
        [HideInInspector] public string CaptureId;
        [HideInInspector] public string SourceAvatarGlobalId;
        [HideInInspector] public string SourceSessionId;
        [HideInInspector] public int SourceAvatarInstanceId;
        public Entry[] Entries = Array.Empty<Entry>();

        const string SessionKey = "Mouv.MaterialCapture.EditorSession";
        static string SessionId
        {
            get
            {
                var id = SessionState.GetString(SessionKey, "");
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString("N");
                    SessionState.SetString(SessionKey, id);
                }
                return id;
            }
        }

        public void SetSourceAvatar(GameObject source)
        {
            if (!source) return;
            SourceAvatarGlobalId = GlobalId(source);
            SourceSessionId = SessionId;
            SourceAvatarInstanceId = source.GetInstanceID();
        }

        public bool MatchesSourceAvatar(GameObject source)
        {
            if (!source) return false;
            var globalId = GlobalId(source);
            if (!string.IsNullOrEmpty(globalId) && !string.IsNullOrEmpty(SourceAvatarGlobalId))
                return globalId == SourceAvatarGlobalId;
            return SourceAvatarInstanceId == source.GetInstanceID() && SourceSessionId == SessionId;
        }

        static string GlobalId(GameObject source)
        {
            if (string.IsNullOrEmpty(source.scene.path)) return "";
            var id = GlobalObjectId.GetGlobalObjectIdSlow(source);
            return id.targetObjectId != 0 ? id.ToString() : "";
        }

        public bool TryGetCapturedTime(out DateTimeOffset time) => DateTimeOffset.TryParse(CapturedUtc,
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out time);

        public static MaterialSnapshot FindLatest(GameObject source)
        {
            if (!source) return null;
            return AssetDatabase.FindAssets("t:MaterialSnapshot")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.StartsWith(MouvMaterialCapture.WorkRoot + "/", StringComparison.Ordinal))
                .Select(AssetDatabase.LoadAssetAtPath<MaterialSnapshot>)
                .Where(s => s && s.MatchesSourceAvatar(source))
                .OrderByDescending(s => s.TryGetCapturedTime(out var time) ? time.UtcDateTime.Ticks : long.MinValue)
                .ThenByDescending(s => s.CaptureId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        public static MaterialSnapshot FindLatestByAvatarName(string avatarName)
        {
            if (string.IsNullOrEmpty(avatarName)) return null;
            return AssetDatabase.FindAssets("t:MaterialSnapshot")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.StartsWith(MouvMaterialCapture.WorkRoot + "/", StringComparison.Ordinal))
                .Select(AssetDatabase.LoadAssetAtPath<MaterialSnapshot>)
                .Where(s => s && string.Equals(s.AvatarName, avatarName, StringComparison.Ordinal))
                .OrderByDescending(s => s.TryGetCapturedTime(out var time) ? time.UtcDateTime.Ticks : long.MinValue)
                .ThenByDescending(s => s.CaptureId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        [Serializable]
        public sealed class Entry
        {
            public string RendererPath;
            public string RendererType;
            public int Slot;
            public string OriginalMaterialName;
            public Material Material;
        }
    }
}
