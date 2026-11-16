using UnityEngine;
using nadena.dev.ndmf;

namespace Baku.MaterialOverrideVrmVRC
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Material Override VRM/Capture Materials on VRM Export")]
    public sealed class CaptureMouvMaterials : MonoBehaviour, INDMFEditorOnly
    {
        [Tooltip("VRM出力直前のマテリアルとテクスチャを保存します。")]
        public bool Capture = true;
    }
}
