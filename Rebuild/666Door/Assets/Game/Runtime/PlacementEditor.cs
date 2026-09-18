using System;
using System.Collections.Generic;
using System.Linq;
using Door666.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Door666.Runtime
{
    /// <summary>
    /// First-person stage editing inside the field. Placed anomalies are live from the moment they are placed, so their
    /// rituals can be tried where they stand. Hold Shift to aim a placement; Tab or Esc opens the menu.
    /// </summary>
    public sealed class PlacementEditor
    {
        private const float PlacementReach = 8f;
        // Samples per axis when measuring how much of an item's collision box is inside other objects.
        private const int OverlapSamples = 5;
        private const float FineYawStep = 5f;
        private const float CoarseYawStep = 45f;
        private const float CoarseGrid = .25f;
        private static readonly Color ValidColor = new Color(.38f, .96f, .74f);
        private static readonly Color HeavyColor = new Color(1f, .78f, .25f);
        private static readonly Color InvalidColor = new Color(1f, .30f, .22f);
        private static readonly Color SelectionColor = new Color(.55f, .80f, 1f);

        private struct PlacementCheck
        {
            public string Problem;
            public float Overlap;
            public bool Heavy;
        }

        private readonly SceneController game;
        private readonly AnomalyActorSet anomalies;
        private readonly Dictionary<string, StageDefinitionMetadata> metadata = new Dictionary<string, StageDefinitionMetadata>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> anchorHeights = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly List<AnomalyActor> pendingResets = new List<AnomalyActor>();
        private StageData draft;
        private string selectedPrefab;
        private bool category;
        private float placementYaw;
        private StageObject preview;
        private GameObject overlays;
        private LineRenderer outline;
        private Material guideMaterial;
        private Material ghostMaterial;
        private bool open;
        private bool dirty;

        public bool IsOpen => open;
        public bool MenuOpen { get; private set; }
        public int CurrentSceneId => draft == null ? 0 : draft.sceneId;
        public bool HasUnsavedChanges => dirty;
        public IReadOnlyList<AnomalyActor> Actors => anomalies.Actors;

        public PlacementEditor(SceneController owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            game = owner;
            anomalies = new AnomalyActorSet(owner);
            anomalies.Recognized += OnRecognized;
            anomalies.Caught += OnCaught;
        }

        public void Open()
        {
            Close();
            open = true;
            overlays = new GameObject("Placement editing guides");
            CreateMaterials();
            var line = new GameObject("Placement outline");
            line.transform.SetParent(overlays.transform, false);
            outline = line.AddComponent<LineRenderer>();
            outline.sharedMaterial = guideMaterial;
            outline.useWorldSpace = true;
            outline.loop = true;
            outline.widthMultiplier = .03f;
            outline.positionCount = 4;
            outline.shadowCastingMode = ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.enabled = false;
            MeasureCatalog();
            game.UI.EditorMessage("Tab でメニューを開き、置くものを選んでください。置いた異変はその場で儀式を試せます。");
            var first = game.Repository.Data.scenes.FirstOrDefault(stage => stage != null && stage.sceneId > 0);
            if (first == null) New(); else Load(first.sceneId);
            SetMenuOpen(false);
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            anomalies.Clear();
            pendingResets.Clear();
            DestroyPreview();
            if (overlays != null) ObjectCatalog.DestroyObject(overlays);
            if (guideMaterial != null) ObjectCatalog.DestroyObject(guideMaterial);
            if (ghostMaterial != null) ObjectCatalog.DestroyObject(ghostMaterial);
            draft = null;
            dirty = false;
            MenuOpen = false;
        }

        public void Tick(float deltaTime)
        {
            if (!open || draft == null) return;
            float dt = Mathf.Min(deltaTime, .1f);
            var keyboard = Keyboard.current;
            bool typing = game.UI.TextHasFocus;
            if (game.Input.Pause.WasPressedThisFrame() || (!typing && keyboard != null && keyboard.tabKey.wasPressedThisFrame))
            {
                SetMenuOpen(!MenuOpen);
                return;
            }
            if (MenuOpen)
            {
                if (!typing && keyboard != null && keyboard.f5Key.wasPressedThisFrame) Save(game.UI.EditorSceneIdText);
                return;
            }

            game.Player.Tick(game.Input, game.Settings.playerSpeed, dt);
            var view = game.Player.View.transform;
            Physics.Raycast(view.position, view.forward, out var gaze, game.Settings.interactionDistance, ~0, QueryTriggerInteraction.Ignore);
            var gazed = gaze.collider == null ? null : gaze.collider.GetComponentInParent<StageObject>();
            bool aiming = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            bool clicked = game.Input.Hit.WasPressedThisFrame();
            // While aiming, the click places instead of striking.
            anomalies.Tick(gazed, clicked && !aiming, dt);
            ProcessResets();

            if (aiming) AimPlacement(view, keyboard, clicked);
            else HoverPlaced(view, keyboard);
            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame) Save(draft.sceneId.ToString());
        }

        public void SetMenuOpen(bool value)
        {
            if (!open) return;
            MenuOpen = value;
            anomalies.Suspend(value);
            if (value) HideGuides();
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
            RefreshUI();
        }

        public void Load(int sceneId)
        {
            if (!open) return;
            var stage = game.Repository.Data.scenes.FirstOrDefault(value => value != null && value.sceneId == sceneId);
            if (sceneId < 1 || stage == null) { game.UI.EditorMessage("指定した番号のステージはありません。"); return; }
            SetDraft(stage.Clone());
            int unknown = draft.items.Count(item => item != null && !game.World.Catalog.IsKnown(item.prefabId));
            game.UI.EditorMessage("ステージ " + draft.sceneId + " を読み込みました。"
                + (unknown > 0 ? "\n未対応の配置物 " + unknown + " 個は表示しませんが、保存データには残します。" : ""));
        }

        public void New()
        {
            if (!open) return;
            SetDraft(new StageData { sceneId = StageRepository.NextSceneId(game.Repository.Data) });
            game.UI.EditorMessage("新しいステージ " + draft.sceneId + " です。Shift を押しながら床を見て、クリックで置きます。");
        }

        public void SetCategory(bool anomaly)
        {
            if (!open) return;
            category = anomaly;
            game.UI.EditorMessage(category ? "異変として置きます。" : "通常オブジェクトとして置きます。");
            RefreshUI();
        }

        /// <summary>Selects what Shift-aiming places, and returns to walking.</summary>
        public void Choose(string prefabId)
        {
            if (!open || !game.World.Catalog.IsKnown(prefabId)) return;
            var definition = game.Definitions.FindByPrefab(prefabId);
            if (category && definition != null && !definition.userStageAllowed)
            { game.UI.EditorMessage("この異変は公式ステージ専用です。"); return; }
            selectedPrefab = prefabId;
            placementYaw = 0;
            DestroyPreview();
            preview = game.World.Catalog.Create(new StageItem { prefabId = prefabId, isAnomaly = category }, overlays.transform);
            foreach (var collider in preview.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var renderer in preview.GetComponentsInChildren<Renderer>(true))
            {
                var replacements = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < replacements.Length; i++) replacements[i] = ghostMaterial;
                renderer.sharedMaterials = replacements;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            preview.gameObject.SetActive(false);
            game.UI.EditorMessage(game.World.Catalog.DisplayName(prefabId) + "（" + (category ? "異変" : "通常") + "）を選びました。Shift を押しながら床を見て、クリックで置きます。");
            SetMenuOpen(false);
        }

        /// <summary>Places the selected item at a floor point (1 cm precision, current yaw). The item is live immediately.</summary>
        public bool TryPlace(Vector3 floorPoint, out string problem)
        {
            problem = null;
            if (!open || selectedPrefab == null) { problem = "置くものが選ばれていません。"; return false; }
            var item = PendingItem(floorPoint, false);
            problem = Check(item, null).Problem;
            if (problem != null) return false;
            Place(item);
            return true;
        }

        /// <summary>Sets the yaw used for the next placement, in degrees.</summary>
        public void SetPlacementYaw(float degrees) => placementYaw = Mathf.Repeat(degrees, 360);

        public void Rotate(StageObject target, float degrees = CoarseYawStep)
        {
            if (!open || target == null || !game.World.PlacedObjects.Contains(target)) return;
            var item = target.Data;
            var previous = item.rotation;
            item.rotation = new Float3(previous.x, Mathf.Repeat(previous.y + degrees, 360), previous.z);
            string problem = Check(item, target).Problem;
            if (problem != null)
            {
                item.rotation = previous;
                game.UI.EditorMessage("回転できません。" + problem);
                return;
            }
            Respawn(target);
            game.World.RebuildNavigation();
            MarkDirty(game.World.Catalog.DisplayName(item.prefabId) + "の向きを " + Mathf.RoundToInt(item.rotation.y) + "° にしました。");
        }

        /// <summary>Share (0–1) of a placement's collision box inside walls, furniture and other placements.</summary>
        public float OverlapOf(StageObject placed) => placed == null ? 0 : OverlapRatio(placed.Data, placed);

        public void Delete(StageObject target)
        {
            if (!open || target == null || !game.World.PlacedObjects.Contains(target)) return;
            string name = game.World.Catalog.DisplayName(target.PrefabId);
            draft.items.Remove(target.Data);
            var actor = target.GetComponent<AnomalyActor>();
            if (actor != null) anomalies.Remove(actor);
            game.World.Remove(target);
            game.World.RebuildNavigation();
            if (outline != null) outline.enabled = false;
            MarkDirty(name + "を削除しました。保存すると反映されます。");
        }

        public void Save(string sceneId)
        {
            if (!open || draft == null) return;
            if (!int.TryParse(sceneId, out int id) || id < 1)
            { game.UI.EditorMessage("ステージ番号は1以上の整数にしてください。"); return; }
            var candidate = draft.Clone();
            candidate.sceneId = id;
            var context = ValidationContext();
            // Overlaps are measured on the placed objects; the clone keeps the draft's item order.
            context.OverlapRatios = draft.items.Select(item =>
            {
                var placed = game.World.PlacedObjects.FirstOrDefault(existing => existing.Data == item);
                return placed == null ? 0f : OverlapRatio(item, placed);
            }).ToList();
            AddDoorBounds(game.World.ForwardDoor, context);
            AddDoorBounds(game.World.BackDoor, context);
            var result = StageValidator.Validate(candidate, metadata, context);
            foreach (var item in candidate.items)
            {
                if (item == null || !game.World.Catalog.IsKnown(item.prefabId)) continue;
                if (!FitsField(item)) result.Errors.Add(game.World.Catalog.DisplayName(item.prefabId) + "が配置できる床の範囲を超えています。");
                if (!item.isAnomaly)
                {
                    var bounds = WorldBounds(item);
                    var coreBounds = new StageBounds(ToFloat(bounds.center), ToFloat(bounds.extents));
                    if (context.DoorBounds.Any(door => coreBounds.Overlaps(door))) result.Errors.Add("通常オブジェクトが扉を塞いでいます。");
                }
            }
            if (!result.IsValid)
            {
                game.UI.EditorMessage("保存できません。\n" + string.Join("\n", result.Errors.Take(3))
                    + (result.Errors.Count > 3 ? "\nほか " + (result.Errors.Count - 3) + " 件" : ""));
                return;
            }
            var saved = game.Repository.SaveStage(candidate);
            if (!saved.Success) { game.UI.EditorMessage(saved.Error); return; }
            draft.sceneId = id;
            dirty = false;
            game.UI.EditorMessage("ステージ " + id + " を保存しました。"
                + (result.Warnings.Count > 0 ? "\n" + string.Join("\n", result.Warnings.Take(2)) : ""));
            RefreshUI();
        }

        private void SetDraft(StageData stage)
        {
            anomalies.Clear();
            pendingResets.Clear();
            draft = stage;
            dirty = false;
            game.World.Build(draft, true, int.MaxValue, enforceRoundRules: false);
            foreach (var placed in game.World.PlacedObjects)
            {
                anomalies.Attach(placed);
                MarkInvisible(placed);
            }
            game.Player.Teleport(WorldBuilder.SpawnPosition);
            HideGuides();
            RefreshUI();
        }

        private void Place(StageItem item)
        {
            draft.items.Add(item);
            var placed = game.World.Add(item);
            anomalies.Attach(placed);
            MarkInvisible(placed);
            game.World.RebuildNavigation();
            MarkDirty(game.World.Catalog.DisplayName(item.prefabId) + "を置きました。" + (item.isAnomaly ? "その場で儀式を試せます。" : ""));
        }

        /// <summary>Recreates a placement from its data: back at its authored pose, with a fresh, unrecognized ritual.</summary>
        private void Respawn(StageObject placed)
        {
            var item = placed.Data;
            var actor = placed.GetComponent<AnomalyActor>();
            if (actor != null) anomalies.Remove(actor);
            game.World.Remove(placed);
            var fresh = game.World.Add(item);
            anomalies.Attach(fresh);
            MarkInvisible(fresh);
        }

        private void AimPlacement(Transform view, Keyboard keyboard, bool clicked)
        {
            if (selectedPrefab == null)
            {
                HideGuides();
                game.UI.Prompt("");
                game.UI.EditorMessage("Tab でメニューを開き、置くものを選んでください。");
                return;
            }
            bool coarse = keyboard.ctrlKey.isPressed;
            if (keyboard.rKey.wasPressedThisFrame) placementYaw = Mathf.Repeat(placementYaw + CoarseYawStep, 360);
            placementYaw = Mathf.Repeat(placementYaw + ScrollSteps() * (coarse ? CoarseYawStep : FineYawStep), 360);
            if (!TryAimPoint(view, out var point)) { HideGuides(); game.UI.Prompt(""); return; }
            var item = PendingItem(point, coarse);
            preview.transform.SetPositionAndRotation(ToVector(item.position), Quaternion.Euler(ToVector(item.rotation)));
            preview.gameObject.SetActive(true);
            var check = Check(item, null);
            DrawOutline(item, check.Problem != null ? InvalidColor : check.Heavy ? HeavyColor : ValidColor);
            game.UI.Prompt("重なり " + StageValidator.Percent(check.Overlap) + (check.Heavy ? "（大）" : "")
                + "     向き " + Mathf.RoundToInt(item.rotation.y) + "°" + (coarse ? "     0.25m・45°刻み" : ""));
            if (!clicked) return;
            if (check.Problem != null) game.UI.EditorMessage(check.Problem);
            else Place(item);
        }

        private void HoverPlaced(Transform view, Keyboard keyboard)
        {
            if (preview != null) preview.gameObject.SetActive(false);
            // Triggers count here so renderer-less placements such as the footstep echo can still be selected.
            Physics.Raycast(view.position, view.forward, out var hit, game.Settings.interactionDistance, ~0, QueryTriggerInteraction.Collide);
            var target = hit.collider == null ? null : hit.collider.GetComponentInParent<StageObject>();
            if (target == null || !game.World.PlacedObjects.Contains(target))
            {
                if (outline != null) outline.enabled = false;
                game.UI.Prompt("");
                return;
            }
            DrawOutline(target.Data, SelectionColor);
            game.UI.Prompt(game.World.Catalog.DisplayName(target.PrefabId) + (target.IsAnomaly ? "（異変）" : "")
                + "     重なり " + StageValidator.Percent(OverlapOf(target)) + "     向き " + Mathf.RoundToInt(target.Data.rotation.y) + "°");
            if (keyboard == null) return;
            int steps = ScrollSteps();
            if (keyboard.rKey.wasPressedThisFrame) Rotate(target, CoarseYawStep);
            else if (steps != 0) Rotate(target, steps * (keyboard.ctrlKey.isPressed ? CoarseYawStep : FineYawStep));
            else if (keyboard.deleteKey.wasPressedThisFrame) Delete(target);
        }

        private static int ScrollSteps()
        {
            var mouse = Mouse.current;
            float scroll = mouse == null ? 0 : mouse.scroll.ReadValue().y;
            return scroll > .01f ? 1 : scroll < -.01f ? -1 : 0;
        }

        /// <summary>The floor point under the crosshair, pulled back in front of any wall or object in between.</summary>
        private static bool TryAimPoint(Transform view, out Vector3 point)
        {
            point = default;
            var ray = new Ray(view.position, view.forward);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance) || distance > PlacementReach) return false;
            if (Physics.Raycast(ray, out var obstacle, distance, ~0, QueryTriggerInteraction.Ignore) && obstacle.point.y > .02f)
                distance = Mathf.Max(0, obstacle.distance - .35f);
            point = ray.GetPoint(distance);
            point.y = 0;
            return true;
        }

        private StageItem PendingItem(Vector3 floorPoint, bool coarse)
        {
            float height = anchorHeights.TryGetValue(selectedPrefab, out float anchor) ? anchor : .5f;
            float grid = coarse ? CoarseGrid : .01f;
            float yaw = coarse ? Mathf.Repeat(Mathf.Round(placementYaw / CoarseYawStep) * CoarseYawStep, 360) : placementYaw;
            return new StageItem
            {
                prefabId = selectedPrefab,
                isAnomaly = category,
                position = new Float3(Mathf.Round(floorPoint.x / grid) * grid, height, Mathf.Round(floorPoint.z / grid) * grid),
                rotation = new Float3(0, yaw, 0)
            };
        }

        /// <summary>
        /// Items may overlap walls, furniture and each other, but not doors, the player, or beyond the floor.
        /// Anomalies share a budget: heavily overlapped ones are allowed only beyond the clearly placed ones (see StageValidator).
        /// </summary>
        /// <param name="self">The placement being rotated, excluded from its own overlap; null for a new item.</param>
        private PlacementCheck Check(StageItem item, StageObject self)
        {
            var check = new PlacementCheck();
            var body = game.Player.GetComponent<CharacterController>();
            if (!FitsField(item)) check.Problem = "床の範囲の外には置けません。";
            else if (IntersectsDoor(item)) check.Problem = "扉と重なる場所には置けません。";
            else if (body != null && WorldBounds(item).Intersects(body.bounds)) check.Problem = "自分と重なる場所には置けません。";
            if (check.Problem != null) return check;

            var context = ValidationContext();
            check.Overlap = OverlapRatio(item, self);
            check.Heavy = item.isAnomaly && check.Overlap > context.HeavyOverlapRatio;
            if (check.Overlap > context.MaximumOverlapRatio)
            {
                check.Problem = "重なりすぎています（" + StageValidator.Percent(check.Overlap) + "）。" + StageValidator.Percent(context.MaximumOverlapRatio) + "までにしてください。";
                return check;
            }
            if (!item.isAnomaly) return check;

            CountAnomalies(self, out int anomalies, out int heavy);
            anomalies++;
            if (check.Heavy) heavy++;
            if (self == null && anomalies > context.MaximumAnomalies) check.Problem = "異変は" + context.MaximumAnomalies + "個まで置けます。";
            else if (heavy > context.AllowedHeavyAnomalies(anomalies)) check.Problem = StageValidator.HeavyOverlapRule(context);
            return check;
        }

        private void CountAnomalies(StageObject except, out int anomalies, out int heavy)
        {
            anomalies = 0;
            heavy = 0;
            float heavyRatio = game.Settings.heavyOverlapRatio;
            foreach (var placed in game.World.PlacedObjects)
            {
                if (placed == null || placed == except || !placed.IsAnomaly) continue;
                anomalies++;
                if (OverlapRatio(placed.Data, placed) > heavyRatio) heavy++;
            }
        }

        private StageValidationContext ValidationContext() => new StageValidationContext
        {
            MaximumAnomalies = game.Settings.maximumAnomalies,
            MaximumOverlapRatio = game.Settings.maximumOverlapRatio,
            HeavyOverlapRatio = game.Settings.heavyOverlapRatio,
            ClearAnomaliesRequired = game.Settings.clearAnomaliesRequired,
            IsUserStage = true,
            PlayerSpawn = ToFloat(WorldBuilder.SpawnPosition)
        };

        private void OnRecognized(AnomalyActor actor)
        {
            game.UI.Recognized(actor.IsThreat, true);
        }

        // Respawning destroys the actor, so captures are handled after the tick that reported them.
        private void OnCaught(AnomalyActor actor)
        {
            if (!pendingResets.Contains(actor)) pendingResets.Add(actor);
        }

        private void ProcessResets()
        {
            if (pendingResets.Count == 0) return;
            foreach (var actor in pendingResets)
            {
                var placed = actor == null ? null : actor.GetComponent<StageObject>();
                if (placed == null || !game.World.PlacedObjects.Contains(placed)) continue;
                game.UI.CaughtWhileEditing(game.World.Catalog.DisplayName(placed.PrefabId));
                Respawn(placed);
            }
            pendingResets.Clear();
        }

        private void MarkDirty(string message)
        {
            dirty = true;
            game.UI.EditorMessage(message);
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (!open || draft == null) return;
            var context = ValidationContext();
            CountAnomalies(null, out int anomalies, out int heavy);
            var state = new EditorStatus
            {
                StageId = draft.sceneId,
                Unsaved = dirty,
                AnomalyCategory = category,
                SelectedPrefab = selectedPrefab,
                SelectedName = selectedPrefab == null ? null : game.World.Catalog.DisplayName(selectedPrefab),
                Anomalies = anomalies,
                MaximumAnomalies = context.MaximumAnomalies,
                HeavyAnomalies = heavy,
                AllowedHeavyAnomalies = context.AllowedHeavyAnomalies(anomalies)
            };
            if (MenuOpen) game.UI.ShowEditorMenu(state);
            else game.UI.ShowEditorHud(state);
        }

        private void HideGuides()
        {
            if (preview != null) preview.gameObject.SetActive(false);
            if (outline != null) outline.enabled = false;
        }

        private void DestroyPreview()
        {
            if (preview != null) ObjectCatalog.DestroyObject(preview.gameObject);
            preview = null;
        }

        private void MeasureCatalog()
        {
            metadata.Clear();
            anchorHeights.Clear();
            var samples = new GameObject("Catalog collider measurements");
            samples.SetActive(false);
            try
            {
                foreach (string id in game.World.Catalog.PrefabIds)
                {
                    var sample = game.World.Catalog.Create(new StageItem { prefabId = id }, samples.transform);
                    if (sample == null || !sample.TryGetComponent<BoxCollider>(out var box)) continue;
                    var definition = game.Definitions.FindByPrefab(id);
                    metadata[id] = new StageDefinitionMetadata
                    {
                        HalfExtents = ToFloat(box.size * .5f),
                        BoundsCenterOffset = ToFloat(box.center),
                        IsChaser = definition != null && definition.IsThreat,
                        UserStageAllowed = definition == null || definition.userStageAllowed,
                        AutomaticallyActivates = definition != null && definition.placementRules != null && definition.placementRules.automaticActivation,
                        AutoActivationRadius = definition != null && definition.placementRules != null ? definition.placementRules.activationRadius : 0
                    };
                    anchorHeights[id] = sample.VisualRoot != null ? -sample.VisualRoot.localPosition.y : .5f;
                }
            }
            finally { ObjectCatalog.DestroyObject(samples); }
        }

        private Bounds WorldBounds(StageItem item)
        {
            var data = metadata.TryGetValue(item.prefabId, out var found) ? found : new StageDefinitionMetadata();
            var matrix = Matrix4x4.TRS(ToVector(item.position), Quaternion.Euler(ToVector(item.rotation)), Vector3.one);
            var half = ToVector(data.HalfExtents);
            var center = ToVector(data.BoundsCenterOffset);
            var bounds = new Bounds(matrix.MultiplyPoint3x4(center), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
                bounds.Encapsulate(matrix.MultiplyPoint3x4(center + new Vector3((corner & 1) == 0 ? -half.x : half.x,
                    (corner & 2) == 0 ? -half.y : half.y, (corner & 4) == 0 ? -half.z : half.z)));
            return bounds;
        }

        private bool FitsField(StageItem item)
        {
            var bounds = WorldBounds(item);
            var field = WorldBuilder.PlacementBounds;
            return bounds.min.x >= field.min.x && bounds.max.x <= field.max.x
                && bounds.min.z >= field.min.z && bounds.max.z <= field.max.z
                && bounds.min.y >= -.025f && bounds.max.y <= field.max.y;
        }

        private bool IntersectsDoor(StageItem item)
        {
            if (!metadata.TryGetValue(item.prefabId, out var data)) return false;
            var rotation = Quaternion.Euler(ToVector(item.rotation));
            var center = ToVector(item.position) + rotation * ToVector(data.BoundsCenterOffset);
            foreach (var collider in Physics.OverlapBox(center, ToVector(data.HalfExtents) * .97f, rotation, ~0, QueryTriggerInteraction.Ignore))
                if (collider != null && collider.GetComponentInParent<DoorTarget>() != null) return true;
            return false;
        }

        /// <summary>Share (0–1) of the item's collision box inside other solid objects, sampled on a fixed grid so a pose always
        /// measures the same. The floor, the player and the item itself do not count.</summary>
        private float OverlapRatio(StageItem item, StageObject self)
        {
            if (!metadata.TryGetValue(item.prefabId, out var data)) return 0;
            var rotation = Quaternion.Euler(ToVector(item.rotation));
            var center = ToVector(item.position) + rotation * ToVector(data.BoundsCenterOffset);
            var half = ToVector(data.HalfExtents);
            var solids = new List<Collider>();
            foreach (var collider in Physics.OverlapBox(center, half, rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider == null || collider.bounds.max.y <= .05f || collider is CharacterController) continue;
                if (self != null && collider.transform.IsChildOf(self.transform)) continue;
                solids.Add(collider);
            }
            if (solids.Count == 0) return 0;

            int inside = 0;
            for (int x = 0; x < OverlapSamples; x++)
            for (int y = 0; y < OverlapSamples; y++)
            for (int z = 0; z < OverlapSamples; z++)
            {
                var local = new Vector3(Sample(half.x, x), Sample(half.y, y), Sample(half.z, z));
                var point = center + rotation * local;
                foreach (var solid in solids)
                {
                    if (!Contains(solid, point)) continue;
                    inside++;
                    break;
                }
            }
            return inside / (float)(OverlapSamples * OverlapSamples * OverlapSamples);
        }

        private static float Sample(float halfExtent, int index) => -halfExtent + halfExtent * 2 * (index + .5f) / OverlapSamples;

        private static bool Contains(Collider collider, Vector3 point)
        {
            if (collider is BoxCollider box)
            {
                var local = box.transform.InverseTransformPoint(point) - box.center;
                var extents = box.size * .5f;
                return Mathf.Abs(local.x) <= extents.x && Mathf.Abs(local.y) <= extents.y && Mathf.Abs(local.z) <= extents.z;
            }
            return (Physics.ClosestPoint(point, collider, collider.transform.position, collider.transform.rotation) - point).sqrMagnitude < 1e-6f;
        }

        private void DrawOutline(StageItem item, Color color)
        {
            if (outline == null) return;
            var bounds = WorldBounds(item);
            float y = .03f;
            outline.startColor = color;
            outline.endColor = color;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            outline.SetPropertyBlock(properties);
            outline.SetPositions(new[] { new Vector3(bounds.min.x - .06f, y, bounds.min.z - .06f),
                new Vector3(bounds.max.x + .06f, y, bounds.min.z - .06f), new Vector3(bounds.max.x + .06f, y, bounds.max.z + .06f),
                new Vector3(bounds.min.x - .06f, y, bounds.max.z + .06f) });
            outline.enabled = true;
        }

        /// <summary>Renderer-less placements (the footstep echo) get a floor disc so they can be found and selected.</summary>
        private void MarkInvisible(StageObject placed)
        {
            if (placed == null || placed.GetComponentsInChildren<Renderer>().Length > 0) return;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Editor location marker";
            marker.transform.SetParent(placed.transform, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = new Vector3(.36f, .01f, .36f);
            var collider = marker.GetComponent<Collider>();
            collider.enabled = false;
            ObjectCatalog.DestroyObject(collider);
            var renderer = marker.GetComponent<Renderer>();
            renderer.sharedMaterial = guideMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", new Color(.33f, .78f, .65f));
            properties.SetColor("_Color", new Color(.33f, .78f, .65f));
            renderer.SetPropertyBlock(properties);
        }

        private void CreateMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            guideMaterial = new Material(shader) { name = "Editor guide", color = new Color(.55f, .96f, .75f) };
            ghostMaterial = new Material(shader) { name = "Placement ghost", color = new Color(.35f, .95f, .72f, .34f) };
            ghostMaterial.SetColor("_BaseColor", new Color(.35f, .95f, .72f, .34f));
            ghostMaterial.SetFloat("_Surface", 1);
            ghostMaterial.SetFloat("_ZWrite", 0);
            ghostMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            ghostMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            ghostMaterial.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void AddDoorBounds(DoorTarget door, StageValidationContext context)
        {
            if (door == null) return;
            foreach (var collider in door.GetComponentsInChildren<Collider>())
                context.DoorBounds.Add(new StageBounds(ToFloat(collider.bounds.center), ToFloat(collider.bounds.extents)));
        }

        private static Float3 ToFloat(Vector3 value) => new Float3(value.x, value.y, value.z);
        private static Vector3 ToVector(Float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
