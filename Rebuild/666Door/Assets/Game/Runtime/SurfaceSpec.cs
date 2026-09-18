using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>Recipe for a lit surface material asset; the project bootstrap turns these into .mat files.</summary>
    public readonly struct SurfaceSpec
    {
        public readonly string Name;
        public readonly Color Color;
        public readonly float Smoothness;
        public readonly Color Emission;

        public SurfaceSpec(string name, Color color, float smoothness, Color emission = default)
        {
            Name = name;
            Color = color;
            Smoothness = smoothness;
            Emission = emission;
        }

        public bool Emissive => Emission.maxColorComponent > 0;
    }
}
