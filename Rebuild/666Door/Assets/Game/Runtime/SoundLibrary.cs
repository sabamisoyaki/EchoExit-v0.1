using System;
using System.Collections.Generic;
using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>
    /// Audio files that replace the procedural sounds (Resources/SoundLibrary.asset). A sound without a clip keeps
    /// the sound generated in code, so clips can be assigned one at a time.
    /// </summary>
    [CreateAssetMenu(menuName = "666号扉/音の割り当て")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Sound
        {
            [Tooltip("コードと異変定義（AnomalyDefinitions.json の sound）が使う名前。変えると鳴らなくなります。")]
            public string key;
            [Tooltip("鳴らす音声ファイル。空ならコードで作った音を鳴らす。")]
            public AudioClip clip;
            [Tooltip("音量の倍率。")]
            [Range(0f, 2f)] public float volume = 1f;
            [Tooltip("どこで鳴るか（メモ）。")]
            public string usage;
        }

        /// <summary>Every sound the game plays, with where it is heard. The bootstrap adds missing ones to the asset.</summary>
        public static readonly (string Key, string Usage)[] KnownSounds =
        {
            ("footstep", "プレイヤーの足音、一歩多い足音の手がかり"),
            ("door", "正しい扉を選んだとき"),
            ("reverse", "間違った扉を選んだとき、目を離した箱を認識したとき"),
            ("hum", "目を離した箱の手がかり"),
            ("contract", "痩せる箱を認識したとき"),
            ("cloth", "帰ってくる人形の手がかりと認識"),
            ("doubleBreath", "叩き起こしの手がかり"),
            ("inhale", "叩き起こしを認識したとき"),
            ("wood", "席を数える椅子の手がかりと認識"),
            ("settle", "呼吸する壁の手がかり"),
            ("exhale", "呼吸する壁を認識したとき"),
            ("wet", "一歩多い足音を認識したとき")
        };

        [Header("環境音（蛍光灯のうなり。全画面でループ）")]
        [Tooltip("空ならコードで作った音を鳴らす。")]
        public AudioClip ambience;
        [Range(0f, 1f)] public float ambienceVolume = .4f;

        [Header("効果音（3D）")]
        public List<Sound> sounds = new List<Sound>();

        public Sound Find(string key)
        {
            foreach (var sound in sounds)
                if (sound != null && sound.key == key) return sound;
            return null;
        }

        public static SoundLibrary Load() => Resources.Load<SoundLibrary>(GameConstants.SoundsResource);
    }
}
