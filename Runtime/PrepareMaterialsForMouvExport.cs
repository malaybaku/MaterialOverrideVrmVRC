using UnityEngine;
using nadena.dev.ndmf;

namespace Baku.MaterialOverrideVrmVRC
{
    [DisallowMultipleComponent]
    public sealed class PrepareMaterialsForMouvExport : MonoBehaviour, INDMFEditorOnly
    {
        [Tooltip("VRM出力直前のマテリアルとテクスチャを保存します。")]
        public bool Capture = true;

        [Tooltip("サブメッシュ数を超えるマテリアルを .mouv に保持します。中間出力の VRM では見え方が変わる場合があります。")]
        public bool PreserveExtraMaterialSlots = true;
    }
}
