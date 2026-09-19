using UnityEngine;

namespace Door666.Runtime
{
    [CreateAssetMenu(menuName = "666号扉/ゲーム設定")]
    public sealed class GameSettings : ScriptableObject
    {
        [Min(1)] public int maximumAnomalies = 6;
        [Min(10)] public float roundSeconds = 180f;
        [Min(1)] public int requiredCorrectAnswers = 6;
        [Range(0f, 1f)] public float anomalyProbability = 0.666f;
        [Min(1)] public float playerSpeed = 4f;
        public float interactionDistance = 4f;
        public float feedbackSeconds = 1.6f;
        public bool subtitles = true;

        [Header("配置の重なり")]
        [Tooltip("どの配置物も、当たり判定のこの割合を超えてほかの物に埋まってはいけない。")]
        [Range(0f, 1f)] public float maximumOverlapRatio = .75f;
        [Tooltip("この割合を超えて重なっている異変を「重なりの大きい異変」として数える。")]
        [Range(0f, 1f)] public float heavyOverlapRatio = .25f;
        [Tooltip("重なりの大きい異変は、重なりの小さい異変をこの数だけ置いたうえで、それを超える分だけ置ける。")]
        [Min(0)] public int clearAnomaliesRequired = 3;

        [Header("ログ")]
        [Tooltip("儀式の各段階・手がかり・ボタン操作・編集の細かい操作もコンソールに出す（「[666Door]」で絞り込める）。")]
        public bool verboseLogging;
    }
}
