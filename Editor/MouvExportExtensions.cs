using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Baku.MaterialOverridableVrm;
using UnityEditor;
using UnityEngine;

namespace Baku.MaterialOverrideVrmVRC
{
    /// <summary>One export. Snapshot is the original captured asset, not a copy.</summary>
    public sealed class MouvExportContext
    {
        MaterialSnapshot snapshot;
        readonly string snapshotPath;
        public MaterialSnapshot Snapshot => snapshot ? snapshot :
            (snapshot = AssetDatabase.LoadAssetAtPath<MaterialSnapshot>(snapshotPath));
        public MaterialOverrideSource Source { get; }
        public string OutputPath { get; }
        public bool PipelineChanged { get; }
        public bool PipelineRestored { get; internal set; }
        public bool ExportSucceeded { get; internal set; }
        // Per-export storage shared by an extension's preparation and cleanup callbacks.
        public IDictionary<object, object> Items { get; } = new Dictionary<object, object>();

        internal MouvExportContext(MaterialSnapshot snapshot, MaterialOverrideSource source, string output, bool changed)
        {
            this.snapshot = snapshot;
            snapshotPath = snapshot ? AssetDatabase.GetAssetPath(snapshot) : "";
            Source = source; OutputPath = output; PipelineChanged = changed;
        }
    }

    /// <summary>Editor-main-thread callbacks, awaited sequentially for both export modes.</summary>
    public static class MouvExportExtensions
    {
        static readonly List<Registration> registrations = new List<Registration>();

        static MouvExportExtensions()
        {
            // Run shader regeneration after the default material-conversion callbacks.
            Register(LilToonExportExtension.BeforeExport, LilToonExportExtension.AfterExport, 1000);
        }

        /// <summary>Lower order runs first. Cleanup runs in reverse order, including a callback whose preparation failed.</summary>
        public static IDisposable Register(Func<MouvExportContext, Task> beforeExport,
            Func<MouvExportContext, Task> afterExport = null, int order = 0)
        {
            if (beforeExport == null) throw new ArgumentNullException(nameof(beforeExport));
            var registration = new Registration(beforeExport, afterExport, order);
            registrations.Add(registration);
            return registration;
        }

        internal sealed class Registration : IDisposable
        {
            internal readonly Func<MouvExportContext, Task> Before, After;
            internal readonly int Order;
            internal Registration(Func<MouvExportContext, Task> before, Func<MouvExportContext, Task> after, int order)
            { Before = before; After = after; Order = order; }
            public void Dispose() => registrations.Remove(this);
        }

        internal sealed class Session
        {
            readonly Registration[] callbacks = registrations.OrderBy(r => r.Order).ToArray();
            readonly List<Registration> entered = new List<Registration>();
            internal async Task Prepare(MouvExportContext context)
            {
                foreach (var callback in callbacks)
                {
                    entered.Add(callback);
                    await callback.Before(context);
                    if (context.Snapshot) MouvMaterialCapture.Apply(context.Snapshot, context.Source);
                }
            }
            internal async Task Finish(MouvExportContext context)
            {
                var errors = new List<Exception>();
                foreach (var callback in entered.AsEnumerable().Reverse())
                    try { if (callback.After != null) await callback.After(context); }
                    catch (Exception error) { errors.Add(error); }
                if (errors.Count == 0) return;
                if (context.ExportSucceeded) throw new AggregateException("Export extension cleanup failed.", errors);
                foreach (var error in errors) Debug.LogException(error);
            }
        }
    }

    internal static class LilToonExportExtension
    {
        static readonly object restoreKey = new object();
        internal static Task BeforeExport(MouvExportContext context)
        {
            if (context.PipelineChanged && context.Source.RendererBindings.Any(b => b?.ReplacementMaterial &&
                b.ReplacementMaterial.shader && b.ReplacementMaterial.shader.name.IndexOf("lil", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                context.Items[restoreKey] = true;
                Refresh();
            }
            return Task.CompletedTask;
        }
        internal static Task AfterExport(MouvExportContext context)
        {
            if (context.PipelineRestored && context.Items.ContainsKey(restoreKey)) Refresh();
            return Task.CompletedTask;
        }
        internal static void Refresh()
        {
            if (!EditorApplication.ExecuteMenuItem("Assets/lilToon/[Shader] Refresh shaders"))
                throw new MouvBatchExportException("lilToonのシェーダー再生成メニューを実行できませんでした。",
                    "Could not run the lilToon shader refresh menu.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
