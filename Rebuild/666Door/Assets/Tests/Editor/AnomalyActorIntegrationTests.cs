using System;
using System.Collections;
using System.Linq;
using Door666.Core;
using Door666.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Door666.Tests
{
    public sealed class AnomalyActorIntegrationTests
    {
        [UnityTest]
        public IEnumerator AllSevenActorsProduceTheirPhenomenaAndNormalsStayUnchanged()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var environment = new GameObject("Actor acceptance fixture");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(environment.transform);
            floor.transform.position = new Vector3(0, -.1f, 0);
            floor.transform.localScale = new Vector3(30, .2f, 30);
            environment.AddComponent<AudioListener>();
            Physics.SyncTransforms();
            var definitions = AnomalyCatalog.FromJson(Resources.Load<TextAsset>("AnomalyDefinitions").text);
            var objects = new ObjectCatalog();
            try
            {
                foreach (var definition in definitions.Definitions) CheckNormal(definition, objects, environment.transform);
                foreach (int fps in new[] { 30, 60, 120 })
                {
                    CheckShrink(definitions.FindById("ANM-002"), objects, environment.transform, fps);
                    CheckDoll(definitions.FindById("ANM-003"), objects, environment.transform, fps);
                }
                CheckCrimson(definitions.FindById("ANM-001"), objects, environment.transform);
                CheckAwakening(definitions.FindById("ANM-004"), objects, environment.transform);
                CheckChairShadows(definitions.FindById("ANM-005"), objects, environment.transform);
                CheckWallBreath(definitions.FindById("ANM-006"), objects, environment.transform);
                CheckFootstepApparition(definitions.FindById("ANM-008"), objects, environment.transform);
            }
            finally
            {
                Object.Destroy(environment);
            }
            yield return null;
            yield return new ExitPlayMode();
        }

        private static void CheckNormal(AnomalyDefinition definition, ObjectCatalog objects, Transform parent)
        {
            using (var f = new Fixture(definition, objects, parent, false))
            {
                var renderers = f.Visual.GetComponentsInChildren<Renderer>(true);
                var enabled = renderers.Select(renderer => renderer.enabled).ToArray();
                var materials = renderers.Select(renderer => renderer.sharedMaterial).ToArray();
                var initialScale = f.Visual.localScale;
                var initialPosition = f.Visual.localPosition;
                var initialRotation = f.Visual.localRotation;
                var colliderSize = f.Collider.size;
                var colliderCenter = f.Collider.center;
                var actions = new[]
                {
                    new RitualPerception { Distance = 1, Gazing = true, Hit = true, LocalPlayerZ = 1, AudioCue = true },
                    new RitualPerception { Distance = 1, LocalPlayerZ = -1 },
                    new RitualPerception { Distance = 6, LocalPlayerZ = -1, LookingAtAudioCue = true },
                    new RitualPerception { Distance = 1, Gazing = true, Hit = true, AudioCue = true, PlayerSpeed = 3 },
                    new RitualPerception { Distance = 1, LookingAtAudioCue = true }
                };
                foreach (var action in actions) TickFor(f.Actor, action, 4, 60);
                Assert.That(f.Actor.IsRecognized, Is.False, definition.anomalyId);
                Assert.That(f.Recognitions + f.Captures + f.Subtitles, Is.Zero, definition.anomalyId);
                Assert.That(f.Root.GetComponentsInChildren<AudioSource>(true), Is.Empty, definition.anomalyId);
                AssertVector(f.Visual.localScale, initialScale);
                AssertVector(f.Visual.localPosition, initialPosition);
                Assert.That(Quaternion.Angle(f.Visual.localRotation, initialRotation), Is.LessThan(.001f));
                AssertVector(f.Collider.size, colliderSize);
                AssertVector(f.Collider.center, colliderCenter);
                for (int i = 0; i < renderers.Length; ++i)
                {
                    Assert.That(renderers[i].enabled, Is.EqualTo(enabled[i]));
                    Assert.That(renderers[i].sharedMaterial, Is.SameAs(materials[i]));
                    var block = new MaterialPropertyBlock();
                    renderers[i].GetPropertyBlock(block);
                    Assert.That(block.isEmpty, Is.True, "Normal must not acquire an anomaly tint.");
                }
            }
        }

        private static void CheckShrink(AnomalyDefinition definition, ObjectCatalog objects, Transform parent, int fps)
        {
            using (var f = new Fixture(definition, objects, parent))
            {
                var originalSize = f.Collider.size;
                var originalCenter = f.Collider.center;
                f.Actor.Tick(new RitualPerception { Distance = 2 }, 1f / fps);
                Assert.That(f.Recognitions, Is.EqualTo(1));
                // Leaving the activation radius midway must never interrupt contraction.
                TickFor(f.Actor, new RitualPerception { Distance = 4 }, .4, fps);
                Assert.That(f.Visual.localScale.x, Is.EqualTo(.54f).Within(.001f));
                TickFor(f.Actor, new RitualPerception { Distance = 4 }, .4, fps);
                AssertVector(f.Visual.localScale, Vector3.one * .08f, .0001f);
                AssertVector(f.Collider.size, originalSize * .08f, .0001f);
                TickFor(f.Actor, new RitualPerception { Distance = 4.9f }, 1, fps);
                AssertVector(f.Visual.localScale, Vector3.one * .08f);
                TickFor(f.Actor, new RitualPerception { Distance = 5 }, 1, fps);
                AssertVector(f.Visual.localScale, Vector3.one);
                AssertVector(f.Collider.size, originalSize);
                AssertVector(f.Collider.center, originalCenter);
                f.Actor.Tick(new RitualPerception { Distance = 2 }, 1f / fps);
                TickFor(f.Actor, new RitualPerception { Distance = 4 }, .85, fps);
                AssertVector(f.Visual.localScale, Vector3.one * .08f);
                Assert.That(f.Recognitions, Is.EqualTo(1), "Restored phenomena do not spam recognition banners.");
                Assert.That(f.Captures, Is.Zero);
            }
        }

        private static void CheckDoll(AnomalyDefinition definition, ObjectCatalog objects, Transform parent, int fps)
        {
            using (var f = new Fixture(definition, objects, parent))
            {
                Quaternion initialRotation = f.Visual.localRotation;
                f.Actor.Tick(new RitualPerception { Distance = 2 }, 1f / fps);
                Assert.That(f.Recognitions, Is.Zero, "Approaching alone cannot reveal the doll.");
                f.Actor.Tick(new RitualPerception { Distance = 5 }, 1f / fps);
                Assert.That(f.Recognitions, Is.EqualTo(1));
                TickFor(f.Actor, new RitualPerception { Distance = 5 }, .2, fps);
                Assert.That(f.Visual.GetComponentsInChildren<Renderer>().All(renderer => renderer.enabled), Is.True);
                TickFor(f.Actor, new RitualPerception { Distance = 5 }, .15, fps);
                Assert.That(f.Visual.GetComponentsInChildren<Renderer>(true).All(renderer => !renderer.enabled), Is.True);
                Assert.That(f.Collider.enabled, Is.False, "A vanished doll must not leave an invisible solid blocker.");
                TickFor(f.Actor, new RitualPerception { Distance = 5 }, 4.7, fps);
                Assert.That(f.Collider.enabled, Is.False, "The five-second disappearance has not finished.");
                TickFor(f.Actor, new RitualPerception { Distance = 5 }, .35, fps);
                Assert.That(f.Visual.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.enabled), Is.True);
                Assert.That(f.Collider.enabled, Is.True);
                Assert.That(Quaternion.Angle(initialRotation, f.Visual.localRotation), Is.EqualTo(165).Within(.01));
                Assert.That(f.Captures, Is.Zero);
                AssertSpatialSound(f);
            }
        }

        private static void CheckCrimson(AnomalyDefinition definition, ObjectCatalog objects, Transform parent)
        {
            using (var f = new Fixture(definition, objects, parent))
            {
                TickFor(f.Actor, new RitualPerception { Distance = 2, Gazing = true }, 1.5, 60);
                TickFor(f.Actor, new RitualPerception { Distance = 2 }, 1, 60);
                Assert.That(f.Recognitions, Is.EqualTo(1));
                TickFor(f.Actor, new RitualPerception { Distance = 2 }, .5, 60);
                var block = new MaterialPropertyBlock();
                f.Visual.GetComponentInChildren<Renderer>().GetPropertyBlock(block);
                Color color = block.GetColor("_BaseColor");
                Assert.That(color.r, Is.GreaterThan(color.g * 10));
                Assert.That(color.r, Is.GreaterThan(color.b * 10));
                var source = f.Root.GetComponentsInChildren<AudioSource>().Single();
                Assert.That(Vector3.Dot(source.transform.position - f.Player.position, f.Camera.transform.forward), Is.LessThan(0));
                AssertSpatialSound(f);
            }
        }

        private static void CheckAwakening(AnomalyDefinition definition, ObjectCatalog objects, Transform parent)
        {
            using (var f = new Fixture(definition, objects, parent))
            {
                f.Actor.Tick(new RitualPerception { Distance = 2, Gazing = true, Hit = true }, 1f / 60);
                TickFor(f.Actor, new RitualPerception { Distance = 2 }, .6, 60);
                Assert.That(f.Actor.RecognitionEffectActive, Is.True);
                Assert.That(f.Visual.localScale.y, Is.GreaterThan(1));
                var direction = f.Player.position - f.Visual.position;
                direction.y = 0;
                Assert.That(Vector3.Angle(f.Visual.forward, direction), Is.LessThan(2));
                float scaleWhilePaused = f.Visual.localScale.y;
                f.Actor.Suspend(true);
                TickFor(f.Actor, new RitualPerception { Distance = .1f }, 5, 60);
                Assert.That(f.Visual.localScale.y, Is.EqualTo(scaleWhilePaused));
                Assert.That(f.Captures, Is.Zero);
                Assert.That(f.Actor.RecognitionEffectActive, Is.True);
                AssertSpatialSound(f);
            }
        }

        private static void CheckChairShadows(AnomalyDefinition definition, ObjectCatalog objects, Transform parent)
        {
            using (var f = new Fixture(definition, objects, parent))
            {
                int physicalRenderers = f.Visual.GetComponentsInChildren<Renderer>().Length;
                f.Actor.Tick(new RitualPerception { Distance = 1, LocalPlayerZ = 1 }, 1f / 60);
                f.Actor.Tick(new RitualPerception { Distance = 1, LocalPlayerZ = -1 }, 1f / 60);
                TickFor(f.Actor, new RitualPerception { Distance = 1, LocalPlayerZ = -1, Gazing = true }, 1, 60);
                var shadows = Enumerable.Range(1, 3).Select(i => f.Actor.transform.Find("Floor shadow " + i)).ToArray();
                Assert.That(shadows.All(shadow => shadow != null), Is.True);
                Assert.That(shadows.Count(shadow => shadow.gameObject.activeSelf), Is.EqualTo(1));
                TickFor(f.Actor, new RitualPerception { Gazing = true }, .6, 60);
                Assert.That(shadows.Count(shadow => shadow.gameObject.activeSelf), Is.EqualTo(2));
                TickFor(f.Actor, new RitualPerception { Gazing = true }, .6, 60);
                Assert.That(shadows.Count(shadow => shadow.gameObject.activeSelf), Is.EqualTo(3));
                Assert.That(f.Visual.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(physicalRenderers));
                foreach (var shadow in shadows)
                {
                    Assert.That(shadow.position.y, Is.LessThan(.05f), "Shadows must project onto the floor, not the seat collider.");
                    Assert.That(shadow.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
                    Assert.That(shadow.GetComponent<Collider>(), Is.Null);
                }
                TickFor(f.Actor, new RitualPerception { Gazing = true }, .7, 60);
                f.Actor.Tick(new RitualPerception(), 1f / 60);
                var towardPlayer = f.Player.position - f.Actor.transform.position;
                towardPlayer.y = 0;
                Assert.That(Vector3.Angle(shadows[0].forward, towardPlayer), Is.LessThan(.01));
                Assert.That(f.Captures, Is.Zero);
            }
        }

        private static void CheckWallBreath(AnomalyDefinition definition, ObjectCatalog objects, Transform parent)
        {
            using (var f = new Fixture(definition, objects, parent))
            {
                var originalPosition = f.Visual.localPosition;
                f.Actor.Tick(new RitualPerception { Distance = 2 }, 1f / 60);
                TickFor(f.Actor, new RitualPerception { Distance = 2 }, 2.5, 60);
                Assert.That(f.Recognitions, Is.EqualTo(1));
                TickFor(f.Actor, new RitualPerception { Distance = 2 }, .9, 60);
                Assert.That(f.Visual.localScale.z, Is.EqualTo(1.25f).Within(.001));
                TickFor(f.Actor, new RitualPerception { Distance = 2 }, 1, 60);
                AssertVector(f.Visual.localScale, Vector3.one);
                AssertVector(f.Visual.localPosition, originalPosition);
                Assert.That(f.Captures, Is.Zero);
                AssertSpatialSound(f);
            }
        }

        private static void CheckFootstepApparition(AnomalyDefinition definition, ObjectCatalog objects, Transform parent)
        {
            using (var f = new Fixture(definition, objects, parent))
            {
                f.Actor.Tick(new RitualPerception { AudioCue = true, PlayerSpeed = 3, Distance = 2 }, 1f / 60);
                TickFor(f.Actor, new RitualPerception { Distance = 2 }, 2.1, 60);
                Assert.That(f.Recognitions, Is.Zero, "Stopping must still be followed by turning around.");
                f.Actor.Tick(new RitualPerception { Distance = 2, LookingAtAudioCue = true }, 1f / 60);
                Assert.That(f.Recognitions, Is.EqualTo(1));
                var apparition = f.Actor.transform.Find("A place just vacated");
                Assert.That(apparition, Is.Not.Null);
                Assert.That(apparition.gameObject.activeSelf, Is.True);
                Assert.That(apparition.GetComponentsInChildren<Renderer>(), Has.Length.EqualTo(6));
                Assert.That(apparition.GetComponentsInChildren<Collider>().All(collider => !collider.enabled), Is.True);
                TickFor(f.Actor, new RitualPerception { Distance = 2 }, 1.6, 60);
                Assert.That(apparition.gameObject.activeSelf, Is.False);
                Assert.That(f.Captures, Is.Zero);
                AssertSpatialSound(f);
            }
        }

        private static void AssertSpatialSound(Fixture fixture)
        {
            var sources = fixture.Root.GetComponentsInChildren<AudioSource>(true);
            Assert.That(sources, Is.Not.Empty);
            Assert.That(sources.All(source => source.spatialBlend == 1 && source.clip != null), Is.True);
            Assert.That(fixture.Subtitles, Is.GreaterThan(0), "Important recognition sounds provide an accessibility caption.");
        }

        private static void AssertVector(Vector3 actual, Vector3 expected, float tolerance = .001f)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(tolerance));
        }

        private static void TickFor(AnomalyActor actor, RitualPerception perception, double seconds, int fps)
        {
            for (int i = 0; i < (int)Math.Ceiling(seconds * fps - .00001); ++i) actor.Tick(perception, 1f / fps);
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root;
            public readonly AnomalyActor Actor;
            public readonly Transform Visual;
            public readonly Transform Player;
            public readonly Camera Camera;
            public readonly BoxCollider Collider;
            public int Recognitions;
            public int Captures;
            public int Subtitles;

            public Fixture(AnomalyDefinition definition, ObjectCatalog objects, Transform parent, bool anomaly = true)
            {
                Root = new GameObject("Test · " + definition.anomalyId);
                Root.transform.SetParent(parent, false);
                Player = new GameObject("Observer").transform;
                Player.SetParent(Root.transform, false);
                Player.position = new Vector3(0, 0, -8);
                Camera = new GameObject("Observer camera").AddComponent<Camera>();
                Camera.enabled = false;
                Camera.transform.SetParent(Player, false);
                Camera.transform.localPosition = Vector3.up * 1.6f;
                var placed = objects.Create(new StageItem
                {
                    prefabId = definition.prefabId,
                    position = new Float3(0, .5f, 0),
                    isAnomaly = anomaly
                }, Root.transform);
                Visual = placed.VisualRoot;
                Collider = placed.GetComponent<BoxCollider>();
                Actor = placed.gameObject.AddComponent<AnomalyActor>();
                Actor.Initialize(definition, anomaly, Visual, Player, Camera, 4.5f);
                Actor.Recognized += _ => Recognitions++;
                Actor.Caught += _ => Captures++;
                Actor.Subtitle += _ => Subtitles++;
                Physics.SyncTransforms();
            }

            public void Dispose() { Root.SetActive(false); Object.Destroy(Root); }
        }
    }
}
