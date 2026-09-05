using UnityEngine;

/// <summary>Applies a restrained atmosphere change based on the current streak.</summary>
public class ProgressionAtmosphereController : MonoBehaviour
{
    [SerializeField] private Light[] sceneLights;
    [SerializeField] private AudioSource ambience;

    private float[] baseIntensities;
    private Color[] baseColors;
    private Color baseAmbient;
    private float baseVolume;
    private float basePitch;

    private void Awake()
    {
        if (sceneLights == null || sceneLights.Length == 0)
            sceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);

        baseIntensities = new float[sceneLights.Length];
        baseColors = new Color[sceneLights.Length];
        for (int i = 0; i < sceneLights.Length; i++)
        {
            if (sceneLights[i] == null) continue;
            baseIntensities[i] = sceneLights[i].intensity;
            baseColors[i] = sceneLights[i].color;
        }

        if (ambience != null)
        {
            baseVolume = ambience.volume;
            basePitch = ambience.pitch;
        }
        baseAmbient = RenderSettings.ambientLight;
    }

    public void Apply(int streak)
    {
        int stage = streak >= 7 ? 3 : streak >= 5 ? 2 : streak >= 3 ? 1 : 0;
        float intensity = new[] { 1f, 0.9f, 0.76f, 0.62f }[stage];
        float redShift = new[] { 0f, 0.06f, 0.13f, 0.22f }[stage];

        for (int i = 0; i < sceneLights.Length; i++)
        {
            Light lightSource = sceneLights[i];
            if (lightSource == null) continue;
            lightSource.intensity = baseIntensities[i] * intensity;
            lightSource.color = Color.Lerp(baseColors[i], new Color(1f, 0.45f, 0.38f), redShift);
        }

        RenderSettings.ambientLight = Color.Lerp(baseAmbient, new Color(0.12f, 0.025f, 0.025f), redShift);
        if (ambience != null)
        {
            ambience.volume = baseVolume * Mathf.Lerp(1f, 1.15f, stage / 3f);
            ambience.pitch = basePitch * Mathf.Lerp(1f, 0.92f, stage / 3f);
        }
    }
}
