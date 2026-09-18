using System;
using System.Collections.Generic;
using Door666.Core;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Door666.Runtime
{
    /// <summary>Visual factories preserve legacy placement keys without embedding ritual rules.</summary>
    public sealed class ObjectCatalog
    {
        // Material assets under Resources/Materials/Catalog, shared by the field scene's furniture and runtime placements.
        public static readonly SurfaceSpec[] Surfaces =
        {
            new SurfaceSpec("Varnished wood", new Color(.31f, .20f, .095f), .18f),
            new SurfaceSpec("Worn wood edges", new Color(.16f, .105f, .059f), .03f),
            new SurfaceSpec("Old cardboard", new Color(.56f, .43f, .24f), .03f),
            new SurfaceSpec("Paper tape", new Color(.70f, .58f, .35f), .03f),
            new SurfaceSpec("Porcelain", new Color(.74f, .70f, .60f), .38f),
            new SurfaceSpec("Faded velvet", new Color(.27f, .20f, .18f), .03f),
            new SurfaceSpec("Glass eyes", new Color(.014f, .012f, .011f), .8f),
            new SurfaceSpec("Matted bear fur", new Color(.23f, .135f, .067f), .03f),
            new SurfaceSpec("Worn bear muzzle", new Color(.45f, .34f, .19f), .03f),
            new SurfaceSpec("Wallpaper panel", new Color(.54f, .51f, .28f), .03f)
        };

        private sealed class Entry
        {
            public string Name;
            public Vector3 Size;
            public float AnchorHeight;
            public Action<Transform> Factory;
            public bool ExcludeFromNavigation;
            public bool TriggerOnly;
        }

        private readonly Dictionary<string, Entry> entries;
        private readonly List<string> ids;
        private readonly Material timber;
        private readonly Material darkTimber;
        private readonly Material cardboard;
        private readonly Material tape;
        private readonly Material porcelain;
        private readonly Material dress;
        private readonly Material eye;
        private readonly Material fur;
        private readonly Material muzzle;
        private readonly Material wall;

        public IReadOnlyList<string> PrefabIds => ids;

        public ObjectCatalog()
        {
            timber = LoadMaterial("Varnished wood");
            darkTimber = LoadMaterial("Worn wood edges");
            cardboard = LoadMaterial("Old cardboard");
            tape = LoadMaterial("Paper tape");
            porcelain = LoadMaterial("Porcelain");
            dress = LoadMaterial("Faded velvet");
            eye = LoadMaterial("Glass eyes");
            fur = LoadMaterial("Matted bear fur");
            muzzle = LoadMaterial("Worn bear muzzle");
            wall = LoadMaterial("Wallpaper panel");
            entries = new Dictionary<string, Entry>(StringComparer.Ordinal)
            {
                ["ChairPrefab"] = new Entry { Name = "席を数える椅子", Size = new Vector3(.70f, 1.10f, .72f), AnchorHeight = .5f, Factory = CreateChair },
                ["changeColorBox"] = new Entry { Name = "目を離した箱", Size = new Vector3(.86f, .92f, .78f), AnchorHeight = .5f, Factory = CreateBox },
                ["anomaryShirinkBox"] = new Entry { Name = "痩せる箱", Size = new Vector3(.86f, .92f, .78f), AnchorHeight = .5f, Factory = CreateBox },
                ["DollPrefab"] = new Entry { Name = "帰ってくる人形", Size = new Vector3(.60f, 1.26f, .45f), AnchorHeight = .5f, Factory = CreateDoll },
                ["bears"] = new Entry { Name = "叩き起こし", Size = new Vector3(.90f, 1.55f, .72f), AnchorHeight = .5f, Factory = CreateBear, ExcludeFromNavigation = true },
                ["wall"] = new Entry { Name = "呼吸する壁", Size = new Vector3(2.0f, 2.6f, .28f), AnchorHeight = 1f, Factory = CreateWall },
                ["footstepEcho"] = new Entry { Name = "一歩多い足音", Size = new Vector3(.35f, .03f, .8f), AnchorHeight = .03f, Factory = CreateInvisibleSource, ExcludeFromNavigation = true, TriggerOnly = true }
            };
            ids = new List<string>(entries.Keys);
        }

        public bool IsKnown(string prefabId) => !string.IsNullOrEmpty(prefabId) && entries.ContainsKey(prefabId);
        public string DisplayName(string prefabId) => IsKnown(prefabId) ? entries[prefabId].Name : prefabId ?? "不明な配置物";

        public StageObject Create(StageItem item, Transform parent)
        {
            if (item == null || !IsKnown(item.prefabId))
            {
                Debug.LogWarning("配置モデルが見つかりません。元データは保持します: " + (item == null ? "null" : item.prefabId));
                return null;
            }

            var placed = new GameObject(item.prefabId);
            placed.transform.SetParent(parent, false);
            placed.transform.position = ToVector(item.position);
            placed.transform.rotation = Quaternion.Euler(ToVector(item.rotation));
            var marker = placed.AddComponent<StageObject>();
            marker.Data = item;
            marker.VisualRoot = CreateVisual(item.prefabId, placed.transform);
            var entry = entries[item.prefabId];
            var collider = placed.AddComponent<BoxCollider>();
            collider.size = entry.Size;
            collider.center = new Vector3(0, entry.Size.y * .5f - entry.AnchorHeight, 0);
            collider.isTrigger = entry.TriggerOnly;
            if (entry.ExcludeFromNavigation && (item.isAnomaly || entry.TriggerOnly))
            {
                placed.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }
            return marker;
        }

        public Transform CreateVisual(string prefabId, Transform parent)
        {
            if (!IsKnown(prefabId)) return null;
            var entry = entries[prefabId];
            var root = new GameObject("Visual").transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.down * entry.AnchorHeight;
            var prefab = Resources.Load<GameObject>("Visuals/" + prefabId);
            if (prefab != null)
            {
                var instance = Object.Instantiate(prefab, root);
                instance.name = "Model";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                    DestroyObject(collider);
                }
                FitToFloor(instance.transform, entry.Size);
            }
            else entry.Factory(root);
            return root;
        }

        private void FitToFloor(Transform model, Vector3 targetSize)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = LocalBounds(model, renderers);
            float fit = Mathf.Min(targetSize.x / Mathf.Max(.01f, bounds.size.x), targetSize.y / Mathf.Max(.01f, bounds.size.y), targetSize.z / Mathf.Max(.01f, bounds.size.z));
            model.localScale *= fit;
            model.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * fit;
        }

        private static Bounds LocalBounds(Transform root, Renderer[] renderers)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in renderers)
            {
                var world = renderer.bounds;
                for (int n = 0; n < 8; n++)
                {
                    var p = new Vector3((n & 1) == 0 ? world.min.x : world.max.x, (n & 2) == 0 ? world.min.y : world.max.y, (n & 4) == 0 ? world.min.z : world.max.z);
                    p = root.InverseTransformPoint(p);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        private void CreateChair(Transform parent)
        {
            Box("Seat", parent, new Vector3(0, .50f, 0), new Vector3(.64f, .085f, .61f), timber);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                    Box("Leg", parent, new Vector3(x * .245f, .25f, z * .225f), new Vector3(.07f, .50f, .07f), darkTimber);
                Box("Back post", parent, new Vector3(x * .255f, .79f, -.25f), new Vector3(.075f, .62f, .075f), darkTimber);
                Box("Side stretcher", parent, new Vector3(x * .245f, .22f, 0), new Vector3(.035f, .04f, .45f), timber);
            }
            Box("Back rail", parent, new Vector3(0, 1.055f, -.25f), new Vector3(.64f, .09f, .085f), timber);
            for (int i = -1; i <= 1; i++)
                Box("Back slat", parent, new Vector3(i * .17f, .84f, -.25f), new Vector3(.085f, .32f, .045f), timber);
            Box("Front stretcher", parent, new Vector3(0, .23f, .225f), new Vector3(.49f, .04f, .035f), timber);
        }

        private void CreateBox(Transform parent)
        {
            Box("Cardboard", parent, new Vector3(0, .455f, 0), new Vector3(.84f, .91f, .76f), cardboard);
            Box("Top tape", parent, new Vector3(0, .914f, 0), new Vector3(.12f, .006f, .77f), tape);
            Box("Front tape", parent, new Vector3(0, .70f, .382f), new Vector3(.12f, .42f, .004f), tape);
            Box("Fold", parent, new Vector3(0, .917f, 0), new Vector3(.011f, .003f, .75f), darkTimber);
            Box("Label", parent, new Vector3(-.20f, .32f, .383f), new Vector3(.21f, .14f, .005f), tape);
            for (int i = 0; i < 5; i++)
                Box("Old label marking", parent, new Vector3(-.273f + .037f * i, .32f, .388f), new Vector3(.009f, .082f, .003f), darkTimber);
        }

        private void CreateDoll(Transform parent)
        {
            Sphere("Head", parent, new Vector3(0, 1.04f, 0), new Vector3(.30f, .36f, .29f), porcelain);
            Sphere("Hair", parent, new Vector3(0, 1.12f, -.045f), new Vector3(.33f, .24f, .28f), darkTimber);
            Sphere("Torso", parent, new Vector3(0, .75f, 0), new Vector3(.33f, .43f, .24f), dress);
            Cylinder("Dress", parent, new Vector3(0, .46f, 0), new Vector3(.46f, .15f, .39f), dress);
            for (int side = -1; side <= 1; side += 2)
            {
                Capsule("Arm", parent, new Vector3(side * .217f, .70f, 0), new Vector3(.09f, .19f, .09f), porcelain, new Vector3(0, 0, side * 14));
                Capsule("Leg", parent, new Vector3(side * .105f, .20f, 0), new Vector3(.10f, .18f, .10f), porcelain, Vector3.zero);
                Sphere("Shoe", parent, new Vector3(side * .105f, .055f, .04f), new Vector3(.13f, .105f, .19f), eye);
                Sphere("Eye", parent, new Vector3(side * .065f, 1.05f, .134f), new Vector3(.039f, .039f, .018f), eye);
            }
            Box("Mouth", parent, new Vector3(0, .961f, .132f), new Vector3(.045f, .013f, .009f), dress);
            Box("Collar", parent, new Vector3(0, .89f, .104f), new Vector3(.22f, .046f, .025f), porcelain);
        }

        private void CreateBear(Transform parent)
        {
            Sphere("Body", parent, new Vector3(0, .64f, 0), new Vector3(.68f, .84f, .53f), fur);
            Sphere("Belly", parent, new Vector3(0, .65f, .19f), new Vector3(.44f, .55f, .25f), muzzle);
            var head = new GameObject("Head").transform;
            head.SetParent(parent, false);
            head.localPosition = new Vector3(0, 1.17f, .015f);
            Sphere("Head fur", head, Vector3.zero, new Vector3(.63f, .55f, .51f), fur);
            Sphere("Muzzle", head, new Vector3(0, -.09f, .23f), new Vector3(.34f, .22f, .16f), muzzle);
            Sphere("Nose", head, new Vector3(0, -.045f, .315f), new Vector3(.105f, .075f, .05f), eye);
            for (int side = -1; side <= 1; side += 2)
            {
                Sphere("Ear", head, new Vector3(side * .25f, .21f, 0), new Vector3(.20f, .22f, .13f), fur);
                Sphere("Ear lining", head, new Vector3(side * .25f, .21f, .065f), new Vector3(.115f, .125f, .025f), muzzle);
                Sphere("Eye", head, new Vector3(side * .133f, .026f, .239f), new Vector3(.061f, .061f, .029f), eye);
                Capsule("Arm", parent, new Vector3(side * .34f, .68f, .025f), new Vector3(.25f, .26f, .25f), fur, new Vector3(0, 0, side * 20));
                Sphere("Foot", parent, new Vector3(side * .22f, .16f, .12f), new Vector3(.30f, .31f, .45f), fur);
                Sphere("Paw pad", parent, new Vector3(side * .22f, .16f, .328f), new Vector3(.20f, .20f, .036f), muzzle);
            }
            Box("Stitched seam", parent, new Vector3(0, .69f, .32f), new Vector3(.016f, .32f, .008f), darkTimber);
            for (int i = 0; i < 5; i++)
                Box("Stitch", parent, new Vector3(0, .55f + i * .066f, .325f), new Vector3(.065f, .009f, .008f), darkTimber);
        }

        private void CreateWall(Transform parent)
        {
            Box("Wallpaper", parent, new Vector3(0, 1.30f, 0), new Vector3(2, 2.60f, .24f), wall);
            Box("Skirting", parent, new Vector3(0, .08f, .015f), new Vector3(2.03f, .16f, .29f), darkTimber);
            for (int i = -3; i <= 3; i++)
            {
                Box("Wallpaper seam", parent, new Vector3(i * .28f, 1.34f, .121f), new Vector3(.008f, 2.46f, .003f), cardboard);
                Box("Wallpaper seam", parent, new Vector3(i * .28f, 1.34f, -.121f), new Vector3(.008f, 2.46f, .003f), cardboard);
            }
        }

        private static void CreateInvisibleSource(Transform parent)
        {
            // The dormant echo has no visible body. Its recognition effect owns the footprints.
        }

        private static Material LoadMaterial(string name)
        {
            var material = Resources.Load<Material>(GameConstants.CatalogMaterialResource + "/" + name);
            if (material == null)
                throw new InvalidOperationException("配置物のマテリアルがありません。メニュー「666号扉 → プロジェクトを初期化」を実行してください: " + name);
            return material;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            DestroyObject(collider);
            return go;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material) => Primitive(name, PrimitiveType.Cube, parent, position, scale, material);
        private static GameObject Sphere(string name, Transform parent, Vector3 position, Vector3 scale, Material material) => Primitive(name, PrimitiveType.Sphere, parent, position, scale, material);
        private static GameObject Cylinder(string name, Transform parent, Vector3 position, Vector3 scale, Material material) => Primitive(name, PrimitiveType.Cylinder, parent, position, scale, material);
        private static GameObject Capsule(string name, Transform parent, Vector3 position, Vector3 scale, Material material, Vector3 rotation)
        {
            var go = Primitive(name, PrimitiveType.Capsule, parent, position, scale, material);
            go.transform.localRotation = Quaternion.Euler(rotation);
            return go;
        }

        private static Vector3 ToVector(Float3 value) => new Vector3(value.x, value.y, value.z);

        internal static void DestroyObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
