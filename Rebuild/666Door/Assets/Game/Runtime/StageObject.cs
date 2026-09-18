using Door666.Core;
using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>Placed data and its visual identity. Normal objects have no ritual behaviour.</summary>
    [DisallowMultipleComponent]
    public sealed class StageObject : MonoBehaviour
    {
        public StageItem Data;
        public Transform VisualRoot;
        public string PrefabId => Data == null ? string.Empty : Data.prefabId;
        public bool IsAnomaly => Data != null && Data.isAnomaly;

        public Bounds WorldBounds
        {
            get
            {
                if (TryGetComponent<Collider>(out var collider)) return collider.bounds;
                return new Bounds(transform.position, Vector3.one * 0.4f);
            }
        }
    }
}
