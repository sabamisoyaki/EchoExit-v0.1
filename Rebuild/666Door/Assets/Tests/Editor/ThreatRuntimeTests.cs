using Door666.Core;
using NUnit.Framework;

namespace Door666.Tests
{
    public sealed class ThreatRuntimeTests
    {
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void CannotCaptureBeforeMinimumGraceAtAnyFrameRate(int fps)
        {
            var threat = new ThreatRuntime(new ThreatProfile { enabled = true, graceSeconds = 0.1f }, 4.5f);
            threat.Recognize();
            for (int frame = 0; frame < fps * 1.2 - 1; ++frame)
                Assert.That(threat.Tick(1.0 / fps, 0.1f, false), Is.False);
            Assert.That(threat.Tick(1.0 / fps, 0.1f, false), Is.True);
        }

        [Test]
        public void RecognitionAnimationAndDoorInteractionAlwaysProtectPlayer()
        {
            var threat = new ThreatRuntime(new ThreatProfile { enabled = true }, 4.5f);
            threat.Recognize();
            Assert.That(threat.Tick(3, 0, true), Is.False);
            Assert.That(threat.CanChase, Is.False);
            Assert.That(threat.Tick(1, 0, false, true), Is.False);
            Assert.That(threat.Tick(0.01, 0, false), Is.True);
        }

        [Test]
        public void ChaseNeverExceedsThreeQuartersOfPlayerSpeed()
        {
            var threat = new ThreatRuntime(new ThreatProfile { enabled = true, speed = 99, maxPlayerSpeedRatio = 1 }, 3.2f);
            Assert.That(threat.Speed, Is.EqualTo(2.4f).Within(0.0001));
            var normal = new ThreatRuntime(new ThreatProfile { enabled = true, speed = 2.8f }, 4.5f);
            Assert.That(normal.Speed, Is.EqualTo(2.8f));
        }

        [Test]
        public void NormalObjectsAndUnrecognizedThreatsCannotCapture()
        {
            var normal = new ThreatRuntime(new ThreatProfile(), 4.5f);
            normal.Recognize();
            Assert.That(normal.Tick(99, 0, false), Is.False);
            var sleeping = new ThreatRuntime(new ThreatProfile { enabled = true }, 4.5f);
            Assert.That(sleeping.Tick(99, 0, false), Is.False);
        }

        [Test]
        public void CaptureRequiresDistanceAndEmitsOnce()
        {
            var threat = new ThreatRuntime(new ThreatProfile { enabled = true }, 4.5f);
            threat.Recognize();
            Assert.That(threat.Tick(2, 2, false), Is.False);
            Assert.That(threat.CanChase, Is.True);
            Assert.That(threat.Tick(0.1, 1, false), Is.True);
            Assert.That(threat.Tick(0.1, 1, false), Is.False);
        }
    }
}
