using System.Collections.Generic;
using UnityEngine;

public enum AnomalySoundKind
{
    Recognition,
    Danger,
    ColorShift,
    Shrink,
    DollReturn,
    Scrape,
    Breath,
    Footstep
}

/// <summary>外部音源なしでも異変ごとの空間音を確認できる、MVP用の手続き生成音。</summary>
public static class ProceduralAnomalyAudio
{
    private const int SampleRate = 22050;
    private static readonly Dictionary<AnomalySoundKind, AudioClip> Clips = new();

    public static void PlayAtPosition(AnomalySoundKind kind, Vector3 position, float volume = 0.7f)
    {
        AudioSource.PlayClipAtPoint(GetClip(kind), position, Mathf.Clamp01(volume));
    }

    public static void Play(AudioSource source, AnomalySoundKind kind, float volume = 0.7f)
    {
        if (source == null) return;
        source.PlayOneShot(GetClip(kind), Mathf.Clamp01(volume));
    }

    public static AudioSource EnsureSpatialSource(GameObject host)
    {
        if (host == null) return null;

        // Unity のオブジェクトは == を「未アタッチ/破棄済みなら null」にオーバーロードしているが、
        // ?? は真の null 判定を使うためそれを迂回する。GetComponent の返す擬似 null が
        // そのまま通り、AddComponent が呼ばれずに MissingComponentException になる。
        if (!host.TryGetComponent(out AudioSource source))
        {
            source = host.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = 14f;
        return source;
    }

    private static AudioClip GetClip(AnomalySoundKind kind)
    {
        if (Clips.TryGetValue(kind, out AudioClip clip) && clip != null) return clip;

        float duration = kind switch
        {
            AnomalySoundKind.Breath => 1.4f,
            AnomalySoundKind.Scrape => 0.75f,
            AnomalySoundKind.Footstep => 0.32f,
            _ => 0.6f
        };
        int sampleCount = Mathf.CeilToInt(SampleRate * duration);
        float[] data = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;
            float normalized = i / (float)sampleCount;
            float envelope = Mathf.Sin(Mathf.PI * normalized);
            float deterministicNoise = Mathf.Sin(i * 12.9898f) * Mathf.Sin(i * 0.117f);
            data[i] = kind switch
            {
                AnomalySoundKind.Recognition =>
                    (Mathf.Sin(2f * Mathf.PI * 440f * t) + Mathf.Sin(2f * Mathf.PI * 660f * t) * 0.55f) * envelope * 0.28f,
                AnomalySoundKind.Danger =>
                    Mathf.Sin(2f * Mathf.PI * (75f + 18f * Mathf.Sin(t * 9f)) * t) * envelope * 0.5f,
                AnomalySoundKind.ColorShift =>
                    (Mathf.Sin(2f * Mathf.PI * (280f + 520f * normalized) * t) * 0.48f +
                     Mathf.Sin(2f * Mathf.PI * 910f * t) * 0.12f) * envelope,
                AnomalySoundKind.Shrink =>
                    (Mathf.Sin(2f * Mathf.PI * (170f - 95f * normalized) * t) * 0.42f +
                     deterministicNoise * 0.14f) * envelope,
                AnomalySoundKind.DollReturn =>
                    (Mathf.Sin(2f * Mathf.PI * 185f * t) * 0.25f +
                     Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 37f * t)) * 0.12f) * envelope,
                AnomalySoundKind.Scrape =>
                    (deterministicNoise * 0.5f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.25f) * envelope * 0.45f,
                AnomalySoundKind.Breath =>
                    deterministicNoise * envelope * (0.12f + 0.18f * Mathf.Sin(Mathf.PI * normalized)),
                AnomalySoundKind.Footstep =>
                    (Mathf.Sin(2f * Mathf.PI * 62f * t) * 0.65f + deterministicNoise * 0.22f) *
                    Mathf.Exp(-normalized * 7f) * 0.65f,
                _ => 0f
            };
        }

        clip = AudioClip.Create($"Generated_{kind}", sampleCount, 1, SampleRate, false);
        clip.SetData(data, 0);
        Clips[kind] = clip;
        return clip;
    }
}

public sealed class AnomalyRecognitionAudio : MonoBehaviour
{
    private AnomalyRitualController ritual;
    private AudioSource source;

    public void Configure(AnomalyRitualController controller)
    {
        if (ritual != null) ritual.Recognized -= OnRecognized;
        ritual = controller;
        source = ProceduralAnomalyAudio.EnsureSpatialSource(gameObject);
        if (ritual != null) ritual.Recognized += OnRecognized;
    }

    private void OnDestroy()
    {
        if (ritual != null) ritual.Recognized -= OnRecognized;
    }

    private void OnRecognized(AnomalyRitualController controller)
    {
        AnomalySoundKind kind = controller.RitualKind switch
        {
            AnomalyRitualKind.GazeThenLookAway => AnomalySoundKind.ColorShift,
            AnomalyRitualKind.Approach => AnomalySoundKind.Shrink,
            AnomalyRitualKind.ApproachThenRetreat => AnomalySoundKind.DollReturn,
            AnomalyRitualKind.Hit => AnomalySoundKind.Danger,
            _ => AnomalySoundKind.Recognition
        };
        ProceduralAnomalyAudio.Play(
            source,
            kind,
            controller.IsAggressive ? 0.9f : 0.65f);
    }
}
