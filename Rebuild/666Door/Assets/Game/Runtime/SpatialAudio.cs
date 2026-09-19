using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Door666.Runtime
{
    /// <summary>Spatial sound effects: the clip assigned in SoundLibrary.asset, or a deterministic procedural sound.</summary>
    public static class SpatialAudio
    {
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static readonly HashSet<string> ReportedUnknown = new HashSet<string>();
        private static SoundLibrary library;

        public static void Emit(Transform owner, Vector3 position, string kind, float volume = 0.5f)
        {
            if (string.IsNullOrEmpty(kind) || volume <= 0) return;
            if (library == null) library = SoundLibrary.Load();
            var assigned = library == null ? null : library.Find(kind);
            AudioClip clip;
            if (assigned != null && assigned.clip != null)
            {
                clip = assigned.clip;
                volume *= assigned.volume;
            }
            else
            {
                if (assigned != null) volume *= assigned.volume;
                if (!Clips.TryGetValue(kind, out clip) || clip == null)
                {
                    if (assigned == null && Array.FindIndex(SoundLibrary.KnownSounds, known => known.Key == kind) < 0 && ReportedUnknown.Add(kind))
                        GameLog.Warning("音", "音「" + kind + "」は SoundLibrary.KnownSounds にない名前です。既定の雑音で鳴らします。");
                    clip = CreateClip(kind);
                    Clips[kind] = clip;
                }
            }
            if (volume <= 0) return;
            var sound = new GameObject("Spatial sound · " + kind);
            if (owner != null) sound.transform.SetParent(owner, true);
            sound.transform.position = position;
            var source = sound.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.spatialBlend = 1;
            source.dopplerLevel = 0;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1.2f;
            source.maxDistance = 16;
            source.Play();
            Object.Destroy(sound, clip.length + 0.1f);
        }

        private static AudioClip CreateClip(string kind)
        {
            const int rate = 22050;
            float duration = kind == "hum" ? 1.8f : kind == "doubleBreath" ? 1.6f : kind == "footstep" ? 0.28f : 0.9f;
            int count = Mathf.CeilToInt(duration * rate);
            var samples = new float[count];
            float filtered = 0;
            uint noiseState = 666;
            for (int i = 0; i < count; ++i)
            {
                float time = i / (float)rate;
                float phase = time / duration;
                noiseState = noiseState * 1664525u + 1013904223u;
                float noise = ((noiseState >> 8) / 16777215f) * 2 - 1;
                filtered = Mathf.Lerp(filtered, noise, 0.11f);
                float envelope = Mathf.Sin(phase * Mathf.PI);
                float value;
                switch (kind)
                {
                    case "hum": value = Mathf.Sin(2 * Mathf.PI * 58 * time) * 0.28f + Mathf.Sin(2 * Mathf.PI * 117 * time) * 0.1f; break;
                    case "reverse": value = (filtered * 0.65f + Mathf.Sin(2 * Mathf.PI * (150 * time + 220 * time * time)) * 0.28f) * phase * phase; break;
                    case "footstep": value = (filtered * 0.7f + Mathf.Sin(2 * Mathf.PI * 82 * time) * 0.4f) * Mathf.Exp(-time * 20); envelope = Mathf.Min(1, time * 300); break;
                    case "wet": value = (noise * 0.3f + filtered) * Mathf.Exp(-time * 8); break;
                    case "wood": value = (Mathf.Sin(2 * Mathf.PI * 176 * time) + Mathf.Sin(2 * Mathf.PI * 391 * time) * 0.4f + noise * 0.2f) * Mathf.Exp(-time * 9); break;
                    case "contract": value = Mathf.Sin(2 * Mathf.PI * (260 * time - 100 * time * time)) * 0.35f + filtered * 0.5f; break;
                    case "cloth": value = filtered * Mathf.Sin(time * 35) * 1.4f; break;
                    case "inhale": value = noise * 0.28f + filtered * 1.2f + Mathf.Sin(2 * Mathf.PI * 91 * time) * 0.15f; envelope *= phase; break;
                    case "exhale": value = filtered * 1.5f + noise * 0.18f; envelope *= 1 - phase; break;
                    case "doubleBreath": value = filtered * (1 + Mathf.Sin(time * 9)) + noise * 0.12f * (1 + Mathf.Sin(time * 13)); break;
                    default: value = filtered * 0.8f + Mathf.Sin(2 * Mathf.PI * 46 * time) * 0.2f; break;
                }
                samples[i] = Mathf.Clamp(value * envelope * 0.65f, -0.85f, 0.85f);
            }
            var clip = AudioClip.Create("666 · " + kind, count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
