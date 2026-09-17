using System.Collections.Generic;
using Door666.Core;
using NUnit.Framework;

namespace Door666.Tests
{
    public sealed class StageValidatorTests
    {
        [Test]
        public void EmptyRoomAndInvalidNumberAreRejected()
        {
            var result = Validate(new StageData());
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Count.EqualTo(2));
        }

        [Test]
        public void UnknownPrefabIsWarningOnlyAndDataIsUntouched()
        {
            var stage = Room(Item("future-anomaly", true, 8));
            var result = Validate(stage);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Warnings, Has.Count.EqualTo(1));
            Assert.That(stage.items[0].prefabId, Is.EqualTo("future-anomaly"));
            Assert.That(stage.items[0].isAnomaly, Is.True);
        }

        [Test]
        public void AnomalyCapCountsUnknownObjectsAsWell()
        {
            var stage = Room(Item("known", true, 8), Item("known", true, 8), Item("known", true, 8), Item("unknown", true, 8));
            var result = Validate(stage);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("3個"));
            Assert.That(result.Warnings, Has.Count.EqualTo(1));
        }

        [Test]
        public void ChaserCapIsGlobalEvenForDifferentDefinitions()
        {
            var definitions = Definitions();
            definitions.Add("chaser-a", new StageDefinitionMetadata { IsChaser = true });
            definitions.Add("chaser-b", new StageDefinitionMetadata { IsChaser = true });
            var result = StageValidator.Validate(Room(Item("chaser-a", true, 8), Item("chaser-b", true, 10)), definitions, Context());
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("1体"));
        }

        [Test]
        public void NormalVersionsDoNotCountAsThreatsOrOfficialOnlyAnomalies()
        {
            var definitions = Definitions();
            definitions.Add("restricted", new StageDefinitionMetadata
                { IsChaser = true, UserStageAllowed = false, AutomaticallyActivates = true });
            var result = StageValidator.Validate(Room(Item("restricted", false, 0), Item("restricted", false, 0)), definitions, Context());
            Assert.That(result.IsValid, Is.True);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void OfficialOnlyAnomaliesAreRestrictedToOfficialStages(bool userStage, bool valid)
        {
            var definitions = Definitions();
            definitions.Add("official", new StageDefinitionMetadata { UserStageAllowed = false });
            var context = Context(); context.IsUserStage = userStage;
            var result = StageValidator.Validate(Room(Item("official", true, 8)), definitions, context);
            Assert.That(result.IsValid, Is.EqualTo(valid));
        }

        [TestCase(2.49f, false)]
        [TestCase(2.5f, false)]
        [TestCase(2.51f, true)]
        public void AutomaticActivationCannotReachSpawnProtectionBoundary(float distance, bool valid)
        {
            var definitions = Definitions();
            definitions.Add("automatic", new StageDefinitionMetadata { AutomaticallyActivates = true });
            var result = StageValidator.Validate(Room(Item("automatic", true, distance)), definitions, Context());
            Assert.That(result.IsValid, Is.EqualTo(valid));
        }

        [Test]
        public void LargerAutomaticRadiusIsAlsoCheckedAtSpawn()
        {
            var definitions = Definitions();
            definitions.Add("automatic", new StageDefinitionMetadata { AutomaticallyActivates = true, AutoActivationRadius = 3 });
            var result = StageValidator.Validate(Room(Item("automatic", true, 2.9f)), definitions, Context());
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void AnomalyColliderOverlapIsRejectedButNormalFurnitureIsAllowed()
        {
            var context = Context();
            context.DoorBounds.Add(new StageBounds(new Float3(0, 0, 8), new Float3(0.8f, 1.5f, 0.2f)));
            var anomaly = StageValidator.Validate(Room(Item("known", true, 8)), Definitions(), context);
            var normal = StageValidator.Validate(Room(Item("known", false, 8)), Definitions(), context);
            Assert.That(anomaly.IsValid, Is.False);
            Assert.That(anomaly.Errors, Has.Some.Contains("扉"));
            Assert.That(normal.IsValid, Is.True);
        }

        [Test]
        public void ColliderRotationIsAppliedBeforeTestingDoorOverlap()
        {
            var definitions = Definitions();
            definitions["known"].HalfExtents = new Float3(2, 0.2f, 0.1f);
            var context = Context();
            context.DoorBounds.Add(new StageBounds(new Float3(0, 0, 9.5f), new Float3(0.2f, 1, 0.2f)));
            var item = Item("known", true, 8);
            Assert.That(StageValidator.Validate(Room(item), definitions, context).IsValid, Is.True);
            item.rotation = new Float3(0, 90, 0);
            Assert.That(StageValidator.Validate(Room(item), definitions, context).IsValid, Is.False);
        }

        [Test]
        public void HeavilyOverlappedAnomaliesAreAllowedOnlyBeyondTheClearOnes()
        {
            var context = Context();
            context.MaximumAnomalies = 6;
            var stage = Room(Item("known", true, 8), Item("known", true, 9), Item("known", true, 10), Item("known", true, 11), Item("known", false, 12));
            // Four anomalies leave room for one heavily overlapped anomaly; ordinary furniture never counts.
            context.OverlapRatios = new[] { 0f, 0.1f, 0.25f, 0.6f, 0.7f };
            Assert.That(StageValidator.Validate(stage, Definitions(), context).IsValid, Is.True);
            context.OverlapRatios = new[] { 0f, 0.3f, 0.25f, 0.6f, 0f };
            var tooMany = StageValidator.Validate(stage, Definitions(), context);
            Assert.That(tooMany.IsValid, Is.False);
            Assert.That(tooMany.Errors, Has.Some.Contains("重なりの大きい異変 2 個"));
        }

        [Test]
        public void NoAnomalyMayBeHeavilyOverlappedUntilTheClearOnesArePlaced()
        {
            var context = Context();
            context.OverlapRatios = new[] { 0f, 0.5f };
            Assert.That(StageValidator.Validate(Room(Item("known", true, 8), Item("known", true, 9)), Definitions(), context).IsValid, Is.False);
        }

        [Test]
        public void ItemsBuriedBeyondTheMaximumAreRejectedEvenWhenOrdinary()
        {
            var context = Context();
            context.OverlapRatios = new[] { 0.76f };
            var result = StageValidator.Validate(Room(Item("known", false, 8)), Definitions(), context);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("76%"));
        }

        [Test]
        public void InvalidCoordinatesAndMissingPrefabIdsAreRejected()
        {
            var invalid = Item("known", true, 8);
            invalid.position = new Float3(float.NaN, 0, 8);
            var result = Validate(Room(invalid, Item(" ", false, 8)));
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Count.EqualTo(2));
        }

        private static StageValidationContext Context() => new StageValidationContext { PlayerSpawn = new Float3(0, 0, 0) };
        private static Dictionary<string, StageDefinitionMetadata> Definitions() => new Dictionary<string, StageDefinitionMetadata>
        {
            ["known"] = new StageDefinitionMetadata()
        };
        private static StageValidationResult Validate(StageData stage) => StageValidator.Validate(stage, Definitions(), Context());
        private static StageData Room(params StageItem[] items) => new StageData { sceneId = 1, items = new List<StageItem>(items) };
        private static StageItem Item(string prefabId, bool anomaly, float z) => new StageItem
        {
            prefabId = prefabId, isAnomaly = anomaly, position = new Float3(0, 0, z)
        };
    }
}
