# エクスポート処理の拡張

Editorコードから `Baku.MaterialOverrideVrmVRC.MouvExportExtensions.Register` を呼ぶことで、両方のエクスポート方法に処理を追加できます。独自asmdefを使う場合は `MaterialOverrideVrmVRC.Editor` を参照してください。依存パッケージが揃ってからコンパイルする必要があります。

## 出力前の処理

`beforeExport` はURPの準備後、Coreによる `.mouv` 出力前に呼ばれます。最初からURPのプロジェクトでも呼ばれます。コールバックはEditorのメインスレッドで順番に実行し、返されたTaskの完了を待ちます。

`context.Snapshot` は保存済みSnapshotそのものです。マテリアルの変更や参照の差し替えを直接行えます。各コールバックの完了後、Snapshotの対応表を出力用オブジェクトへ再適用します。対応表のRendererPath・Slot等は維持してください。変更したMaterial等を保存する場合は、そのアセットに `EditorUtility.SetDirty` を呼んでください。Snapshot自体は本パッケージが保存します。

変更はロールバックしません。繰り返し出力されても適切に動く処理にしてください。一時Snapshotを使う場合は、そのSnapshotと同じ一時フォルダ内へ生成アセットを保存すれば出力終了時に一緒に削除されます。別フォルダのアセットは自動削除されません。

```csharp
using System.Threading.Tasks;
using Baku.MaterialOverrideVrmVRC;
using UnityEditor;

[InitializeOnLoad]
static class MyMaterialExportExtension
{
    static readonly System.IDisposable registration;

    static MyMaterialExportExtension()
    {
        registration = MouvExportExtensions.Register(context =>
        {
            if (!context.Snapshot) return Task.CompletedTask;
            foreach (var entry in context.Snapshot.Entries)
            {
                if (!entry.Material) continue;
                // ここで必要なシェーダーやプロパティの変換を行います。
                // 同じMaterialが複数エントリに登場する場合もあります。
                EditorUtility.SetDirty(entry.Material);
            }
            return Task.CompletedTask;
        });
    }
}
```

登録はDomain Reloadまで有効です。戻り値の `Dispose()` で解除できます。実行開始時に登録一覧を固定するため、途中の登録・解除は次回の出力から反映されます。`order` は小さい順で、同値は登録順です。組み込みのlilToon再生成は `1000` なので、通常の変換処理はデフォルトの `0` でその前に実行できます。

## 終了時の処理

任意の `afterExport` は、出力処理と必要なRender Pipeline復帰が終わった後に、出力前とは逆順で呼ばれます。途中で失敗したコールバック自身にも呼ばれ、まだ開始していないコールバックには呼ばれません。出力前後で必要な情報は `context.Items` に独自のキーで保管できます。

- `PipelineChanged`：今回Built-inからURPへの切り替えが必要だったか。
- `PipelineRestored`：元のRender Pipelineへ戻したか。URP維持設定で成功した場合はfalseです。
- `ExportSucceeded`：Coreによる `.mouv` 出力が成功したか。後続のVRMコピー等の成否は含みません。
- `Source`：今回のCore出力用オブジェクト。コールバックの外へ保持しないでください。
- `OutputPath`：`.mouv` の出力先。

出力前の例外は出力を中止し、Pipeline復帰と終了処理へ進みます。終了処理の例外があっても、残りの終了処理は実行します。既に出力が失敗している場合は元の例外を優先し、終了処理の例外をConsoleへ記録します。出力成功後に終了処理だけが失敗した場合はエラーを通知しますが、作成済み `.mouv` は残ります。

lilToonも同じ仕組みに登録しています。Pipelineを切り替えた場合に出力前のシェーダー再生成を行い、元のPipelineへ戻した場合に終了処理で再生成します。

`MouvExportWindow.ExportAsync` を直接呼ぶ場合は、任意の第5引数にSnapshotを渡せます。省略した呼び出しでは `context.Snapshot` はnullです。
