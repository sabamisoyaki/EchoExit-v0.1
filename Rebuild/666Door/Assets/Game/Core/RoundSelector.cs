using System;
using System.Collections.Generic;
using System.Linq;

namespace Door666.Core
{
    public sealed class RoundSelection
    {
        public StageData Stage { get; internal set; }
        public bool IncludeAnomalies { get; internal set; }
        public bool UsedFallback { get; internal set; }
        public bool ReuseCurrent { get; internal set; }
        public List<StageItem> Items { get; internal set; } = new List<StageItem>();
    }

    /// <summary>Selects a room; the world builder reports the actual placed count to RunSession.</summary>
    public static class RoundSelector
    {
        public const double AnomalyProbability = 0.666;

        public static RoundSelection Select(IReadOnlyList<StageData> stages, int? previousSceneId,
            StageData currentStage, double anomalyRoll, int selectionSeed)
        {
            if (double.IsNaN(anomalyRoll) || anomalyRoll < 0 || anomalyRoll >= 1)
                throw new ArgumentOutOfRangeException(nameof(anomalyRoll), "抽選値は0以上1未満です。");

            bool requestedAnomaly = anomalyRoll < AnomalyProbability;
            var all = stages == null ? new List<StageData>() : stages.Where(stage => stage != null).ToList();
            var candidates = requestedAnomaly ? all.Where(stage => stage.HasAnomalies).ToList() : all;
            bool fallback = candidates.Count == 0;
            bool includeAnomalies = requestedAnomaly;
            if (fallback)
            {
                includeAnomalies = !requestedAnomaly;
                candidates = includeAnomalies ? all.Where(stage => stage.HasAnomalies).ToList() : all;
            }

            if (candidates.Count == 0)
            {
                return Build(currentStage, currentStage != null && currentStage.HasAnomalies, true, true);
            }

            if (candidates.Select(stage => stage.sceneId).Distinct().Count() > 1 && previousSceneId.HasValue)
                candidates = candidates.Where(stage => stage.sceneId != previousSceneId.Value).ToList();

            int index = (int)((uint)selectionSeed % (uint)candidates.Count);
            return Build(candidates[index], includeAnomalies, fallback, false);
        }

        private static RoundSelection Build(StageData stage, bool includeAnomalies, bool fallback, bool reuse)
        {
            var copy = stage?.Clone();
            return new RoundSelection
            {
                Stage = copy,
                IncludeAnomalies = includeAnomalies,
                UsedFallback = fallback,
                ReuseCurrent = reuse,
                Items = copy?.items.Where(item => item != null && (includeAnomalies || !item.isAnomaly))
                    .Select(item => item.Clone()).ToList() ?? new List<StageItem>()
            };
        }
    }
}
