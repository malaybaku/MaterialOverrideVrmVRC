using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using nadena.dev.ndmf;
using nadena.dev.ndmf.platform;
using UnityEngine;

namespace Baku.MaterialOverrideVrmVRC
{
    internal sealed class MouvBatchExportException : InvalidOperationException
    {
        public readonly string EnglishMessage;
        public MouvBatchExportException(string japanese, string english) : base(japanese) => EnglishMessage = english;
    }

    // All access to NDMF VRM Exporter internals is confined to this adapter.
    internal sealed class MouvVrmExporterBridge
    {
        internal const string PlatformId = "com.github.hkrn.ndmf-vrm-exporter";
        readonly Type componentType;
        readonly PropertyInfo hasAuthor, hasLicense, skipExport;
        readonly MethodInfo export;
        internal INDMFPlatformProvider Platform { get; }

        MouvVrmExporterBridge(Type componentType, PropertyInfo hasAuthor, PropertyInfo hasLicense,
            PropertyInfo skipExport, MethodInfo export, INDMFPlatformProvider platform)
        {
            this.componentType = componentType; this.hasAuthor = hasAuthor; this.hasLicense = hasLicense;
            this.skipExport = skipExport; this.export = export; Platform = platform;
        }

        internal static bool TryCreate(out MouvVrmExporterBridge bridge, out string reason)
        {
            bridge = null;
            try
            {
                if (!PlatformRegistry.PlatformProviders.TryGetValue(PlatformId, out var provider))
                { reason = "NDMF VRM Exporter platform not registered."; return false; }
                return TryCreateFromAssembly(provider.GetType().Assembly, out bridge, out reason);
            }
            catch (Exception exception) { reason = exception.Message; return false; }
        }

        internal static bool TryCreateFromAssembly(Assembly assembly, out MouvVrmExporterBridge bridge, out string reason)
        {
            bridge = null;
            try
            {
                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                var platformType = assembly.GetType("com.github.hkrn.NdmfVrmExporterPlatform");
                var component = assembly.GetType("com.github.hkrn.NdmfVrmExporterComponent");
                var plugin = assembly.GetType("com.github.hkrn.NdmfVrmExporterPlugin");
                if (platformType == null || component == null || plugin == null || !typeof(Component).IsAssignableFrom(component))
                    throw new MissingMemberException("Exporter platform, component or plugin type.");
                var instance = platformType.GetField("Instance", flags);
                if (instance == null || !typeof(INDMFPlatformProvider).IsAssignableFrom(instance.FieldType))
                    throw new MissingMemberException("NdmfVrmExporterPlatform.Instance");
                var platform = instance.GetValue(null) as INDMFPlatformProvider;
                if (platform == null || platform.QualifiedName != PlatformId)
                    throw new MissingMemberException("Exporter platform instance.");
                var author = component.GetProperty("HasAuthor");
                var license = component.GetProperty("HasLicenseUrl");
                var skip = platformType.GetProperty("SkipExportingVrmFile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!IsInstanceBool(author) || !IsInstanceBool(license) || !IsInstanceBool(skip))
                    throw new MissingMemberException("Exporter validation or skip properties.");
                var method = plugin.GetMethod("ExportVrmFile", flags, null,
                    new[] { component, typeof(BuildContext), typeof(string), typeof(string) }, null);
                if (method == null || method.ReturnType != typeof(void) || method.ContainsGenericParameters)
                    throw new MissingMemberException("ExportVrmFile(component, BuildContext, string, string)");
                bridge = new MouvVrmExporterBridge(component, author, license, skip, method, platform);
                reason = "";
                return true;
            }
            catch (Exception exception) { reason = exception.Message; return false; }
        }

        static bool IsInstanceBool(PropertyInfo property) => property?.PropertyType == typeof(bool) &&
            property.GetIndexParameters().Length == 0 && property.GetGetMethod(true) != null &&
            !property.GetGetMethod(true).IsStatic;

        internal bool HasExportDescription(GameObject avatar) => avatar && avatar.GetComponent(componentType);

        internal void ValidateAvatar(GameObject avatar)
        {
            var component = avatar.GetComponent(componentType);
            if (!component)
                throw new MouvBatchExportException("元アバターにNDMF VRM Exporterの設定コンポーネントを追加してください。",
                    "Add the NDMF VRM Exporter configuration component to the source avatar.");
            if (!(bool)hasAuthor.GetValue(component) || !(bool)hasLicense.GetValue(component))
                throw new MouvBatchExportException("NDMF VRM Exporterの作者とライセンスURLを設定してください。",
                    "Set the author and license URL in NDMF VRM Exporter.");
            if ((bool)skipExport.GetValue(Platform))
                throw new MouvBatchExportException("NDMF VRM ExporterがVRM出力を一時停止しています。現在の処理が完了してから再実行してください。",
                    "NDMF VRM Exporter is temporarily skipping VRM output. Wait for the current operation to finish.");
        }

        internal void Export(GameObject avatar, BuildContext context, string basePath, string workingDirectory)
        {
            ValidateAvatar(avatar);
            try { export.Invoke(null, new object[] { avatar.GetComponent(componentType), context, basePath, workingDirectory }); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
        }
    }
}
