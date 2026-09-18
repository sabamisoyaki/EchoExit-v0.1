using System;
using System.Collections.Generic;

namespace Door666.Core
{
    public sealed class StageDefinitionMetadata
    {
        public bool IsChaser;
        public bool UserStageAllowed = true;
        public bool AutomaticallyActivates;
        public float AutoActivationRadius = 2.5f;
        public Float3 HalfExtents = new Float3(0.5f, 0.5f, 0.5f);
        public Float3 BoundsCenterOffset;
    }

    public struct StageBounds
    {
        public Float3 Center;
        public Float3 HalfExtents;

        public StageBounds(Float3 center, Float3 halfExtents) { Center = center; HalfExtents = halfExtents; }
        public bool Overlaps(StageBounds other) => Math.Abs(Center.x - other.Center.x) <= HalfExtents.x + other.HalfExtents.x
            && Math.Abs(Center.y - other.Center.y) <= HalfExtents.y + other.HalfExtents.y
            && Math.Abs(Center.z - other.Center.z) <= HalfExtents.z + other.HalfExtents.z;
    }

    public sealed class StageValidationContext
    {
        public Float3 PlayerSpawn;
        public List<StageBounds> DoorBounds = new List<StageBounds>();
        public int MaximumAnomalies = 3;
        public bool IsUserStage = true;
        public float SpawnProtectionRadius = 2.5f;

        /// <summary>Share (0–1) of each item's collision box inside other solid objects, aligned with the stage's items.
        /// Measured by the runtime; null skips the overlap rules.</summary>
        public IReadOnlyList<float> OverlapRatios;
        /// <summary>No item may be buried deeper than this.</summary>
        public float MaximumOverlapRatio = 0.75f;
        /// <summary>Anomalies overlapping more than this count as heavily overlapped.</summary>
        public float HeavyOverlapRatio = 0.25f;
        /// <summary>Heavily overlapped anomalies are allowed only beyond this many clearly placed ones.</summary>
        public int ClearAnomaliesRequired = 3;

        public int AllowedHeavyAnomalies(int anomalies) => Math.Max(0, anomalies - ClearAnomaliesRequired);
    }

