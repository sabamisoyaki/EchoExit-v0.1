using System;
using Door666.Core;
using NUnit.Framework;
using UnityEngine;

namespace Door666.Tests
{
    public sealed class RitualRuntimeTests
    {
        private AnomalyCatalog catalog;

        [SetUp]
        public void LoadDefinitions()
        {
            var json = Resources.Load<TextAsset>("AnomalyDefinitions");
            Assert.That(json, Is.Not.Null);
            catalog = AnomalyCatalog.FromJson(json.text);
        }

        [Test]
        public void CatalogHasSevenDistinctDataDrivenDefinitions()
        {
            Assert.That(catalog.Definitions.Count, Is.EqualTo(7));
            Assert.That(catalog.FindByPrefab("anomaryShirinkBox").anomalyId, Is.EqualTo("ANM-002"));
            Assert.That(catalog.FindByPrefab("unavailable"), Is.Null);
            Assert.That(catalog.FindById("ANM-004").threatProfile.graceSeconds, Is.GreaterThanOrEqualTo(1.2));
        }

        [TestCase("ANM-001", 2.5)]
        [TestCase("ANM-002", 1.0)]
        [TestCase("ANM-003", 3.0)]
        [TestCase("ANM-004", 1.0)]
        [TestCase("ANM-005", 2.2)]
        [TestCase("ANM-006", 3.5)]
        [TestCase("ANM-008", 3.3)]
        public void CorrectRitualHasEquivalentTimingAt30_60_120Fps(string id, double expected)
        {
            double earliest = double.MaxValue;
            double latest = 0;
            foreach (int fps in new[] { 30, 60, 120 })
            {
                var ritual = new RitualRuntime(catalog.FindById(id));
                for (int frame = 0; frame < fps * 9; ++frame)
                {
                    double time = (double)frame / fps;
                    ritual.Tick(ScriptedPerception(id, time, 1.0 / fps), 1.0 / fps);
                    if (ritual.IsRecognized) break;
                }
                Assert.That(ritual.IsRecognized, Is.True, id + " at " + fps + " fps");
                Assert.That(ritual.RecognizedAt, Is.EqualTo(expected).Within(0.075), id + " at " + fps + " fps");
                earliest = Math.Min(earliest, ritual.RecognizedAt);
                latest = Math.Max(latest, ritual.RecognizedAt);
            }
            Assert.That(latest - earliest, Is.LessThanOrEqualTo(0.05), "Frame rates must not change ritual duration.");
        }

        [Test]
        public void EveryNormalVersionIgnoresAllCandidateActions()
        {
            foreach (var definition in catalog.Definitions)
            {
                var normal = new RitualRuntime(definition, false);
                for (int frame = 0; frame < 60 * 20; ++frame)
                {
                    var p = ScriptedPerception(definition.anomalyId, frame / 60.0 % 9, 1.0 / 60);
                    p.Hit = true;
                    p.AudioCue = true;
                    Assert.That(normal.Tick(p, 1.0 / 60), Is.False, definition.anomalyId);
                }
                Assert.That(normal.IsRecognized, Is.False);
                Assert.That(normal.StepIndex, Is.Zero);
            }
        }

        [Test]
        public void InterruptedBoxGazeMustRestartAndLookingBackRestartsOnlyLookAway()
        {
            var ritual = new RitualRuntime(catalog.FindById("ANM-001"));
            Run(ritual, new RitualPerception { Gazing = true }, 1.4);
            Run(ritual, new RitualPerception(), 0.1);
            Run(ritual, new RitualPerception { Gazing = true }, 1.4);
            Assert.That(ritual.StepIndex, Is.Zero);
            Run(ritual, new RitualPerception { Gazing = true }, 0.1);
            Run(ritual, new RitualPerception(), 0.8);
            Run(ritual, new RitualPerception { Gazing = true }, 0.1);
            Run(ritual, new RitualPerception(), 0.9);
            Assert.That(ritual.IsRecognized, Is.False);
            Run(ritual, new RitualPerception(), 0.1);
            Assert.That(ritual.IsRecognized, Is.True);
        }

        [Test]
        public void DollRequiresLeavingWithinSixSecondsAndCanRetry()
        {
            var ritual = new RitualRuntime(catalog.FindById("ANM-003"));
            Run(ritual, new RitualPerception { Distance = 2 }, 0.1);
            Run(ritual, new RitualPerception { Distance = 3 }, 6.2);
            Run(ritual, new RitualPerception { Distance = 5 }, 0.2);
            Assert.That(ritual.IsRecognized, Is.False);
            Run(ritual, new RitualPerception { Distance = 2 }, 0.1);
            Run(ritual, new RitualPerception { Distance = 5 }, 0.1);
            Assert.That(ritual.IsRecognized, Is.True);
        }

        [Test]
        public void ChairGazeDeadlineCannotBeExtendedByRepeatedInterruptions()
        {
            var ritual = new RitualRuntime(catalog.FindById("ANM-005"));
            Run(ritual, new RitualPerception { Distance = 1, LocalPlayerZ = 1 }, 0.1);
            Run(ritual, new RitualPerception { Distance = 1, LocalPlayerZ = -1 }, 0.1);
            for (int i = 0; i < 6; ++i)
            {
                Run(ritual, new RitualPerception { Distance = 1, LocalPlayerZ = -1, Gazing = true }, 0.7);
                Run(ritual, new RitualPerception { Distance = 1, LocalPlayerZ = -1 }, 0.1);
            }
            Assert.That(ritual.IsRecognized, Is.False);
            Assert.That(ritual.StepIndex, Is.Zero);
        }

