# Material Override VRM for VRChat

VRChat向けにセットアップしたアバターから、[VMagicMirror](https://malaybaku.github.io/VMagicMirror/)で使える `.mouv`（Material Override VRM）を出力するUnity Editor用パッケージです。VRMに変換したモデルと、MToonへの変換前のマテリアルを組み合わせます。

Unity 2022.3のVRChatアバタープロジェクト向けです。**現在は初回ベータ版の公開準備中です。**

## 導入と使い方

1. [baxter's VPM Packages](https://malaybaku.github.io/baku-vpm/)をVCC / ALCOMへ追加し、本パッケージをインストールします（パッケージ掲載後に利用できます）。
2. Unityの **Tools > Material Override VRM > For VRChat: Setup** を開き、依存パッケージの準備を行います。UniVRMは画面の案内に従って手動で導入します。
3. **For VRChat: Export Avatar** を開き、通常は **まとめてエクスポート** を使います。

操作手順、マテリアル情報の保管・上書きについては、**[使い方](README_usage.md)**を参照してください。

不具合報告は[GitHub Issues](https://github.com/malaybaku/MaterialOverrideVrmVRC/issues)へお願いします。[変更履歴](CHANGELOG.md)も参照できます。

## ライセンス・依存パッケージ

本パッケージのコードは **[zlib License](LICENSE)** です。依存パッケージには、それぞれのライセンス・利用条件が適用されます。詳細は各リポジトリや配布物を参照してください。

| パッケージ | リポジトリ |
| --- | --- |
| NDMF | [bdunderscore/ndmf](https://github.com/bdunderscore/ndmf) |
| NDMF VRM Exporter | [hkrn/ndmf-vrm-exporter](https://github.com/hkrn/ndmf-vrm-exporter) |
| UniVRM（UniGLTF / VRM 1.0） | [vrm-c/UniVRM](https://github.com/vrm-c/UniVRM) |
| Universal Render Pipeline | [Unity-Technologies/Graphics](https://github.com/Unity-Technologies/Graphics) |
| VRChat SDK - Avatars | [vrchat/packages](https://github.com/vrchat/packages)（配布リポジトリ） |

[lilToon](https://github.com/lilxyzw/lilToon)を使用するアバター向けのシェーダー再生成にも対応しています。lilToon自体は本パッケージの必須依存ではありません。

アバター・マテリアル・テクスチャの利用条件は、本パッケージのライセンスとは別に確認してください。

## 開発者向け

[開発・メンテナンス](MAINTENANCE.md) / [エクスポート処理の拡張](README_extensions.md)
