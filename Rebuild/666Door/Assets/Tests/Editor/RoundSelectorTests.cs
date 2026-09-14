using System.Linq;
using Door666.Core;
using NUnit.Framework;

namespace Door666.Tests
{
    public sealed class RoundSelectorTests
    {
        [TestCase(0.0, true)]
        [TestCase(0.665999, true)]
        [TestCase(0.666, false)]
        [TestCase(0.99999, false)]
        public void ProbabilityBoundaryIsExactlyPointSixSixSix(double roll, bool expected)
        {
            var selected = RoundSelector.Select(new[] { Room(1, true) }, null, null, roll, 0);
            Assert.That(selected.IncludeAnomalies, Is.EqualTo(expected));
            Assert.That(selected.Items.Count(item => item.isAnomaly), Is.EqualTo(expected ? 1 : 0));
        }

        [Test]
        public void AnomalySelectionUsesItemsEvenWhenLegacyFlagDisagrees()
        {
            var normal = Room(1, false); normal.anomalyHouse = true;
            var anomaly = Room(2, true); anomaly.anomalyHouse = false;
            for (int seed = 0; seed < 10; seed++)
                Assert.That(RoundSelector.Select(new[] { normal, anomaly }, null, null, 0.1, seed).Stage.sceneId, Is.EqualTo(2));
        }

        [Test]
        public void NormalRoundCanUseAnyRoomButNeverCopiesItsAnomalies()
        {
            var rooms = new[] { Room(1, false), Room(2, true) };
            var selections = Enumerable.Range(0, 2).Select(seed => RoundSelector.Select(rooms, null, null, 0.9, seed)).ToList();
            Assert.That(selections.Select(result => result.Stage.sceneId), Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(selections.SelectMany(result => result.Items).Any(item => item.isAnomaly), Is.False);
            Assert.That(rooms[1].items, Has.Count.EqualTo(2), "Filtering must leave saved room data intact.");
        }

        [TestCase(0.1)]
        [TestCase(0.9)]
        public void PreviousRoomIsExcludedWhenAnAlternativeExists(double roll)
        {
            var rooms = new[] { Room(1, true), Room(2, true) };
            for (int seed = -2; seed < 5; seed++)
                Assert.That(RoundSelector.Select(rooms, 1, null, roll, seed).Stage.sceneId, Is.EqualTo(2));
        }

        [Test]
        public void OnlyCandidateIsAllowedToRepeat()
        {
            var room = Room(4, true);
            Assert.That(RoundSelector.Select(new[] { room }, 4, room, 0.2, 5).Stage.sceneId, Is.EqualTo(4));
        }

        [Test]
        public void MissingAnomalyCandidateFallsBackToNormalRoom()
        {
            var selected = RoundSelector.Select(new[] { Room(2, false) }, null, null, 0.1, 0);
            Assert.That(selected.UsedFallback, Is.True);
            Assert.That(selected.IncludeAnomalies, Is.False);
            Assert.That(selected.Items.Any(item => item.isAnomaly), Is.False);
        }

        [Test]
        public void EmptyRepositoryReusesCurrentRoomWithoutLosingItsAnswer()
        {
            var current = Room(3, true);
            var selected = RoundSelector.Select(new StageData[0], 3, current, 0.99, 0);
            Assert.That(selected.ReuseCurrent, Is.True);
            Assert.That(selected.Stage.sceneId, Is.EqualTo(3));
            Assert.That(selected.Items.Any(item => item.isAnomaly), Is.True);
        }

        [Test]
        public void NoCandidatesAndNoCurrentRoomReturnsExplicitEmptySelection()
        {
            var selected = RoundSelector.Select(null, null, null, 0.2, 0);
            Assert.That(selected.ReuseCurrent, Is.True);
            Assert.That(selected.Stage, Is.Null);
            Assert.That(selected.Items, Is.Empty);
        }

        private static StageData Room(int id, bool anomaly)
        {
            var stage = new StageData { sceneId = id, anomalyHouse = anomaly };
            stage.items.Add(new StageItem { prefabId = "normal", isAnomaly = false });
            if (anomaly) stage.items.Add(new StageItem { prefabId = "anomaly", isAnomaly = true });
            return stage;
        }
    }
}
