# Material Override VRM for VRChat の使い方

初めての方は「パッケージをインストールする」から「まとめてエクスポートする」まで順に進めてください。

**目次**

- [パッケージをインストールする](#install)
- [プロジェクトの初回セットアップ](#project-setup)
- [`.mouv` をまとめてエクスポートする（推奨手順）](#export-avatar)
- [エクスポートの詳細設定と個別手順](#export-details)
- [パーフェクトシンク用の表情を設定する（任意）](#perfect-sync-expressions)
- [トラブルシューティング](#troubleshooting)

## このパッケージで出力する `.mouv` の特徴

本パッケージで出力する `.mouv` ファイルは以下のデータからなるファイルです。

- [NDMF VRM Exporter](https://github.com/hkrn/ndmf-vrm-exporter) によって変換したVRM相当のアバター情報
- マテリアル情報の一覧

出力の過程でVRMファイル (`.vrm`) も生成されます。

とくにマテリアル情報については、VRMへの変換直前にモデルに設定されていたマテリアルをURP向けに変換してファイルに格納します。ファイルのロード時にはこのマテリアルを適用することで、 `.mouv` ではMToon以外のシェーダーが利用できます。

上記の特徴により、 `.mouv` ファイルでは以下の表現が可能です。

- VRMで指定できる揺れものやConstraint全般の反映
- MToonに限定しないマテリアルの適用

一方、`.mouv` ファイルは下記の制約を受けます。

- URPをサポートしないシェーダーは使用できません。
- VRMのExpressionとして指定していない表情やBlendShapeを動かせることは保証されません。
- アニメーション等のデータは含まれません。

<a name="install"></a>

## パッケージをインストールする

- [baxter's VPM Packages](https://malaybaku.github.io/baku-vpm/) をVCC / ALCOMへ追加します。
- 本パッケージ (`Material Override VRM for VRChat`) を指定してインストールします。

<a name="project-setup"></a>

## プロジェクトの初回セットアップ

### セットアップウィンドウを開く

Unity Editorで `Tools > Material Override VRM > For VRChat: Setup` を選択してセットアップウィンドウを開きます。

このセットアップウィンドウで必要なパッケージがインストールされているか確認します。

### 必要に応じてパッケージをインストールする

とくに [UniVRM](https://github.com/vrm-c/UniVRM) と [NDMF VRM Exporter](https://github.com/hkrn/ndmf-vrm-exporter) がインストールされていない場合、それぞれ案内に沿ってインストールを行います。

NDMF VRM ExporterはVCC / ALCOMから導入します。UniVRMは以下いずれかの方法でインストールします。

unitypackageを使って導入する場合:

1. [UniVRM v0.131.4 の公式リリース](https://github.com/vrm-c/UniVRM/releases/tag/v0.131.4)を開きます。
2. リリースページの `Installation > UnityPackage` で `VRM 1.0: Download` の箇所から unitypackageファイル(`VRM-0.131.4_efd2.unitypackage`)をダウンロードします。
3. ダウンロードしたファイルを Project ウィンドウにドラッグし、Importを実行します。

インポート後にコンパイルエラーが出た場合は、[UniVRMの更新時にコンパイルエラーが発生した](#univrm-compile-error) を参照してください。

UPM (Unity Package Manager)にgit URLを指定して導入する場合:

1. `Window > Package Manager` でパッケージマネージャーのウィンドウを開きます。
2. `[+] > Add package from git URL...` で、下記のURLを1つずつ追加します。

```
https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.4
https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.4
```

### セットアップの完了確認

上記のインストールを行ったのち、セットアップウィンドウ上に `セットアップ完了` が表示されることを確認します。もし表示が更新されない場合は「依存パッケージの状態」の `パッケージを再確認` を選択します。

`セットアップ完了` が表示されたら、[アバターの準備とエクスポート](#export-avatar) に進みます。

<a name="export-avatar"></a>

## `.mouv` をまとめてエクスポートする（推奨手順）

### 対象アバターを準備する

変換元となるアバターをシーン上に配置します。このアバターには `NDMF VRM Exporter` 用のエクスポート設定が指定されている必要があります。VRMへの変換設定の詳細は [NDMF VRM Exporter](https://github.com/hkrn/ndmf-vrm-exporter) の説明を参照してください。

本パッケージを使ううえでの `NDMF VRM Exporter` の使用については下記も注意してください。

- パーフェクトシンクを使う場合は、出力前に [パーフェクトシンク用の表情を設定する](#perfect-sync-expressions) を参照してください。
- VRM Export Descriptionの `MToon Options` は基本的にVRM (`.vrm`) の出力にのみ影響し、 `.mouv` での見た目には影響しません。ただし `.mouv` についても、VRM側のMToonをそのまま使うようセットアップされたマテリアルでは影響を受けます。

### エクスポートウィンドウを開く

セットアップウィンドウの `エクスポート画面を開く` を選択します。メニューバーからは `Tools > Material Override VRM > For VRChat: Export Avatar` で開けます。項目が見つからない場合は、[プロジェクトの初回セットアップ](#project-setup) を確認してください。

### エクスポートして表示を確認する

とくにアバターが lilToon のマテリアルのみを使用している場合は、まとめてエクスポートする方法を推奨します。

1. `エクスポート方法` で `まとめてエクスポート` を選び、`元アバター` にシーン上の対象アバターを指定します。
2. `エクスポート` を実行して保存先を選びます。同じフォルダに、同名の `.mouv` と `.vrm` が保存されます。
3. 使用予定のアプリで **`.mouv`** を開きます。アバターが意図どおり表示されたら、基本のエクスポートは完了です。

同時に保存される `.vrm` は中間ファイルです。FakeShadow などを使用していて [`.vrm` だけ見た目が崩れる場合](#intermediate-vrm-appearance) は、`.mouv` をそのまま使用できます。`.mouv` の表示を調整したい場合は、[出力した `.mouv` の見た目が元のアバターと異なる](#mouv-appearance) を参照してください。

<a name="export-details"></a>

## エクスポートの詳細設定と個別手順

出力時の設定を変更したい場合や、[ステップごとにエクスポート](#step-by-step) してマテリアルを調整したい場合に参照してください。

### まとめてエクスポートの詳細設定

詳細設定では下記のオプションが利用可能です。いずれもデフォルトでは変更不要です。

- `余剰マテリアルスロットを保持`: lilToon の FakeShadow など、同じサブメッシュに重ねて描画されるマテリアルを `.mouv` に引き継ぐオプションです。デフォルトではオンになっています。詳しくは [「余剰マテリアルスロットを保持」オプションについて](#extra-material-slots)  を参照してください。
- `エクスポートしたマテリアル情報をプロジェクトにも残す`: `.mouv` ファイルに含めたマテリアル情報をプロジェクトの `Assets` 配下に `Assets/MaterialOverrideVrm/Generated/<アバター名>` というフォルダパスでも残します。
  - 出力した `.mouv` ファイルの表示に問題がある場合の調査時などにオンにします。
  - 出力前にマテリアルを調整する場合は、[マテリアル情報（Material Snapshot）を編集する](#edit-material-snapshot) を参照してください。
- `エクスポート後もURPを維持`: エクスポート後、プロジェクトのRender PipelineをURPに変更した状態を維持します。
  - URPでのマテリアル表示に問題がある場合の調査などで、必要に応じてオンにします。
  - このオプションを有効にした場合、後で `Project Settings > Graphics > Scriptable Render Pipeline Settings` を `None` にするとRender PipelineをBuilt-in RPに変更します。
- `エクスポート用URP`: この設定は高度なデバッグ目的で用意されているため、通常は未指定のままにしてください。

<a name="step-by-step"></a>

### ステップごとにエクスポートする

推奨手順でのエクスポートに問題がある場合、この方法でエクスポートを行います。この手順ではマテリアルを調整できるタイミングがあるため、URP対応で手作業が必要な場合に利用できます。

`エクスポート方法` を `ステップごとエクスポート` に指定し、後述の2つのステップに分けて `.vrm` ファイルと `.mouv` ファイルを出力します。

#### Step1：VRM (.vrm) を出力

以下の手順でVRMファイルを出力します。

- `元アバター` に、対象となるアバターを指定します。
- その後、`マテリアル保存を有効にする` を実行し、`マテリアルの保存: セットアップ済み` という表示になることを確認します。
- 必要に応じて、Step1 の [余剰マテリアルスロットを保持](#extra-material-slots) を切り替えます。VRM出力後に設定を変えた場合は、Step1からやり直してください。
- `NDMF VRM ExporterでVRMを出力` にある `Export` を選択し、VRMを出力します。このVRMはStep2で参照するため、プロジェクトの `Assets` 以下のフォルダに保存します。

VRMの出力とあわせて、アバターのマテリアルおよびテクスチャの情報が `Assets/MaterialOverrideVrm/Generated/MaterialSnapshots/<アバター名>_<id>` のようなパスのフォルダ以下に出力されます。

この出力先は同名のアバターを繰り返し出力すると上書きされます。手でマテリアル情報を編集したい場合、[マテリアル情報（Material Snapshot）を編集する](#edit-material-snapshot)を参照してください。

<a name="export-mouv-step"></a>

#### Step2：Material Override VRM (.mouv) を出力

以下の手順で、VRMファイルにマテリアル情報を付与した `.mouv` ファイルを出力します。

- `出力済みVRM` に、Step1で出力したVRMを指定します。この指定時にはシーン上のモデルではなく、prefabを指定します。
- `マテリアルの指定方法` を適宜選択します。デフォルトでは `自動で選択 (VRMのアバター名と一致する最新)` を指定することで、Step1で生成されたマテリアル情報が紐付けられます。
- `URPでプレビューを開始` を実行して、シーン上でマテリアルが差し替わったモデルの見え方を確認します。
  - プレビュー中はRender PipelineがURPに変更されます。プレビュー対象ではない、元からシーンに配置されていたオブジェクトについては見え方がおかしくなる場合があります。
  - エクスポート予定のモデルでシェーダーやマテリアルに起因する問題がある場合、[マテリアル情報（Material Snapshot）を編集する](#edit-material-snapshot) を参照し、シェーダーやマテリアルを調整します。
- `プレビューを終了` でプレビュー用のシーンを閉じます。
- `エクスポート` を実行します。

詳細設定では `まとめてエクスポート` で指定できる項目が部分的に利用可能です。

- `エクスポート後もURPを維持`: エクスポート後、プロジェクトのRender PipelineをURPに変更した状態を維持します。
- `エクスポート用URP`: この設定は高度なデバッグ目的で用意されているため、通常は未指定のままにしてください。

<a name="edit-material-snapshot"></a>

### マテリアル情報（Material Snapshot）を編集する

シェーダーがURP未対応の場合などではStep1の実行後、Step2のエクスポート前にマテリアルやシェーダーの調整が必要な場合があります。

下記の手順で調整とエクスポートを進めます。

- Step1でマテリアル情報が出力されたフォルダ (`Assets/MaterialOverrideVrm/Generated/MaterialSnapshots/<アバター名>_<id>`)を、フォルダごと別の場所に移動・リネームして退避します。
- Step2の `マテリアルの指定方法` を `手動で選択` に変更し、退避したフォルダに含まれる `Snapshot.asset` を指定します。
- `URPでプレビューを開始` を実行します。
  - この状態で、退避したフォルダに含まれるマテリアルのプロパティ等を調整します。
- `プレビューを終了` を実行します。
- `エクスポート` を実行します。

プレビューを行ったままマテリアルのプロパティやシェーダーを変更するとシーンビュー上で反映されますが、`Snapshot.asset` 自体の変更は反映されません。マテリアルそのものを差し替えたり、 `Use Vrm Material` オプションのオンオフを切り替えた場合は、プレビューを一度終了し再び開始してください。

シェーダーの一部でURP対応が難しい場合、`Snapshot.asset` のInspectorで `Entries` を開き、対象のシェーダーを利用しているマテリアルの要素について `Use Vrm Material` をオンにすると、そのマテリアルは使用されず、VRMファイルに指定されたマテリアル(MToon)をそのまま使用する設定にできます。この方法でシェーダーの互換性に対処する場合、VRM Export Descriptionの `MToon Options` が `.mouv` ファイルでの見え方にも影響を与えるため、必要に応じて調整してください。

もしプレビュー用の`Untitled`シーンが残ってしまった場合、ヒエラルキー上で該当のシーンを保存せずに閉じます。また、Render Pipelineの設定が戻っていない場合は `Project Settings > Graphics` および `Quality` でRender Pipeline設定を開始前の状態に戻してください。

<a name="extra-material-slots"></a>

### 「余剰マテリアルスロットを保持」オプションについて

lilToon の FakeShadow で前髪の影を作るときなど、同一のメッシュに対して重ね描きをする狙いでサブメッシュ数よりも多数のマテリアルを割り当てるセットアップをすることがあります。本パッケージではこのような、サブメッシュ数を超える数で割り当てたマテリアルを「余剰マテリアル」と呼んでいます。

`.mouv` を作成するとき、余剰マテリアルの情報を割り当てられるようにするためには `.vrm` の生成時点で追加的な加工が必要となるため、本オプションではこの加工を行うかどうかを選択します。

オプションによって、下記のように挙動が異なります。

- **オン（デフォルト）**: 上述した余剰マテリアルが `.mouv` に引き継げるよう、`.vrm` の作成時点でモデルに加工を行います。この処理により、 `.mouv` の見た目がよりUnity Editor上の見た目と揃うかわり、中間出力の `.vrm` をVRM対応アプリで表示した際の見た目が不自然になることがあります。
- **オフ**: 余剰マテリアルを引き継ぐための補正を省きます。中間出力の `.vrm` の見た目が崩れにくくなる代わり、`.mouv` に対して FakeShadow 等のマテリアルが適用されなくなるため、見た目の一致度が下がります。

### 必要に応じてエクスポート用のコンポーネントを削除する

`まとめてエクスポート` によるエクスポートを一度実行するか、または `ステップごとにエクスポート` のStep1を実行すると、出力元アバターに `Prepare Materials for MOUV Export` コンポーネントがアタッチされ、NDMF VRM Exporterによる変換前にマテリアルのスナップショット情報を保存するようになります。この挙動を停止させるには、同コンポーネントの `Capture` をオフにするか、コンポーネントを削除してください。

<a name="perfect-sync-expressions"></a>

## パーフェクトシンク用の表情を設定する（任意）

パーフェクトシンクの概要は [VMagicMirror の説明](https://malaybaku.github.io/VMagicMirror/tips/perfect_sync/) を参照してください。パーフェクトシンクとはARKit準拠の表情ブレンドシェイプに基づいて高度にアバター表情を動かす機能であり、VRMの非公式な機能の通称です。

### 本パッケージでサポートしている機能

本パッケージでは、モデルに定義済みのブレンドシェイプをVRM出力の表情(Expression)に割り当てる操作をサポートしています。ブレンドシェイプ自体を新規に作成・編集する機能は含みません。

- モデルにARKit準拠の名称のブレンドシェイプがある場合、そのブレンドシェイプをパーフェクトシンク用のExpression群としてセットアップします。例えば、 [Vtuber ARKit52 FacialTracking BlendShapeKey AddOn（BOOTH）](https://charlotta.booth.pm/items/6961828) では対応アバターにARKit向けのブレンドシェイプ群を適用できます。具体的な対応アバターや利用条件については同ページを確認してください。
- ARKitではなく、例えば `VRCFaceTracking（VRCFT）` に対応したモデルがある場合、本パッケージの機能でパーフェクトシンク用のExpressionを一括生成したのち、`VRM Export Description` コンポーネント上でBlendShapeを割り当てていくことができます。表情の対応表は [VRCFaceTrackingのドキュメント](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/compatibility/arkit#arkit-comparison) 等で確認できます。

### セットアップ方法

`Tools > Material Override VRM > For VRChat (Util): VRM Perfect Sync Setup...` から、パーフェクトシンク表情設定ウィンドウを開きます。

- `対象アバター` に、`VRC Avatar Descriptor` および `VRM Export Description` がアタッチされたシーン上のアバターを指定します。
- `Expressionを作成` を選択します。

`Expressionを作成` を行うと `VRM Export Description` の `Expressions > Custom` にパーフェクトシンク用のExpressionが一括で生成されます。このとき、モデルに `eyeBlinkLeft` など、ARKitに準拠した名称のブレンドシェイプがあれば、Expressionに自動で紐づけられます。この方法で紐づけるブレンドシェイプが見つからない場合、空のExpressionが作成されます。

セットアップ方法については下記の2種類をサポートしています。いずれの場合も、セットアップ結果を確認・調整する場合は [NDMF VRM Exporterのドキュメント](https://github.com/hkrn/ndmf-vrm-exporter/blob/main/docs~/component.md#expressions) を参照してください。

- `BlendShape を直接割り当てる (推奨)`: Expressionごとに単一のブレンドシェイプを紐づけます。セットアップが簡易であり、かつAvatar Optimizerとの相性がよいため、このオプションを推奨しています。
- `AnimationClip を作成する`: 各Expressionに対応した `AnimationClip` を作成します。ブレンドシェイプを弱めて適用したい場合や、1つのExpressionで複数のBlendShapeを動かしたい場合はこちらを選択します。

`詳細` では下記の機能が利用できます。

- `定義済みのExpressionは変更しない`: このオプションをオンにして `Expressionを作成` を行うと、未定義のExpressionだけ生成し、既存のExpressionを上書きしなくなります。一部のExpressionだけを手作業で削除しており、それを再生成させたい場合などに使用します。
- `パーフェクトシンク用 Expression を削除`: パーフェクトシンク用のExpressionを削除します。セットアップを最初からやり直したい場合に使用します。

<a name="troubleshooting"></a>

## トラブルシューティング

<a name="mouv-appearance"></a>

### 出力した `.mouv` の見た目が元のアバターと異なる

この場合、まずはUnity上の元アバターと、使用予定のアプリで読み込んだ `.mouv` を比較します。気になる箇所に応じて下記を確認します。

- **色・透明度・影が異なる場合**は、必要に応じて [Step2 の URP プレビュー](#export-mouv-step) で同じ箇所を確認します。プレビューでも再現する場合は、保存したマテリアルやシェーダーの設定を確認します。アプリでだけ再現する場合は、アプリ側の描画設定や照明などを確認します。
- **メッシュの一部が欠ける、形状が崩れる場合**は、中間出力の `.vrm` も開いて、該当メッシュが存在するか、形状が崩れているかを確認します。この比較ではメッシュの有無と形状を主に確認します。

問題が FakeShadow を設定した前髪など、同一のメッシュに重ね描きを行っている箇所に限られる場合は、`余剰マテリアルスロットを保持` オプションをオフにして別名で再出力し、オンのときとオフのときの `.mouv` を同じアプリで比較してください。ステップごとのエクスポートでは、設定を切り替えて Step1 からやり直します。ただし、 `余剰マテリアルスロットを保持` がオフの場合、重ね描きによる表現は反映されなくなります。

<a name="univrm-compile-error"></a>

### UniVRMの更新時にコンパイルエラーが発生した

プロジェクトにインストール済みだがバージョンが古いUniVRMが存在し、それを `.unitypackage` で更新した場合、コンパイルエラーが発生することがあります。

この場合、下記の手順でUniVRMを再インストールしてください。

- いったんUnityのプロジェクトを閉じます。
- プロジェクトフォルダの `Packages` フォルダを開きます。
-  `com.vrmc.gltf` フォルダと `com.vrmc.vrm` フォルダを削除します。
- Unityのプロジェクトを再び開き、ダウンロードしてあるunitypackageを再度ProjectウィンドウにドラッグしてImportします。

### VMagicMirrorでアバターのメッシュが表示されない（lilToon）

`.mouv` をアプリケーションで読み込んだとき、アバターのメッシュが表示されず、影やポストプロセスによるリムライト等だけが見える場合があります。

とくにliltoonを使用している場合、Unity Editor上で手動でRender PipelineをBuilt-in RPからURPへ切り替えたあとで `.mouv` をエクスポートすると本事象が起きることがあります。

本ツールはエクスポート時にRender Pipelineを切り替える場合にはlilToonシェーダーを自動で再生成しますが、あらかじめRender PipelineがURPである場合この再生成が省略されます。

この場合は、URPが有効な状態でUnityの `Assets > lilToon > [Shader] Refresh shaders` を実行したのち、 `.mouv` を再エクスポートします。ステップごとにエクスポートしている場合は [Step2](#export-mouv-step) のみを再実施します。

### 別のOSで `.mouv` を正常に読み込めない

`.mouv` に含まれるAssetBundleとシェーダーは、エクスポート時にUnity Editorで有効な `Target Platform` 向けにビルドされます。たとえば、macOSをターゲットにして出力した `.mouv` はWindows環境では正常に読み込めません。

Macで作業してWindows向けにファイルをエクスポートする場合、Unityの `File > Build Settings` で `PC, Mac & Linux Standalone` の `Target Platform` を `Windows` に設定します。`Switch Platform` で切り替えが完了してから `.mouv` を再エクスポートしてください。

<a name="intermediate-vrm-appearance"></a>

### `.mouv` の見た目は意図どおりだが、同時出力した `.vrm` の見た目が崩れている

とくに `余剰マテリアルスロットを保持` をオンにして FakeShadow などの重ね描き情報を `mouv` に引き継いだ場合、中間出力の `.vrm` に重ね描き用のメッシュやマテリアル情報が追加され、これをStandardシェーダーやMToonシェーダーで描画してしまう事により、見た目がかえって悪化することがあります。

`.mouv` が使用予定のアプリで意図どおり表示されていれば、その `.mouv` をそのまま使用できます。

`.vrm` の見た目を改善したい場合、`.mouv` ファイルは別途退避したのち、`余剰マテリアルスロットを保持` オプションをオフにして再びエクスポートを行い、 `.vrm` を再取得します。
