using Door666.Core;
using NUnit.Framework;

namespace Door666.Tests
{
    public sealed class RoundSessionTests
    {
        [TestCase(0, DoorChoice.Forward, true)]
        [TestCase(0, DoorChoice.Backward, false)]
        [TestCase(1, DoorChoice.Forward, false)]
        [TestCase(1, DoorChoice.Backward, true)]
        [TestCase(3, DoorChoice.Backward, true)]
        public void AnswerDependsOnlyOnActualPlacedAnomalyCount(int count, DoorChoice choice, bool correct)
        {
            var run = NewRun();
            run.BeginRound(1, count);
            var result = run.Decide(choice);
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.IsCorrect, Is.EqualTo(correct));
            Assert.That(run.Streak, Is.EqualTo(correct ? 1 : 0));
        }

        [Test]
        public void SixConsecutiveCorrectAnswersEscapeAndWrongAnswerResetsProgress()
        {
            var run = NewRun();
            for (int i = 0; i < 4; i++) Answer(run, true);
            Answer(run, false);
            Assert.That(run.Streak, Is.Zero);
            for (int i = 0; i < 5; i++) Answer(run, true);
            Assert.That(run.EndReason, Is.EqualTo(RunEndReason.None));
            Answer(run, true);
            Assert.That(run.Streak, Is.EqualTo(6));
            Assert.That(run.EndReason, Is.EqualTo(RunEndReason.Escaped));
            Assert.That(run.RunActive, Is.False);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void TimeoutOccursAt180SecondsAcrossFrameRates(int framesPerSecond)
        {
            var run = NewRun();
            run.BeginRound(1, 0);
            for (int frame = 0; frame < 180 * framesPerSecond - 1; frame++) run.Tick(1.0 / framesPerSecond);
            Assert.That(run.RoundActive, Is.True);
            run.Tick(1.0 / framesPerSecond);
            Assert.That(run.EndReason, Is.EqualTo(RunEndReason.TimeExpired));
            Assert.That(run.RemainingSeconds, Is.Zero);
            Assert.That(run.Decide(DoorChoice.Forward).Accepted, Is.False);
        }

        [Test]
        public void TimerStartsAfterPlacementAndResetsForEachRound()
        {
            var run = NewRun();
            run.Tick(200);
            Assert.That(run.EndReason, Is.EqualTo(RunEndReason.None));
            run.BeginRound(1, 0);
            run.Tick(100);
            Assert.That(run.RemainingSeconds, Is.EqualTo(80));
            run.Decide(DoorChoice.Forward);
            run.Tick(100);
            Assert.That(run.EndReason, Is.EqualTo(RunEndReason.None));
            run.BeginRound(2, 1);
            Assert.That(run.RemainingSeconds, Is.EqualTo(180));
        }

        [Test]
        public void DoorDecisionLocksOutRepeatedInputAndCaptureImmediately()
        {
            var run = NewRun();
            run.BeginRound(1, 1);
            Assert.That(run.Decide(DoorChoice.Backward).Accepted, Is.True);
            Assert.That(run.Decide(DoorChoice.Backward).Accepted, Is.False);
            Assert.That(run.Catch("叩き起こし"), Is.False);
            Assert.That(run.Streak, Is.EqualTo(1));
            Assert.That(run.EndReason, Is.EqualTo(RunEndReason.None));
        }

        [Test]
        public void CaptureRetainsNameAndEndingAndStartingOverClearsAllRunState()
        {
            var run = NewRun();
            Answer(run, true);
            run.BeginRound(2, 1);
            Assert.That(run.Catch("叩き起こし", "doll-row"), Is.True);
            Assert.That(run.EndReason, Is.EqualTo(RunEndReason.Caught));
            Assert.That(run.CapturedBy, Is.EqualTo("叩き起こし"));
            Assert.That(run.EndingId, Is.EqualTo("doll-row"));
            run.Tick(1000);
            Assert.That(run.EndReason, Is.EqualTo(RunEndReason.Caught));
            run.StartRun();
            Assert.That(run.Streak, Is.Zero);
            Assert.That(run.RoundsPlayed, Is.Zero);
            Assert.That(run.CurrentSceneId, Is.Zero);
            Assert.That(run.EndReason, Is.EqualTo(RunEndReason.None));
            Assert.That(run.CapturedBy, Is.Empty);
            Assert.That(run.EndingId, Is.Empty);
            Assert.That(run.RoundActive, Is.False);
        }

        private static RunSession NewRun() { var run = new RunSession(); run.StartRun(); return run; }
        private static void Answer(RunSession run, bool correct)
        {
            run.BeginRound(1, 0);
            run.Decide(correct ? DoorChoice.Forward : DoorChoice.Backward);
        }
    }
}
