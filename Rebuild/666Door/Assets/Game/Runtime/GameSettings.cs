using UnityEngine;

namespace Door666.Runtime
{
    [CreateAssetMenu(menuName = "666号扉/ゲーム設定")]
    public sealed class GameSettings : ScriptableObject
    {
        [Min(1)] public int maximumAnomalies = 3;
        [Min(10)] public float roundSeconds = 180f;
        [Min(1)] public int requiredCorrectAnswers = 6;
        [Range(0f, 1f)] public float anomalyProbability = 0.666f;
        [Min(1)] public float playerSpeed = 4f;
        public float interactionDistance = 4f;
        public float feedbackSeconds = 1.6f;
        public bool subtitles = true;
    }
}
