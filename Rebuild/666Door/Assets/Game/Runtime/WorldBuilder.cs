using System;
using System.Collections.Generic;
using Door666.Core;
using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>Places a stage's items into the authored field. Round changes replace only the placements.</summary>
    public sealed class WorldBuilder : IDisposable
    {
        public static readonly Vector3 SpawnPosition = new Vector3(0, .05f, -4.8f);
        public static readonly Vector3 ForwardDoorPosition = new Vector3(0, 0, 11.75f);
        public static readonly Vector3 BackDoorPosition = new Vector3(0, 0, -6.75f);
        public static readonly Bounds PlacementBounds = new Bounds(new Vector3(0, 1.5f, 2.5f), new Vector3(13.2f, 3f, 17.9f));

        private readonly FieldRoot field;
        private readonly AnomalyCatalog anomalyDefinitions;
        private readonly List<StageObject> placed = new List<StageObject>();

        public GameObject Root => field.gameObject;
        public ObjectCatalog Catalog { get; }
        public int PlacedAnomalyCount { get; private set; }
        public IReadOnlyList<StageObject> PlacedObjects => placed;
        public DoorTarget ForwardDoor => field.ForwardDoor;
        public DoorTarget BackDoor => field.BackDoor;

        public WorldBuilder(FieldRoot field, AnomalyCatalog definitions)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            this.field = field;
            anomalyDefinitions = definitions;
            Catalog = new ObjectCatalog();
        }

        /// <param name="enforceRoundRules">A round caps anomalies and allows one pursuer; the editor shows the stage exactly as authored.</param>
        public void Build(StageData stage, bool includeAnomalies, int maxAnomalies, bool enforceRoundRules = true)
        {
            Clear();
            FieldRoot.ApplyAtmosphere();

            int chasing = 0, withheld = 0, overCap = 0, extraChasers = 0, unknown = 0;
            if (stage != null && stage.items != null)
            {
                foreach (var item in stage.items)
                {
                    if (item == null) continue;
                    if (item.isAnomaly && !includeAnomalies) { withheld++; continue; }
                    if (item.isAnomaly && enforceRoundRules && PlacedAnomalyCount >= Mathf.Max(0, maxAnomalies)) { overCap++; continue; }
                    var definition = anomalyDefinitions == null ? null : anomalyDefinitions.FindByPrefab(item.prefabId);
                    bool pursuit = item.isAnomaly && definition != null && definition.IsThreat;
                    if (enforceRoundRules && pursuit && chasing > 0) { extraChasers++; continue; }
                    var instance = Add(item);
                    if (instance == null) unknown++;
                    else if (pursuit) chasing++;
                }
            }
            RebuildNavigation();

            var excluded = new List<string>();
            if (overCap > 0) excluded.Add("異変の上限 " + maxAnomalies + " 個を超えた異変 " + overCap + " 個");
            if (extraChasers > 0) excluded.Add("2体目以降の追跡型 " + extraChasers + " 個");
            if (unknown > 0) excluded.Add("未対応の配置物 " + unknown + " 個");
            string summary = "ステージ " + (stage == null ? 0 : stage.sceneId) + " を配置: " + placed.Count + " 個（うち異変 " + PlacedAnomalyCount + " 個）"
                + (withheld > 0 ? "、異変なしの回のため異変 " + withheld + " 個を外しました" : "");
            if (excluded.Count > 0) GameLog.Info("部屋", summary + "。除外: " + string.Join("、", excluded));
            else GameLog.Detail("部屋", summary);
        }

        /// <summary>Creates one item under the field's placements. Call <see cref="RebuildNavigation"/> after changing the layout.</summary>
        public StageObject Add(StageItem item)
        {
            var instance = Catalog.Create(item, field.Placements);
            if (instance == null) return null;
            placed.Add(instance);
            if (item.isAnomaly) PlacedAnomalyCount++;
            return instance;
        }

        public void Remove(StageObject instance)
        {
            if (instance == null || !placed.Remove(instance)) return;
            if (instance.IsAnomaly) PlacedAnomalyCount--;
            // Deactivate first so its colliders leave physics before the deferred destroy.
            instance.gameObject.SetActive(false);
            ObjectCatalog.DestroyObject(instance.gameObject);
        }

        public void RebuildNavigation()
        {
            var surface = field.Navigation;
            if (surface == null) return;
            Physics.SyncTransforms();
            var previousData = surface.navMeshData;
            surface.BuildNavMesh();
            if (previousData != null && previousData != surface.navMeshData) ObjectCatalog.DestroyObject(previousData);
            if (surface.navMeshData == null) GameLog.Warning("部屋", "ナビメッシュを作れませんでした。追跡型の異変が動けません。", surface);
            else GameLog.Detail("部屋", "ナビメッシュを作り直しました。");
        }

        public void Clear()
        {
            if (field != null && field.Placements != null)
            {
                for (int i = field.Placements.childCount - 1; i >= 0; i--)
                {
                    var child = field.Placements.GetChild(i).gameObject;
                    child.SetActive(false);
                    ObjectCatalog.DestroyObject(child);
                }
            }
            placed.Clear();
            PlacedAnomalyCount = 0;
        }

        public void Dispose()
        {
            Clear();
            if (field == null || field.Navigation == null) return;
            var data = field.Navigation.navMeshData;
            field.Navigation.RemoveData();
            if (data != null) ObjectCatalog.DestroyObject(data);
        }
    }
}
