using System;
using System.Collections.Generic;
using Door666.Core;
using Door666.Runtime;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Door666.Editor
{
    /// <summary>Generates Field.unity from code once. Afterwards the room is edited directly in the Unity editor.</summary>
    internal sealed class FieldSceneBuilder
    {
        private const string MaterialFolder = "Assets/Field/Materials";
        private const string TextureFolder = "Assets/Field/Textures";

        private readonly List<GameObject> overhead = new List<GameObject>();
        private readonly ObjectCatalog catalog = new ObjectCatalog();
        private readonly TMP_FontAsset font = Resources.Load<TMP_FontAsset>(GameConstants.FontResource);
        private readonly Material wallpaper;
        private readonly Material carpet;
        private readonly Material ceiling;
        private readonly Material skirting;
        private readonly Material metal;
        private readonly Material dark;
        private readonly Material doorPaint;
        private readonly Material lampGlow;
        private readonly Material paper;
        private readonly Material oxidized;
        private readonly Material greenGlow;

        public static void Create(string scenePath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new FieldSceneBuilder().Build();
            // The new scene is active, so these lighting settings are saved into Field.unity for editing previews.
            FieldRoot.ApplyAtmosphere();
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private FieldSceneBuilder()
        {
            ProjectBootstrap.EnsureFolder(MaterialFolder);
            ProjectBootstrap.EnsureFolder(TextureFolder);
            wallpaper = Surface(new SurfaceSpec("Nicotine wallpaper", new Color(.74f, .69f, .43f), .02f), MakeWallpaper());
            carpet = Surface(new SurfaceSpec("Stained carpet", new Color(.60f, .55f, .36f), 0), MakeCarpet());
            ceiling = Surface(new SurfaceSpec("Acoustic ceiling", new Color(.72f, .70f, .56f), .02f), MakeCeiling());
            skirting = Surface(new SurfaceSpec("Brown baseboard", new Color(.25f, .215f, .13f), .09f));
            metal = Surface(new SurfaceSpec("Aged fixtures", new Color(.39f, .39f, .33f), .31f));
            dark = Surface(new SurfaceSpec("Unlit recess", new Color(.018f, .019f, .014f), .01f));
            doorPaint = Surface(new SurfaceSpec("Chipped dark enamel", new Color(.20f, .23f, .19f), .19f));
            paper = Surface(new SurfaceSpec("Faded notices", new Color(.76f, .71f, .49f), .02f));
            oxidized = Surface(new SurfaceSpec("Oxidized pipe", new Color(.29f, .22f, .12f), .16f));
            lampGlow = Surface(new SurfaceSpec("Fluorescent phosphor", new Color(.90f, .95f, .76f), .3f, new Color(.88f, 1f, .68f) * 3.5f));
            greenGlow = Surface(new SurfaceSpec("Exit glass", new Color(.10f, .27f, .20f), .15f, new Color(.15f, .55f, .27f) * 1.1f));
        }

        private void Build()
        {
            var root = new GameObject("Field");
            var field = root.AddComponent<FieldRoot>();
            var architecture = Child("Architecture", root.transform);
            var decoration = Child("Ordinary surroundings", root.transform);
            field.Placements = Child("Stage placements", root.transform);
            BuildShell(architecture);
            BuildDetails(decoration);
            field.BackDoor = BuildDoor(WorldBuilder.BackDoorPosition, false, architecture);
            field.ForwardDoor = BuildDoor(WorldBuilder.ForwardDoorPosition, true, architecture);
            field.FlickerLamp = BuildLighting(architecture);
            field.Overhead = overhead.ToArray();

            // Built at runtime after each round's placements; the scene stores only the settings.
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = .10f;
            surface.minRegionArea = .1f;
            field.Navigation = surface;
        }

        private void BuildShell(Transform parent)
        {
            Box("Carpet floor", parent, new Vector3(0, -.12f, 2.5f), new Vector3(14, .24f, 19), carpet, true, new Vector2(7, 9.5f));
            overhead.Add(Box("Low acoustic ceiling", parent, new Vector3(0, 3.37f, 2.5f), new Vector3(14, .18f, 19), ceiling, true, new Vector2(7, 9.5f)));
            Wall("Left perimeter", parent, new Vector3(-7, 1.65f, 2.5f), new Vector3(.25f, 3.3f, 19));
            Wall("Right perimeter", parent, new Vector3(7, 1.65f, 2.5f), new Vector3(.25f, 3.3f, 19));
            Wall("Entrance wall left", parent, new Vector3(-4, 1.65f, -7), new Vector3(6, 3.3f, .25f));
            Wall("Entrance wall right", parent, new Vector3(4, 1.65f, -7), new Vector3(6, 3.3f, .25f));
            Wall("Exit wall left", parent, new Vector3(-4, 1.65f, 12), new Vector3(6, 3.3f, .25f));
            Wall("Exit wall right", parent, new Vector3(4, 1.65f, 12), new Vector3(6, 3.3f, .25f));
            Box("Entrance lintel", parent, new Vector3(0, 3f, -7), new Vector3(2, .6f, .25f), wallpaper, true);
            Box("Exit lintel", parent, new Vector3(0, 3f, 12), new Vector3(2, .6f, .25f), wallpaper, true);

            // All old coordinates fit the open central inspection area. Side bays suggest a larger maze.
            foreach (float side in new[] { -1f, 1f })
            {
                Wall("Entry return wall", parent, new Vector3(side * 4.95f, 1.65f, -3.25f), new Vector3(4.1f, 3.3f, .30f));
                Wall("Side bay return", parent, new Vector3(side * 5.25f, 1.65f, 4.9f), new Vector3(3.5f, 3.3f, .32f));
                Wall("Exit approach wall", parent, new Vector3(side * 4.65f, 1.65f, 9.15f), new Vector3(4.7f, 3.3f, .32f));
                Wall("Column A", parent, new Vector3(side * 3.85f, 1.65f, .5f), new Vector3(.76f, 3.3f, .82f));
                Wall("Column B", parent, new Vector3(side * 3.85f, 1.65f, 6.9f), new Vector3(.76f, 3.3f, .82f));
                Box("Deep shadow in service bay", parent, new Vector3(side * 6.845f, 1.26f, 7.1f), new Vector3(.025f, 2.35f, 1.7f), dark);
                Box("Service recess header", parent, new Vector3(side * 6.78f, 2.5f, 7.1f), new Vector3(.15f, .1f, 1.9f), skirting);
                for (int end = -1; end <= 1; end += 2)
                    Box("Service recess frame", parent, new Vector3(side * 6.78f, 1.26f, 7.1f + end * .92f), new Vector3(.15f, 2.45f, .1f), skirting);
            }

            // Ceiling grid has a real silhouette even at oblique first-person angles.
            for (float x = -7; x <= 7; x += 2)
                overhead.Add(Box("Ceiling tee long", parent, new Vector3(x, 3.268f, 2.5f), new Vector3(.027f, .025f, 19), metal));
            for (float z = -7; z <= 12; z += 2)
                overhead.Add(Box("Ceiling tee cross", parent, new Vector3(0, 3.267f, z), new Vector3(14, .025f, .027f), metal));
        }

        private void BuildDetails(Transform parent)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Cylinder("Exposed service pipe", parent, new Vector3(side * 6.77f, 3.06f, 2.5f), new Vector3(.065f, 9f, .065f), oxidized, new Vector3(90, 0, 0));
                for (int i = 0; i < 5; i++)
                    Box("Pipe hanger", parent, new Vector3(side * 6.77f, 3.18f, -5f + 3.6f * i), new Vector3(.1f, .22f, .1f), metal);
            }

            // Ordinary furniture uses the same catalog as stage placements and never receives an anomaly component.
            PlaceOrdinary("ChairPrefab", new Vector3(-5.8f, .5f, .5f), 100, parent);
            PlaceOrdinary("ChairPrefab", new Vector3(-5.7f, .5f, 1.55f), 91, parent);
            PlaceOrdinary("changeColorBox", new Vector3(5.9f, .5f, -.9f), -5, parent);
            PlaceOrdinary("changeColorBox", new Vector3(6.05f, .5f, .25f), 12, parent);
            PlaceOrdinary("DollPrefab", new Vector3(-5.9f, .5f, 7.6f), 125, parent);
            PlaceOrdinary("bears", new Vector3(5.9f, .5f, 7.7f), 240, parent);
            PlaceOrdinary("ChairPrefab", new Vector3(4.8f, .5f, 10.45f), 193, parent);

            Box("Notice board frame", parent, new Vector3(-6.83f, 1.78f, -1.15f), new Vector3(.12f, .92f, 1.45f), skirting);
            Box("Notice board backing", parent, new Vector3(-6.755f, 1.78f, -1.15f), new Vector3(.03f, .80f, 1.33f), paper);
            var notice = Child("Old instructions", parent);
            notice.position = new Vector3(-6.727f, 1.78f, -1.15f);
            notice.rotation = Quaternion.Euler(0, -90, 0);
            Label("施設管理室\n\n扉は静かに閉めてください\n私物を残さないでください", notice, Vector3.zero, 1.13f, .69f, .078f, new Color(.22f, .21f, .16f));

            for (int i = 0; i < 5; i++)
                Box("Abandoned paper", parent, new Vector3(5.2f + i * .19f, .007f, 2.7f + i * .31f), new Vector3(.22f, .007f, .30f), paper).transform.localRotation = Quaternion.Euler(0, i * 27, 0);
            for (int i = 0; i < 4; i++)
                Box("Vent slat", parent, new Vector3(6.843f, 2.65f + .042f * i, 2.55f), new Vector3(.035f, .019f, .70f), metal);
            Box("Service cabinet", parent, new Vector3(-6.62f, .85f, 10.45f), new Vector3(.48f, 1.70f, .85f), doorPaint, true);
            Box("Cabinet handle", parent, new Vector3(-6.355f, .95f, 10.15f), new Vector3(.03f, .19f, .026f), metal);
            var sign = Child("Room marker", parent);
            sign.position = new Vector3(2.65f, 1.78f, 8.97f);
            Label("666\n区画", sign, Vector3.zero, .48f, .68f, .16f, new Color(.30f, .29f, .20f));
        }

        private void PlaceOrdinary(string id, Vector3 position, float rotation, Transform parent)
        {
            catalog.Create(new StageItem
            {
                prefabId = id,
                position = new Float3(position.x, position.y, position.z),
                rotation = new Float3(0, rotation, 0),
                isAnomaly = false
            }, parent);
        }

        private DoorTarget BuildDoor(Vector3 position, bool isForward, Transform parent)
        {
            var root = Child(isForward ? "Forward decision door" : "Retreat decision door", parent);
            root.position = position;
            root.rotation = Quaternion.Euler(0, isForward ? 0 : 180, 0);
            var target = root.gameObject.AddComponent<DoorTarget>();
            target.IsForward = isForward;
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 1.28f, 0);
            collider.size = new Vector3(1.94f, 2.56f, .22f);
            Box("Dark jamb", root, new Vector3(0, 1.29f, .035f), new Vector3(2.05f, 2.65f, .12f), dark);
            Box("Door leaf", root, new Vector3(0, 1.255f, -.035f), new Vector3(1.76f, 2.48f, .10f), doorPaint);
            Box("Upper inset", root, new Vector3(0, 1.75f, -.091f), new Vector3(1.38f, .72f, .016f), skirting);
            Box("Upper painted panel", root, new Vector3(0, 1.75f, -.104f), new Vector3(1.31f, .65f, .012f), doorPaint);
            Box("Kick plate", root, new Vector3(0, .18f, -.102f), new Vector3(1.72f, .31f, .013f), metal);
            for (int side = -1; side <= 1; side += 2)
                Box("Door frame", root, new Vector3(side * .985f, 1.32f, -.09f), new Vector3(.12f, 2.64f, .17f), skirting);
            Box("Door header", root, new Vector3(0, 2.59f, -.09f), new Vector3(2.08f, .13f, .17f), skirting);
            Box("Latch plate", root, new Vector3(.65f, 1.12f, -.108f), new Vector3(.10f, .22f, .015f), metal);
            Box("Door handle", root, new Vector3(.57f, 1.15f, -.152f), new Vector3(.24f, .035f, .065f), metal);
            Box("Room number plaque", root, new Vector3(0, 2.05f, -.121f), new Vector3(.63f, .28f, .02f), paper);
            Label("666", root, new Vector3(0, 2.05f, -.137f), .59f, .23f, .19f, new Color(.19f, .18f, .12f));
            Box("Direction plaque", root, new Vector3(0, 2.96f, -.015f), new Vector3(1.34f, .41f, .07f), greenGlow);
            Label(isForward ? "前進  ↑" : "後退  ↓", root, new Vector3(0, 2.96f, -.057f), 1.25f, .33f, .155f, new Color(.79f, .87f, .68f));
            Label(isForward ? "先へ進む" : "引き返す", root, new Vector3(0, 1.48f, -.118f), 1.25f, .30f, .15f, new Color(.68f, .69f, .55f));
            return target;
        }

        /// <summary>Returns the remote service-bay lamp that flickers.</summary>
        private Light BuildLighting(Transform parent)
        {
            var positions = new[]
            {
                new Vector3(0, 3.12f, -4.6f), new Vector3(0, 3.12f, -.8f),
                new Vector3(0, 3.12f, 3.0f), new Vector3(0, 3.12f, 7.1f),
                new Vector3(0, 3.12f, 10.5f), new Vector3(-5.15f, 3.12f, .5f),
                new Vector3(5.15f, 3.12f, .5f), new Vector3(-5.15f, 3.12f, 7.1f)
            };
            Light flicker = null;
            for (int i = 0; i < positions.Length; i++)
            {
                var fixture = Child("Fluorescent " + (i + 1), parent);
                overhead.Add(fixture.gameObject);
                fixture.position = positions[i];
                Box("Metal tray", fixture, Vector3.zero, new Vector3(.48f, .09f, 1.35f), metal);
                Box("Reflector", fixture, new Vector3(0, -.055f, 0), new Vector3(.39f, .025f, 1.22f), paper);
                for (int tube = -1; tube <= 1; tube += 2)
                    Cylinder("Fluorescent tube", fixture, new Vector3(tube * .112f, -.095f, 0), new Vector3(.045f, .57f, .045f), lampGlow, new Vector3(90, 0, 0));
                var emitter = Child("Light", fixture);
                emitter.localPosition = new Vector3(0, -.18f, 0);
                emitter.localRotation = Quaternion.Euler(90, 0, 0);
                var light = emitter.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(.97f, 1f, .72f);
                light.intensity = i >= 5 ? 1.20f : 1.65f;
                light.range = 6.7f;
                light.shadows = i == 2 || i == 3 ? LightShadows.Soft : LightShadows.None;
                light.shadowStrength = .83f;
                light.shadowBias = .025f;
                light.shadowNormalBias = .12f;
                flicker = light;
            }
            return flicker;
        }

        private void Wall(string name, Transform parent, Vector3 position, Vector3 scale)
        {
            Box(name, parent, position, scale, wallpaper, true, new Vector2(Mathf.Max(scale.x, scale.z) * .65f, scale.y * .55f));
            Box(name + " baseboard", parent, new Vector3(position.x, .09f, position.z), new Vector3(scale.x + .045f, .18f, scale.z + .045f), skirting);
            Box(name + " cornice", parent, new Vector3(position.x, 3.18f, position.z), new Vector3(scale.x + .045f, .085f, scale.z + .045f), ceiling);
        }

        private void Label(string text, Transform parent, Vector3 position, float width, float height, float size, Color color)
        {
            var root = Child("Lettering", parent);
            root.localPosition = position;
            var label = root.gameObject.AddComponent<TextMeshPro>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size * 10;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.rectTransform.sizeDelta = new Vector2(width, height);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.renderer.shadowCastingMode = ShadowCastingMode.Off;
            label.renderer.receiveShadows = false;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool solid = false, Vector2? tiling = null)
        {
            var go = Primitive(name, PrimitiveType.Cube, parent, position, scale, material, solid);
            if (tiling.HasValue) go.AddComponent<SurfaceTiling>().Tiling = tiling.Value;
            return go;
        }

        private static GameObject Cylinder(string name, Transform parent, Vector3 position, Vector3 scale, Material material, Vector3 rotation)
        {
            var go = Primitive(name, PrimitiveType.Cylinder, parent, position, scale, material, false);
            go.transform.localRotation = Quaternion.Euler(rotation);
            return go;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, bool solid)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Material Surface(SurfaceSpec spec, Texture2D texture = null)
        {
            var material = ProjectBootstrap.EnsureSurfaceMaterial(MaterialFolder, spec);
            if (texture != null)
            {
                material.mainTexture = texture;
                material.SetTexture("_BaseMap", texture);
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static Texture2D MakeWallpaper()
        {
            return Texture("Water-stained wallpaper", 128, (x, y) =>
            {
                float noise = Noise(x, y) * .11f;
                float stripe = x % 16 < 2 ? -.08f : .02f;
                float motif = Mathf.Abs(Mathf.Sin(x * Mathf.PI / 16)) * Mathf.Abs(Mathf.Sin(y * Mathf.PI / 24));
                float stain = Mathf.PerlinNoise(x * .043f + 17f, y * .035f + 43f);
                float value = .77f + noise + stripe - (stain > .57f ? (stain - .57f) * .62f : 0);
                if (motif > .81f && motif < .94f) value -= .12f;
                return new Color(value, value * .98f, value * .87f, 1);
            });
        }

        private static Texture2D MakeCarpet()
        {
            return Texture("Matted woven carpet", 128, (x, y) =>
            {
                float weave = ((x + y) % 2 == 0 ? .07f : -.035f) + Noise(x, y) * .18f;
                float stain = Mathf.PerlinNoise(x * .029f + 10, y * .029f + 28);
                float value = .61f + weave - Mathf.Max(0, stain - .47f) * .62f;
                float seam = x == 0 || y == 0 ? .96f : 1f;
                return new Color(value * seam, value * .97f * seam, value * .83f * seam, 1);
            });
        }

        private static Texture2D MakeCeiling()
        {
            return Texture("Acoustic tile speckle", 64, (x, y) =>
            {
                float value = .79f + Noise(x, y) * .12f;
                if (Noise(x + 33, y + 27) < .09f) value -= .17f;
                if (x == 0 || y == 0) value *= .80f;
                return new Color(value, value, value * .94f, 1);
            });
        }

        private static Texture2D Texture(string name, int size, Func<int, int, Color> sample)
        {
            string path = TextureFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) pixels[y * size + x] = sample(x, y);
            texture.SetPixels(pixels);
            texture.Apply(true);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        private static float Noise(int x, int y)
        {
            unchecked
            {
                uint hash = (uint)(x * 374761393 + y * 668265263 + 666);
                hash = (hash ^ (hash >> 13)) * 1274126177;
                return (hash & 65535) / 65535f;
            }
        }
    }
}