    public sealed class StageValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public bool IsValid => Errors.Count == 0;
    }

    public static class StageValidator
    {
        public static StageValidationResult Validate(StageData stage,
            IReadOnlyDictionary<string, StageDefinitionMetadata> definitions, StageValidationContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.MaximumAnomalies < 0) throw new ArgumentOutOfRangeException(nameof(context.MaximumAnomalies));
            var result = new StageValidationResult();
            if (stage == null) { result.Errors.Add("部屋のデータがありません。"); return result; }
            if (stage.sceneId < 1) result.Errors.Add("部屋番号は1以上にしてください。");
            if (stage.items == null || stage.items.Count == 0)
            {
                result.Errors.Add("オブジェクトを1個以上配置してください。");
                return result;
            }

            int anomalies = 0, chasers = 0, heavyAnomalies = 0;
            for (int index = 0; index < stage.items.Count; index++)
            {
                var item = stage.items[index];
                string label = "配置 " + (index + 1) + ": ";
                if (item == null) { result.Errors.Add(label + "配置物のデータがありません。"); continue; }
                if (item.isAnomaly) anomalies++;
                if (string.IsNullOrWhiteSpace(item.prefabId)) { result.Errors.Add(label + "Prefab IDが空です。"); continue; }
                if (!item.position.IsFinite || !item.rotation.IsFinite)
                { result.Errors.Add(label + "位置または回転が不正です。"); continue; }
                if (context.OverlapRatios != null && index < context.OverlapRatios.Count)
                {
                    float overlap = context.OverlapRatios[index];
                    if (overlap > context.MaximumOverlapRatio)
                        result.Errors.Add(label + "ほかの物と" + Percent(overlap) + "重なっています。" + Percent(context.MaximumOverlapRatio) + "までにしてください。");
                    if (item.isAnomaly && overlap > context.HeavyOverlapRatio) heavyAnomalies++;
                }
                StageDefinitionMetadata metadata = null;
                bool known = definitions != null && definitions.TryGetValue(item.prefabId, out metadata) && metadata != null;
                if (!known) result.Warnings.Add(label + "未対応のPrefab ID「" + item.prefabId + "」。データを保持します。");
                if (!item.isAnomaly) continue;

                if (known)
                {
                    if (metadata.IsChaser) chasers++;
                    if (context.IsUserStage && !metadata.UserStageAllowed)
                        result.Errors.Add(label + "公式専用の異変は配置できません。");
                    if (metadata.AutomaticallyActivates && Float3.Distance(item.position, context.PlayerSpawn)
                        <= Math.Max(context.SpawnProtectionRadius, metadata.AutoActivationRadius))
                        result.Errors.Add(label + "自動発動する異変がプレイヤー初期位置に近すぎます。");
                }

                var objectBounds = RotatedBounds(item, metadata ?? new StageDefinitionMetadata());
                foreach (var door in context.DoorBounds ?? new List<StageBounds>())
                {
                    if (!objectBounds.Overlaps(door)) continue;
                    result.Errors.Add(label + "異変の当たり判定が扉と重なっています。");
                    break;
                }
            }

            if (anomalies > context.MaximumAnomalies) result.Errors.Add("異変は" + context.MaximumAnomalies + "個まで配置できます。");
            if (chasers > 1) result.Errors.Add("追跡型の異変は1体まで配置できます。");
            if (heavyAnomalies > context.AllowedHeavyAnomalies(anomalies))
                result.Errors.Add(HeavyOverlapRule(context) + "（いま重なりの大きい異変 " + heavyAnomalies + " 個、異変 " + anomalies + " 個）");
            return result;
        }

        public static string HeavyOverlapRule(StageValidationContext context) =>
            "重なりが" + Percent(context.HeavyOverlapRatio) + "を超える異変は、重なりの小さい異変を" + context.ClearAnomaliesRequired + "個置いたうえで、それを超える分だけ置けます。";

        public static string Percent(float ratio) => Math.Round(ratio * 100) + "%";

        private static StageBounds RotatedBounds(StageItem item, StageDefinitionMetadata metadata)
        {
            // Unity's Euler convention applies Z, X, then Y. Transform all corners to
            // form a conservative world AABB, including tilted and off-centre assets.
            double x = item.rotation.x * Math.PI / 180, y = item.rotation.y * Math.PI / 180, z = item.rotation.z * Math.PI / 180;
            double sx = Math.Sin(x), cx = Math.Cos(x), sy = Math.Sin(y), cy = Math.Cos(y), sz = Math.Sin(z), cz = Math.Cos(z);
            var half = metadata.HalfExtents;
            var offset = metadata.BoundsCenterOffset;
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                double px = offset.x + ((corner & 1) == 0 ? -half.x : half.x);
                double py = offset.y + ((corner & 2) == 0 ? -half.y : half.y);
                double pz = offset.z + ((corner & 4) == 0 ? -half.z : half.z);
                double zx = cz * px - sz * py, zy = sz * px + cz * py;
                double xy = cx * zy - sx * pz, xz = sx * zy + cx * pz;
                double wx = cy * zx + sy * xz + item.position.x;
                double wy = xy + item.position.y;
                double wz = -sy * zx + cy * xz + item.position.z;
                minX = Math.Min(minX, wx); maxX = Math.Max(maxX, wx);
                minY = Math.Min(minY, wy); maxY = Math.Max(maxY, wy);
                minZ = Math.Min(minZ, wz); maxZ = Math.Max(maxZ, wz);
            }
            return new StageBounds(new Float3((float)((minX + maxX) / 2), (float)((minY + maxY) / 2), (float)((minZ + maxZ) / 2)),
                new Float3((float)((maxX - minX) / 2), (float)((maxY - minY) / 2), (float)((maxZ - minZ) / 2)));
        }
    }
}
