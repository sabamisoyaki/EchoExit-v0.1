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

        public void Build(StageData stage, bool includeAnomalies, int maxAnomalies, bool editing = false)
        {
            Clear();
            FieldRoot.ApplyAtmosphere();

            int chasing = 0;
            if (stage != null && stage.items != null)
            {
                foreach (var item in stage.items)
                {
                    if (item == null) continue;
                    if (item.isAnomaly && (!includeAnomalies || (!editing && PlacedAnomalyCount >= Mathf.Max(0, maxAnomalies)))) continue;
                    var definition = anomalyDefinitions == null ? null : anomalyDefinitions.FindByPrefab(item.prefabId);
                    bool pursuit = definition != null && definition.IsThreat;
                    if (!editing && item.isAnomaly && pursuit && chasing > 0) continue;
                    var instance = Catalog.Create(item, field.Placements);
                    if (instance == null) continue;
                    placed.Add(instance);
                    if (item.isAnomaly)
                    {
                        PlacedAnomalyCount++;
                        if (pursuit) chasing++;
                    }
                }
            }

            SetEditingView(editing);
            if (!editing) RebuildNavigation();
        }

        public void SetEditingView(bool editing)
        {
            foreach (var item in field.Overhead)
            {
                if (item == null) continue;
                foreach (var renderer in item.GetComponentsInChildren<Renderer>()) renderer.enabled = !editing;
                foreach (var collider in item.GetComponentsInChildren<Collider>()) collider.enabled = !editing;
            }
        }

        public void RebuildNavigation()
        {
            var surface = field.Navigation;
            if (surface == null) return;
            Physics.SyncTransforms();
            var previousData = surface.navMeshData;
            surface.BuildNavMesh();
            if (previousData != null && previousData != surface.navMeshData) ObjectCatalog.DestroyObject(previousData);
        }

        public void Clear()
        {
            if (field != null && field.Placements != null)
            {
                for (int i = field.Placements.childCount - 1; i >= 0; i--)
                {
                    var child = field.Placements.GetChild(i).gameObject;
                    // Deactivate first so old colliders leave physics before the deferred destroy.
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
