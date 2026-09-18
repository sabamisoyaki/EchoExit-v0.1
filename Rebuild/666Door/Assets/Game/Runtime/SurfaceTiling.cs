using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>Per-object UV density without cloning a material for every wall segment.
    /// Property blocks are not serialized, so the tiling is stored here and reapplied whenever the renderer is enabled.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Renderer))]
    public sealed class SurfaceTiling : MonoBehaviour
    {
        public Vector2 Tiling = Vector2.one;

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        public void Apply()
        {
            var renderer = GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            var scaleOffset = new Vector4(Tiling.x, Tiling.y, 0, 0);
            properties.SetVector("_BaseMap_ST", scaleOffset);
            properties.SetVector("_MainTex_ST", scaleOffset);
            renderer.SetPropertyBlock(properties);
        }
    }
}
