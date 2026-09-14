using System;
using System.Collections.Generic;
using System.Linq;
using Door666.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Door666.Runtime
{
    /// <summary>Edits an isolated placement draft. No anomaly runtime runs in this view.</summary>
    public sealed class PlacementEditor
    {
        // Walled field (14.3 x 19.3 including walls) plus a margin, centred on the field's middle.
        private static readonly Vector2 PlanSize = new Vector2(15.4f, 20.4f);
        private static readonly Vector3 PlanCenter = new Vector3(0, 0, 2.5f);

        private readonly SessionCoordinator game;
        private readonly Dictionary<string, StageDefinitionMetadata> metadata = new Dictionary<string, StageDefinitionMetadata>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> anchorHeights = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly List<StageObject> unknownMarkers = new List<StageObject>();
        private StageData draft;
        private StageItem selected;
        private StageItem pending;
        private StageObject preview;
        private GameObject overlays;
        private LineRenderer outline;
        private Material guideMaterial;
        private Material ghostMaterial;
        private bool open;
        private bool category;
        private bool dirty;
        private bool previewValid;
        private Vector3 savedCameraLocalPosition;
        private Quaternion savedCameraLocalRotation;
        private Rect savedCameraRect;
        private float savedOrthographicSize;
        private bool savedOrthographic;
        private bool savedFog;
        private Color savedAmbientSky;
        private Color savedAmbientEquator;
        private Color savedAmbientGround;

        public int CurrentSceneId => draft == null ? 0 : draft.sceneId;
        public bool HasUnsavedChanges => dirty;

        public PlacementEditor(SessionCoordinator owner) { game = owner ?? throw new ArgumentNullException(nameof(owner)); }

        public void Open()
        {
            Close();
            open = true;
            var camera = game.Player.View;
            savedCameraLocalPosition = camera.transform.localPosition;
            savedCameraLocalRotation = camera.transform.localRotation;
            savedCameraRect = camera.rect;
            savedOrthographicSize = camera.orthographicSize;
            savedOrthographic = camera.orthographic;
            savedFog = RenderSettings.fog;
            savedAmbientSky = RenderSettings.ambientSkyColor;
            savedAmbientEquator = RenderSettings.ambientEquatorColor;
            savedAmbientGround = RenderSettings.ambientGroundColor;
            camera.orthographic = true;
            // A partial viewport leaves the rest of the back buffer uncleared, so render full screen and frame the plan instead.
            camera.rect = new Rect(0, 0, 1, 1);
            FrameCamera();
            overlays = new GameObject("Placement editing guides");
            CreateMaterials();
            var line = new GameObject("Placement outline");
            line.transform.SetParent(overlays.transform, false);
            outline = line.AddComponent<LineRenderer>();
            outline.sharedMaterial = guideMaterial;
            outline.useWorldSpace = true;
            outline.loop = true;
            outline.widthMultiplier = .045f;
            outline.positionCount = 4;
            outline.shadowCastingMode = ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.enabled = false;
            MeasureCatalog();
            var first = game.Repository.Data.scenes.FirstOrDefault(stage => stage != null && stage.sceneId > 0);
            if (first == null) New(); else Load(first.sceneId);
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            CancelPreview();
            if (overlays != null) ObjectCatalog.DestroyObject(overlays);
            if (guideMaterial != null) ObjectCatalog.DestroyObject(guideMaterial);
            if (ghostMaterial != null) ObjectCatalog.DestroyObject(ghostMaterial);
            if (game.Player != null && game.Player.View != null)
            {
                var camera = game.Player.View;
                camera.transform.localPosition = savedCameraLocalPosition;
                camera.transform.localRotation = savedCameraLocalRotation;
                camera.rect = savedCameraRect;
                camera.orthographic = savedOrthographic;
                camera.orthographicSize = savedOrthographicSize;
            }
            RenderSettings.fog = savedFog;
            RenderSettings.ambientSkyColor = savedAmbientSky;
            RenderSettings.ambientEquatorColor = savedAmbientEquator;
            RenderSettings.ambientGroundColor = savedAmbientGround;
            unknownMarkers.Clear();
            selected = null;
            draft = null;
            dirty = false;
        }

        public void Tick()
        {
            if (!open || draft == null) return;
            var camera = game.Player.View;
            FrameCamera();
            bool blocked = game.UI.TextHasFocus || game.UI.PointerOverUI;
            if (blocked)
            {
                if (preview != null) preview.gameObject.SetActive(false);
                if (pending != null && outline != null) outline.enabled = false;
                return;
            }

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) Rotate();
                if (keyboard.deleteKey.wasPressedThisFrame) Delete();
                if (keyboard.f5Key.wasPressedThisFrame) Save(game.UI.EditorSceneIdText);
            }
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame)
            {
                CancelPreview();
                game.UI.EditorMessage("配置物をクリックして選択できます。");
                UpdateSelectionOutline();
                return;
            }

            var screenPoint = mouse.position.ReadValue();
            var plan = new Rect(Screen.width * GameUI.EditorPaletteWidth, Screen.height * GameUI.EditorStatusHeight,
                Screen.width * (1 - GameUI.EditorPaletteWidth), Screen.height * (1 - GameUI.EditorStatusHeight));
            if (!plan.Contains(screenPoint))
            {
                if (preview != null) preview.gameObject.SetActive(false);
                if (pending != null && outline != null) outline.enabled = false;
                return;
            }
            Ray ray = camera.ScreenPointToRay(screenPoint);
            if (pending != null)
            {
                var floor = new Plane(Vector3.up, Vector3.zero);
                if (!floor.Raycast(ray, out float enter)) return;
                Vector3 point = ray.GetPoint(enter);
                point.x = Mathf.Round(point.x * 4) / 4;
                point.z = Mathf.Round(point.z * 4) / 4;
                point.y = anchorHeights.TryGetValue(pending.prefabId, out float height) ? height : .5f;
                pending.position = ToFloat(point);
                preview.transform.SetPositionAndRotation(point, Quaternion.Euler(ToVector(pending.rotation)));
                preview.gameObject.SetActive(true);
                previewValid = FitsField(pending) && !IntersectsArchitecture(pending);
                DrawOutline(pending, previewValid ? new Color(.38f, .96f, .74f) : new Color(1f, .30f, .22f));
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (!previewValid) { game.UI.EditorMessage("壁や扉を避け、床の範囲内に配置してください。"); return; }
                    selected = pending.Clone();
                    draft.items.Add(selected);
                    dirty = true;
                    RebuildDraft();
                    game.UI.EditorMessage(game.World.Catalog.DisplayName(pending.prefabId) + "を配置しました。続けて配置できます。右クリックで解除。");
                }
            }
            else if (mouse.leftButton.wasPressedThisFrame)
            {
                selected = null;
                float nearest = float.PositiveInfinity;
                foreach (var placed in AllEditableObjects())
                {
                    if (placed == null || !placed.TryGetComponent<Collider>(out var collider)) continue;
                    if (!collider.Raycast(ray, out var hit, 100f) || hit.distance >= nearest) continue;
                    nearest = hit.distance;
                    selected = placed.Data;
                }
                UpdateSelectionOutline();
                game.UI.EditorMessage(selected == null ? "配置物をクリックして選択できます。"
                    : game.World.Catalog.DisplayName(selected.prefabId) + "を選択しました。回転 [R] ／ 削除 [Del]");
            }
        }

        /// <summary>Fits the walled field into the screen area beside the palette and above the status bar.</summary>
        public void FrameCamera()
        {
            if (!open) return;
            var camera = game.Player.View;
            float aspect = camera.pixelWidth / (float)Mathf.Max(1, camera.pixelHeight);
            float planWidth = 1 - GameUI.EditorPaletteWidth;
            float planHeight = 1 - GameUI.EditorStatusHeight;
            float size = Mathf.Max(PlanSize.y * .5f / planHeight, PlanSize.x * .5f / (aspect * planWidth));
            camera.orthographicSize = size;
            // Shift the camera so the plan's centre lands in the middle of the uncovered area, not of the whole screen.
            float shiftX = (GameUI.EditorPaletteWidth + planWidth * .5f - .5f) * 2 * size * aspect;
            float shiftZ = (GameUI.EditorStatusHeight + planHeight * .5f - .5f) * 2 * size;
            camera.transform.SetPositionAndRotation(new Vector3(PlanCenter.x - shiftX, 18, PlanCenter.z - shiftZ), Quaternion.Euler(90, 0, 0));
        }

        public void Load(int sceneId)
        {
            if (!open) return;
            var stage = game.Repository.Data.scenes.FirstOrDefault(value => value != null && value.sceneId == sceneId);
            if (sceneId < 1 || stage == null) { game.UI.EditorMessage("指定した番号のステージはありません。"); return; }
            CancelPreview();
            selected = null;
            draft = stage.Clone();
            dirty = false;
            RebuildDraft();
            game.UI.ShowEditor(draft.sceneId, category);
            int unknown = draft.items.Count(item => item != null && !game.World.Catalog.IsKnown(item.prefabId));
            game.UI.EditorMessage("ステージ " + draft.sceneId + " を読み込みました。"
                + (unknown > 0 ? "\n未対応の配置物 " + unknown + " 個は橙色で表示し、保存データを保持します。" : "\n配置物を選ぶか、左の名前から追加できます。"));
        }

        public void New()
        {
            if (!open) return;
            CancelPreview();
            selected = null;
            draft = new StageData { sceneId = StageRepository.NextSceneId(game.Repository.Data) };
            dirty = false;
            RebuildDraft();
            game.UI.ShowEditor(draft.sceneId, category);
            game.UI.EditorMessage("新しいステージ " + draft.sceneId + "。名前を選び、床をクリックして配置してください。");
        }

        public void SetCategory(bool anomaly)
        {
            if (!open) return;
            category = anomaly;
            CancelPreview();
            int displayId = int.TryParse(game.UI.EditorSceneIdText, out int parsed) && parsed > 0 ? parsed : draft.sceneId;
            game.UI.ShowEditor(displayId, category);
            game.UI.EditorMessage(category ? "異変の名前を選んで配置してください。" : "通常オブジェクトの名前を選んで配置してください。");
            UpdateSelectionOutline();
        }

        public void Choose(string prefabId)
        {
            if (!open || !game.World.Catalog.IsKnown(prefabId)) return;
            var definition = game.Definitions.FindByPrefab(prefabId);
            if (category && definition != null && !definition.userStageAllowed)
            { game.UI.EditorMessage("この異変は公式ステージ専用です。"); return; }
            CancelPreview();
            selected = null;
            pending = new StageItem { prefabId = prefabId, isAnomaly = category };
            preview = game.World.Catalog.Create(pending, overlays.transform);
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
            game.UI.EditorMessage(game.World.Catalog.DisplayName(prefabId) + " ／ " + (category ? "異変" : "通常")
                + "\n床をクリックして配置。Rで回転、右クリックで解除。");
        }

        public void Rotate()
        {
            if (!open) return;
            StageItem item = pending ?? selected;
            if (item == null) { game.UI.EditorMessage("回転する配置物を選択してください。"); return; }
            var previous = item.rotation;
            item.rotation = new Float3(previous.x, Mathf.Repeat(previous.y + 45, 360), previous.z);
            if (pending != null) return;
            if (!FitsField(item) || IntersectsArchitecture(item))
            {
                item.rotation = previous;
                game.UI.EditorMessage("回転すると壁や扉に重なるか床の範囲を超えるため、回転できません。");
                return;
            }
            dirty = true;
            RebuildDraft();
            game.UI.EditorMessage(game.World.Catalog.DisplayName(item.prefabId) + "を回転しました。");
        }

        public void Delete()
        {
            if (!open) return;
            if (pending != null) { CancelPreview(); UpdateSelectionOutline(); return; }
            if (selected == null) { game.UI.EditorMessage("削除する配置物を選択してください。"); return; }
            string name = game.World.Catalog.DisplayName(selected.prefabId);
            draft.items.Remove(selected);
            selected = null;
            dirty = true;
            RebuildDraft();
            game.UI.EditorMessage(name + "を削除しました。保存すると反映されます。");
        }

        public void Save(string sceneId)
        {
            if (!open || draft == null) return;
            if (!int.TryParse(sceneId, out int id) || id < 1)
            { game.UI.EditorMessage("ステージ番号は1以上の整数にしてください。"); return; }
            var candidate = draft.Clone();
            candidate.sceneId = id;
            var context = new StageValidationContext
            {
                MaximumAnomalies = game.Settings.maximumAnomalies,
                IsUserStage = true,
                PlayerSpawn = ToFloat(WorldBuilder.SpawnPosition)
            };
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
            game.UI.ShowEditor(id, category);
            game.UI.EditorMessage("ステージ " + id + " を保存しました。"
                + (result.Warnings.Count > 0 ? "\n" + string.Join("\n", result.Warnings.Take(2)) : ""));
        }

        private void RebuildDraft()
        {
            foreach (var marker in unknownMarkers) if (marker != null) ObjectCatalog.DestroyObject(marker.gameObject);
            unknownMarkers.Clear();
            game.World.Build(draft, true, int.MaxValue, editing: true);
            RenderSettings.fog = false;
            // Ceiling fixtures only light the floor beneath them; a plan view needs even light to read placements.
            RenderSettings.ambientSkyColor = new Color(.46f, .47f, .38f);
            RenderSettings.ambientEquatorColor = new Color(.40f, .40f, .32f);
            RenderSettings.ambientGroundColor = new Color(.30f, .29f, .23f);
            foreach (var placed in game.World.PlacedObjects)
            {
                if (placed != null && placed.GetComponentsInChildren<Renderer>().Length == 0)
                    AddInvisibleMarker(placed, new Color(.33f, .78f, .65f));
            }
            foreach (var item in draft.items)
            {
                if (item == null || game.World.Catalog.IsKnown(item.prefabId)) continue;
                var markerObject = new GameObject("Unsupported placement");
                markerObject.transform.SetParent(overlays.transform, false);
                markerObject.transform.SetPositionAndRotation(ToVector(item.position), Quaternion.Euler(ToVector(item.rotation)));
                var marker = markerObject.AddComponent<StageObject>();
                marker.Data = item;
                markerObject.AddComponent<BoxCollider>().size = new Vector3(.5f, .5f, .5f);
                AddInvisibleMarker(marker, new Color(1f, .59f, .22f));
                unknownMarkers.Add(marker);
            }
            Physics.SyncTransforms();
            UpdateSelectionOutline();
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

        private IEnumerable<StageObject> AllEditableObjects() => game.World.PlacedObjects.Concat(unknownMarkers);

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

        private bool IntersectsArchitecture(StageItem item)
        {
            if (!metadata.TryGetValue(item.prefabId, out var data)) return false;
            var rotation = Quaternion.Euler(ToVector(item.rotation));
            var center = ToVector(item.position) + rotation * ToVector(data.BoundsCenterOffset);
            var half = ToVector(data.HalfExtents) * .97f;
            foreach (var collider in Physics.OverlapBox(center, half, rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider == null || collider.bounds.max.y <= .05f || collider.GetComponentInParent<StageObject>() != null) continue;
                if (game.World.Root != null && collider.transform.IsChildOf(game.World.Root.transform)) return true;
            }
            return false;
        }

        private void UpdateSelectionOutline()
        {
            if (outline == null) return;
            if (selected == null || pending != null) { outline.enabled = false; return; }
            DrawOutline(selected, new Color(1f, .79f, .36f));
        }

        private void DrawOutline(StageItem item, Color color)
        {
            if (outline == null) return;
            var bounds = WorldBounds(item);
            float y = Mathf.Max(.035f, bounds.max.y + .04f);
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

        private void AddInvisibleMarker(StageObject parent, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Editor location marker";
            marker.transform.SetParent(parent.transform, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = new Vector3(.36f, .025f, .36f);
            var collider = marker.GetComponent<Collider>();
            collider.enabled = false;
            ObjectCatalog.DestroyObject(collider);
            var renderer = marker.GetComponent<Renderer>();
            renderer.sharedMaterial = guideMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
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

        private void CancelPreview()
        {
            if (preview != null) ObjectCatalog.DestroyObject(preview.gameObject);
            preview = null;
            pending = null;
            previewValid = false;
        }

        private static Float3 ToFloat(Vector3 value) => new Float3(value.x, value.y, value.z);
        private static Vector3 ToVector(Float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
