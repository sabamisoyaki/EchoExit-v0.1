using System;
using System.Collections.Generic;
using Door666.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Door666.Runtime
{
    /// <summary>Effects are selected by effect kind, never by a placement prefab ID.</summary>
    public sealed class AnomalyEffects : IDisposable
    {
        private interface IEffect { bool Active { get; } void Begin(); void Tick(RitualPerception perception, float deltaTime); }
        private readonly Context context;
        private readonly IEffect effect;
        private bool started;
        public bool IsActive => started && effect.Active;
        private static readonly Dictionary<string, Func<Context, IEffect>> Factories = new Dictionary<string, Func<Context, IEffect>>
        {
            { "Crimson", c => new Crimson(c) },
            { "Shrink", c => new Shrink(c) },
            { "VanishReturn", c => new VanishReturn(c) },
            { "Awaken", c => new Awaken(c) },
            { "MultiplyShadow", c => new MultiplyShadow(c) },
            { "WallBreath", c => new WallBreath(c) },
            { "FootstepApparition", c => new FootstepApparition(c) }
        };

        public AnomalyEffects(AnomalyActor actor)
        {
            context = new Context(actor);
            var kind = actor.Definition.recognitionEffect.kind;
            if (!Factories.TryGetValue(kind, out var factory)) throw new ArgumentException("Unknown recognition effect: " + kind);
            effect = factory(context);
        }

        public void ApplyInitialClue()
        {
            switch (context.Actor.Definition.clue.kind)
            {
                case "Saturation":
                    context.Tint(new Color(0.65f, 0.3f, 0.17f), 0.12f);
                    break;
                case "FaceSpawn":
                    Vector3 direction = context.Actor.Player.position - context.Visual.position;
                    direction.y = 0;
                    if (direction.sqrMagnitude > 0.01f) context.Visual.rotation = Quaternion.LookRotation(direction);
                    context.BaseRotation = context.Visual.localRotation;
                    break;
                case "NarrowShadow":
                    var shadow = context.CreateShadow(0);
                    shadow.transform.localScale = new Vector3(0.82f, 1, 0.82f);
                    foreach (var renderer in context.Renderers) renderer.shadowCastingMode = ShadowCastingMode.Off;
                    break;
                case "ExtraFootstep":
                    context.Visible(false);
                    break;
            }
        }

        public void TickClue(float time)
        {
            switch (context.Actor.Definition.clue.kind)
            {
                case "Breathing": context.Visual.localPosition = context.BasePosition + Vector3.up * (Mathf.Sin(time * 2.4f) * 0.008f); break;
                case "SeamPulse": context.Visual.localScale = Vector3.Scale(context.BaseScale, new Vector3(1, 1, 1 + Mathf.Sin(time * 1.7f) * 0.004f)); break;
            }
        }

        public void Begin()
        {
            if (started) return;
            started = true;
            context.Visual.localScale = context.BaseScale;
            context.Visual.localPosition = context.BasePosition;
            var definition = context.Definition;
            bool behind = definition.kind == "Crimson" || definition.kind == "WallBreath";
            context.Actor.Emit(definition.sound, definition.subtitle, behind ? context.Actor.BehindPlayer() : context.Actor.transform.position);
            effect.Begin();
        }

        public void Tick(RitualPerception perception, float deltaTime)
        {
            if (started) effect.Tick(perception, deltaTime);
            if (context.Definition.kind == "WallBreath")
            {
                Vector3 direction = context.Actor.Player.position - context.Actor.transform.position;
                direction.y = 0;
                float distance = direction.magnitude;
                if (distance < 0.8f && distance > 0.01f && context.Actor.Player.TryGetComponent<CharacterController>(out var controller))
                    controller.Move(direction / distance * Mathf.Min(0.8f - distance, deltaTime * 2));
            }
        }

        public void Dispose() => context.Dispose();

        private sealed class Context : IDisposable
        {
            public readonly AnomalyActor Actor;
            public Transform Visual => Actor.VisualRoot;
            public AnomalyEffectDefinition Definition => Actor.Definition.recognitionEffect;
            public readonly Vector3 BaseScale;
            public readonly Vector3 BasePosition;
            public Quaternion BaseRotation;
            public readonly Renderer[] Renderers;
            private readonly Color[] originalColors;
            private readonly BoxCollider[] colliders;
            private readonly Vector3[] colliderSizes;
            private readonly Vector3[] colliderCenters;
            private readonly bool[] colliderEnabled;
            private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            private Material shadowMaterial;

            public Context(AnomalyActor actor)
            {
                Actor = actor;
                BaseScale = Visual.localScale;
                BasePosition = Visual.localPosition;
                BaseRotation = Visual.localRotation;
                Renderers = Visual.GetComponentsInChildren<Renderer>(true);
                originalColors = new Color[Renderers.Length];
                colliders = Actor.GetComponents<BoxCollider>();
                colliderSizes = new Vector3[colliders.Length];
                colliderCenters = new Vector3[colliders.Length];
                colliderEnabled = new bool[colliders.Length];
                for (int i = 0; i < colliders.Length; ++i)
                {
                    colliderSizes[i] = colliders[i].size;
                    colliderCenters[i] = colliders[i].center;
                    colliderEnabled[i] = colliders[i].enabled;
                }
                for (int i = 0; i < Renderers.Length; ++i)
                {
                    var material = Renderers[i].sharedMaterial;
                    originalColors[i] = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.gray;
                }
            }

            public void Tint(Color color, float blend)
            {
                for (int i = 0; i < Renderers.Length; ++i)
                {
                    if (Renderers[i] == null) continue;
                    Renderers[i].GetPropertyBlock(block);
                    var value = Color.Lerp(originalColors[i], color, blend);
                    block.SetColor("_BaseColor", value);
                    block.SetColor("_Color", value);
                    Renderers[i].SetPropertyBlock(block);
                }
            }

            public void Visible(bool visible) { foreach (var renderer in Renderers) if (renderer != null) renderer.enabled = visible; }

            public void Solid(bool solid)
            {
                for (int i = 0; i < colliders.Length; ++i)
                    if (colliders[i] != null) colliders[i].enabled = solid && colliderEnabled[i];
            }

            public void ScaleCollider(float scale)
            {
                for (int i = 0; i < colliders.Length; ++i)
                {
                    if (colliders[i] == null) continue;
                    colliders[i].size = colliderSizes[i] * scale;
                    colliders[i].center = BasePosition + (colliderCenters[i] - BasePosition) * scale;
                }
            }

            public Material ShadowMaterial
            {
                get
                {
                    if (shadowMaterial != null) return shadowMaterial;
                    var shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null) shader = Shader.Find("Unlit/Color");
                    shadowMaterial = new Material(shader) { name = "Phenomenon shadow" };
                    if (shadowMaterial.HasProperty("_BaseColor")) shadowMaterial.SetColor("_BaseColor", new Color(0.014f, 0.018f, 0.02f));
                    if (shadowMaterial.HasProperty("_Color")) shadowMaterial.SetColor("_Color", new Color(0.014f, 0.018f, 0.02f));
                    shadowMaterial.SetFloat("_Cull", 0);
                    owned.Add(shadowMaterial);
                    return shadowMaterial;
                }
            }

            public GameObject CreateShadow(int index)
            {
                var center = AnomalyActor.FloorPoint(Actor.transform.position);
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                Vector3 projection = Quaternion.Euler(0, index * 48 - 48, 0) * new Vector3(0.45f, 0, 0.65f);
                foreach (var filter in Visual.GetComponentsInChildren<MeshFilter>())
                {
                    var source = filter.sharedMesh;
                    if (source == null || !source.isReadable) continue;
                    int offset = vertices.Count;
                    foreach (var vertex in source.vertices)
                    {
                        var world = filter.transform.TransformPoint(vertex);
                        float height = Mathf.Max(0, world.y - center.y);
                        var point = world + projection * height - center;
                        point.y = 0.004f + index * 0.002f;
                        vertices.Add(point);
                    }
                    foreach (int triangle in source.triangles) triangles.Add(triangle + offset);
                }
                var mesh = new Mesh { name = "Projected chair shadow" };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                var shadow = new GameObject("Floor shadow " + (index + 1));
                shadow.transform.SetParent(Actor.transform, false);
                shadow.transform.position = center;
                shadow.transform.rotation = Quaternion.identity;
                shadow.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = shadow.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = ShadowMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                owned.Add(mesh);
                owned.Add(shadow);
                return shadow;
            }

            public GameObject Shape(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 scale)
            {
                var shape = GameObject.CreatePrimitive(type);
                shape.name = "Apparition";
                shape.transform.SetParent(parent, false);
                shape.transform.localPosition = localPosition;
                shape.transform.localScale = scale;
                var collider = shape.GetComponent<Collider>();
                collider.enabled = false;
                UnityEngine.Object.Destroy(collider);
                var renderer = shape.GetComponent<Renderer>();
                renderer.sharedMaterial = ShadowMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                owned.Add(shape);
                return shape;
            }

            public void Own(UnityEngine.Object value) => owned.Add(value);
            public void Dispose() { foreach (var item in owned) if (item != null) UnityEngine.Object.Destroy(item); }
        }

        private abstract class TimedEffect : IEffect
        {
            protected readonly Context C;
            protected float Elapsed;
            public virtual bool Active => Elapsed < C.Definition.duration;
            protected TimedEffect(Context context) { C = context; }
            public virtual void Begin() { Elapsed = 0; }
            public virtual void Tick(RitualPerception perception, float deltaTime) { Elapsed += deltaTime; }
            protected float Progress => Mathf.Clamp01(Elapsed / Mathf.Max(0.001f, C.Definition.duration));
        }

        private sealed class Crimson : TimedEffect
        {
            public Crimson(Context c) : base(c) { }
            public override void Tick(RitualPerception p, float dt) { base.Tick(p, dt); C.Tint(new Color(0.56f, 0.012f, 0.017f), Mathf.SmoothStep(0, 1, Progress)); }
        }

        private sealed class Shrink : TimedEffect
        {
            private bool restoring;
            private bool restored;
            private float restoreElapsed;
            public Shrink(Context c) : base(c) { }
            public override void Tick(RitualPerception p, float dt)
            {
                base.Tick(p, dt);
                if (restored)
                {
                    if (p.Distance > 2.5f) return;
                    restored = false; restoring = false; restoreElapsed = 0; Elapsed = 0;
                }
                if (!restoring)
                {
                    float scale = Mathf.Lerp(1, C.Definition.scale, Mathf.SmoothStep(0, 1, Progress));
                    C.Visual.localScale = C.BaseScale * scale;
                    C.ScaleCollider(scale);
                    if (Progress >= 1 && p.Distance >= C.Definition.restoreDistance) restoring = true;
                }
                else
                {
                    restoreElapsed += dt;
                    float t = Mathf.Clamp01(restoreElapsed / 0.8f);
                    float scale = Mathf.Lerp(C.Definition.scale, 1, Mathf.SmoothStep(0, 1, t));
                    C.Visual.localScale = C.BaseScale * scale;
                    C.ScaleCollider(scale);
                    restored = t >= 1;
                }
            }
        }

        private sealed class VanishReturn : TimedEffect
        {
            private bool returned;
            public override bool Active => !returned;
            public VanishReturn(Context c) : base(c) { }
            public override void Tick(RitualPerception p, float dt)
            {
                base.Tick(p, dt);
                if (returned) return;
                C.Tint(Color.black, Progress);
                if (Progress >= 1) { C.Visible(false); C.Solid(false); }
                if (Elapsed < C.Definition.duration + C.Definition.returnDelay) return;
                returned = true;
                C.Tint(Color.black, 0);
                C.Visual.localRotation = C.BaseRotation * Quaternion.Euler(0, 165, 0);
                C.Visible(true);
                C.Solid(true);
                C.Actor.Emit("cloth", "［布を引きずる音］", C.Visual.position, 0.75f);
            }
        }

        private sealed class Awaken : TimedEffect
        {
            private Quaternion facePlayer;
            public Awaken(Context c) : base(c) { }
            public override void Begin()
            {
                base.Begin();
                Vector3 direction = C.Actor.Player.position - C.Visual.position;
                direction.y = 0;
                var parentRotation = C.Visual.parent != null ? C.Visual.parent.rotation : Quaternion.identity;
                facePlayer = direction.sqrMagnitude > 0.001f ? Quaternion.Inverse(parentRotation) * Quaternion.LookRotation(direction) : C.BaseRotation;
            }
            public override void Tick(RitualPerception p, float dt)
            {
                base.Tick(p, dt);
                C.Visual.localRotation = Quaternion.Slerp(C.BaseRotation, facePlayer, Mathf.Clamp01(Progress * 3));
                C.Visual.localScale = Vector3.Scale(C.BaseScale, new Vector3(1, Mathf.Lerp(1, 1.35f, Mathf.SmoothStep(0, 1, Progress)), 1));
                C.Tint(new Color(0.17f, 0.09f, 0.08f), Progress * 0.5f);
            }
        }

        private sealed class MultiplyShadow : TimedEffect
        {
            private readonly GameObject[] shadows = new GameObject[3];
            private bool turned;
            public MultiplyShadow(Context c) : base(c) { }
            public override void Begin()
            {
                base.Begin();
                foreach (var renderer in C.Renderers) renderer.shadowCastingMode = ShadowCastingMode.Off;
                for (int i = 0; i < 3; ++i) { shadows[i] = C.CreateShadow(i); shadows[i].SetActive(i == 0); }
            }
            public override void Tick(RitualPerception p, float dt)
            {
                base.Tick(p, dt);
                for (int i = 0; i < 3; ++i) shadows[i].SetActive(Elapsed >= i * 0.55f);
                if (Progress < 1 || p.Gazing || turned) return;
                turned = true;
                Vector3 direction = C.Actor.Player.position - C.Actor.transform.position;
                direction.y = 0;
                if (direction.sqrMagnitude < 0.001f) return;
                foreach (var shadow in shadows) shadow.transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private sealed class WallBreath : TimedEffect
        {
            public WallBreath(Context c) : base(c) { }
            public override void Tick(RitualPerception p, float dt)
            {
                base.Tick(p, dt);
                float breath = Mathf.Sin(Progress * Mathf.PI);
                C.Visual.localScale = Vector3.Scale(C.BaseScale, new Vector3(1 + breath * 0.035f, 1 + breath * 0.018f, 1 + breath * (C.Definition.scale - 1)));
                C.Visual.localPosition = C.BasePosition + Vector3.forward * (breath * 0.18f);
            }
        }

        private sealed class FootstepApparition : TimedEffect
        {
            private GameObject apparition;
            private bool vanished;
            public FootstepApparition(Context c) : base(c) { }
            public override void Begin()
            {
                base.Begin();
                apparition = new GameObject("A place just vacated");
                apparition.transform.SetParent(C.Actor.transform, true);
                apparition.transform.position = AnomalyActor.FloorPoint(C.Actor.AudioCuePosition);
                var direction = C.Actor.Player.position - apparition.transform.position;
                direction.y = 0;
                if (direction.sqrMagnitude > 0.01f) apparition.transform.rotation = Quaternion.LookRotation(direction);
                C.Own(apparition);
                C.Shape(apparition.transform, PrimitiveType.Capsule, new Vector3(0, 0.85f, 0), new Vector3(0.28f, 0.62f, 0.15f));
                C.Shape(apparition.transform, PrimitiveType.Sphere, new Vector3(0, 1.6f, 0), new Vector3(0.24f, 0.31f, 0.16f));
                for (int i = 0; i < 4; ++i)
                    C.Shape(apparition.transform, PrimitiveType.Sphere, new Vector3(i % 2 == 0 ? -0.13f : 0.13f, 0.012f, i * 0.35f), new Vector3(0.13f, 0.016f, 0.28f));
            }
            public override void Tick(RitualPerception p, float dt)
            {
                base.Tick(p, dt);
                if (Progress < 1 || vanished) return;
                vanished = true;
                apparition.SetActive(false);
            }
        }
    }
}