        [Test]
        public void CrossThresholdSupportsSmallFrameToFrameStepsAcrossDeadZone()
        {
            var ritual = new RitualRuntime(catalog.FindById("ANM-005"));
            ritual.Tick(new RitualPerception { Distance = 1, LocalPlayerZ = 0.01f }, 1.0 / 120);
            ritual.Tick(new RitualPerception { Distance = 1, LocalPlayerZ = -0.08f }, 1.0 / 120);
            ritual.Tick(new RitualPerception { Distance = 1, LocalPlayerZ = -0.16f }, 1.0 / 120);
            Assert.That(ritual.StepIndex, Is.EqualTo(1));
        }

        [Test]
        public void WallRequiresTheEntireStillPeriodToBeNearAndLookingAway()
        {
            var ritual = new RitualRuntime(catalog.FindById("ANM-006"));
            Run(ritual, new RitualPerception { Distance = 2, Gazing = true }, 4);
            Assert.That(ritual.IsRecognized, Is.False);
            Run(ritual, new RitualPerception { Distance = 2 }, 2);
            Run(ritual, new RitualPerception { Distance = 4 }, 0.1);
            Run(ritual, new RitualPerception { Distance = 2 }, 2);
            Assert.That(ritual.IsRecognized, Is.False);
            Run(ritual, new RitualPerception { Distance = 2 }, 0.6);
            Assert.That(ritual.IsRecognized, Is.True);
        }

        [Test]
        public void ExtraFootstepRejectsLateStopAndInterruptedStillness()
        {
            var definition = catalog.FindById("ANM-008");
            var late = new RitualRuntime(definition);
            late.Tick(new RitualPerception { AudioCue = true, PlayerSpeed = 3 }, 0.01);
            Run(late, new RitualPerception { PlayerSpeed = 3 }, 0.9);
            Run(late, new RitualPerception { LookingAtAudioCue = true }, 4);
            Assert.That(late.IsRecognized, Is.False);
            var interrupted = new RitualRuntime(definition);
            interrupted.Tick(new RitualPerception { AudioCue = true, PlayerSpeed = 3 }, 0.01);
            Run(interrupted, new RitualPerception(), 1.5);
            Run(interrupted, new RitualPerception { PlayerSpeed = 2 }, 0.1);
            Run(interrupted, new RitualPerception { LookingAtAudioCue = true }, 4);
            Assert.That(interrupted.IsRecognized, Is.False);
            var movedAfterHolding = new RitualRuntime(definition);
            movedAfterHolding.Tick(new RitualPerception { AudioCue = true, PlayerSpeed = 3 }, 0.01);
            Run(movedAfterHolding, new RitualPerception(), 2.2);
            Assert.That(movedAfterHolding.StepIndex, Is.EqualTo(3));
            Run(movedAfterHolding, new RitualPerception { PlayerSpeed = 2 }, 0.1);
            Run(movedAfterHolding, new RitualPerception { LookingAtAudioCue = true }, 4);
            Assert.That(movedAfterHolding.IsRecognized, Is.False);
        }

        [Test]
        public void HitOnlyWorksWhileTheActualTargetIsGazed()
        {
            var ritual = new RitualRuntime(catalog.FindById("ANM-004"));
            Run(ritual, new RitualPerception { Hit = true }, 3);
            Assert.That(ritual.IsRecognized, Is.False);
            Assert.That(ritual.Tick(new RitualPerception { Hit = true, Gazing = true }, 0.02), Is.True);
            Assert.That(ritual.Tick(new RitualPerception { Hit = true, Gazing = true }, 0.02), Is.False, "Recognition emits once.");
        }

        private static void Run(RitualRuntime ritual, RitualPerception p, double seconds)
        {
            for (int i = 0; i < (int)Math.Round(seconds * 100); ++i) ritual.Tick(p, 0.01);
        }

        private static RitualPerception ScriptedPerception(string id, double time, double dt)
        {
            var p = new RitualPerception { Distance = 2, LocalPlayerZ = 1 };
            switch (id)
            {
                case "ANM-001": p.Gazing = time < 1.5 - 0.00001; break;
                case "ANM-002": p.Distance = time < 1 ? 6 : 2; break;
                case "ANM-003": p.Distance = time < 1 || time >= 3 ? 5 : 2; break;
                case "ANM-004": p.Gazing = true; p.Hit = time >= 1 && time < 1 + dt * 1.01; break;
                case "ANM-005": p.LocalPlayerZ = time < 1 ? 1 : -1; p.Gazing = time >= 1.2 - 0.00001; break;
                case "ANM-006": p.Gazing = time < 1; p.PlayerSpeed = time < 1 ? 2 : 0; break;
                case "ANM-008": p.AudioCue = time >= 1 && time < 1 + dt * 1.01; p.PlayerSpeed = time < 1.2 ? 2 : 0; p.LookingAtAudioCue = time >= 3.3 - 0.00001; break;
            }
            return p;
        }
    }
}
