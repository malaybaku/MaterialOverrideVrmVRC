# Material Override VRM for VRChat

VRChat向けにセットアップしたアバターから、[VMagicMirror](https://malaybaku.github.io/VMagicMirror/)で使えるアバター形式である `.mouv`（Material Override Unity VRM）を出力するUnity Editor用パッケージです。

本パッケージを通じて、MToonに変換する前のマテリアルをモデルに適用してVMagicMirrorで表示できます。

Unity 2022.3のVRChatアバタープロジェクト向けパッケージです。

## 導入と使い方

1. [baxter's VPM Packages](https://malaybaku.github.io/baku-vpm/)をVCC / ALCOMへ追加し、本パッケージ (`Material Override VRM for VRChat`) をインストールします。
2. Unityの `Tools > Material Override VRM > For VRChat: Setup` を開き、依存パッケージのセットアップを行います。
3. `For VRChat: Export Avatar` を開き、エクスポート操作を行います。

詳しくは [README_usage](README_usage.md) ページを参照してください。

不具合報告は[GitHub Issues](https://github.com/malaybaku/MaterialOverrideVrmVRC/issues)へお願いします。

## ライセンス・依存パッケージ

本パッケージは **[zlib License](LICENSE)** で公開されています。依存パッケージには、それぞれのライセンス・利用条件が適用されます。

| パッケージ | リポジトリ |
| --- | --- |
| NDMF | [bdunderscore/ndmf](https://github.com/bdunderscore/ndmf) |
| NDMF VRM Exporter | [hkrn/ndmf-vrm-exporter](https://github.com/hkrn/ndmf-vrm-exporter) |
| UniVRM（UniGLTF / VRM 1.0） | [vrm-c/UniVRM](https://github.com/vrm-c/UniVRM) |
| VRChat SDK - Avatars | [vrchat/packages](https://github.com/vrchat/packages)（配布リポジトリ） |

また、本パッケージはUniversal Render Pipeline (URP)向けのファイルを出力するため、[lilToon](https://github.com/lilxyzw/lilToon)を使用するアバターに対してはシェーダーの再生成処理にも対応しています。

## 開発者向け

とくに本パッケージのエクスポート処理のなかでマテリアル/シェーダーをエクスポート前に調整する処理をフックしたい場合、 [エクスポート処理の拡張](https://github.com/malaybaku/MaterialOverrideVrmVRC/blob/main/README_extensions.md) を参照してください。
