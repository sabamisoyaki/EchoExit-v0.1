using System;
using System.IO;
using System.Linq;
using Door666.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Door666.Tests
{
    public sealed class StageRepositoryTests
    {
        private string temporaryRoot;
        private string defaults;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "Door666-StageTests-" + Guid.NewGuid().ToString("N"));
            defaults = File.ReadAllText(Path.Combine(Application.dataPath, "Resources", "DefaultAnomalies.json"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
        }

        [Test]
        public void BundledNineRoomsRoundTripWithEverySchemaFieldAndValue()
        {
            var result = StageRepository.Parse(defaults);
            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.Data.scenes.Count, Is.EqualTo(9));
            CompareJsonValues(JToken.Parse(defaults), JToken.Parse(StageRepository.Serialize(result.Data)));
        }

        [Test]
        public void DuplicateRoomsMergeTheirItemsAndUpsertReplacesAllDuplicateBlocks()
        {
            var data = new StageFile();
            data.scenes.Add(Room(3, "first", false));
            data.scenes.Add(Room(1, "normal", false));
            data.scenes.Add(Room(3, "second", true));
            var warnings = new System.Collections.Generic.List<string>();
            var merged = StageRepository.Merge(data, warnings);
            Assert.That(merged.scenes.Select(room => room.sceneId), Is.EqualTo(new[] { 1, 3 }));
            Assert.That(merged.scenes[1].items.Select(item => item.prefabId), Is.EqualTo(new[] { "first", "second" }));
            Assert.That(warnings, Has.Count.EqualTo(1));

            var updated = StageRepository.Upsert(data, Room(3, "replacement", false));
            Assert.That(updated.scenes.Count, Is.EqualTo(2));
            Assert.That(updated.scenes[1].items.Single().prefabId, Is.EqualTo("replacement"));
            Assert.That(updated.scenes[1].anomalyHouse, Is.False);
            Assert.That(data.scenes, Has.Count.EqualTo(3), "Saving must not mutate the loaded draft.");
        }

        [Test]
        public void NextRoomNumberUsesTheFirstGapStartingAtOne()
        {
            var data = new StageFile();
            data.scenes.Add(Room(4, "box", false));
            data.scenes.Add(Room(1, "box", false));
            data.scenes.Add(Room(2, "box", false));
            Assert.That(StageRepository.NextSceneId(data), Is.EqualTo(3));
            Assert.That(StageRepository.NextSceneId(new StageFile()), Is.EqualTo(1));
        }

        [Test]
        public void UnknownPrefabAndFutureMetadataSurviveEditingAnotherRoom()
        {
            var document = JObject.Parse(defaults);
            document["futureVersion"] = "v-next";
            var first = (JObject)document["scenes"][0];
            first["author"] = "テスト作者";
            var item = (JObject)first["items"][0];
            item["prefabId"] = "future/unknown-object";
            item["customData"] = new JObject { ["keep"] = 17 };
            var data = StageRepository.Parse(document.ToString()).Data;
            var updated = StageRepository.Upsert(data, Room(2, "replacement", true));
            var saved = JObject.Parse(StageRepository.Serialize(updated));
            Assert.That(saved["futureVersion"].Value<string>(), Is.EqualTo("v-next"));
            Assert.That(saved["scenes"][0]["author"].Value<string>(), Is.EqualTo("テスト作者"));
            Assert.That(saved["scenes"][0]["items"][0]["prefabId"].Value<string>(), Is.EqualTo("future/unknown-object"));
            Assert.That(saved["scenes"][0]["items"][0]["customData"]["keep"].Value<int>(), Is.EqualTo(17));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrWhitespaceSaveExpandsDefaultsExactlyOnce(bool emptyFile)
        {
            var repository = new StageRepository(temporaryRoot);
            Assert.That(Directory.Exists(temporaryRoot), Is.False, "Constructing a repository has no filesystem side effects.");
            if (emptyFile)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(repository.SavePath));
                File.WriteAllText(repository.SavePath, " \r\n\t");
            }
            var created = repository.LoadOrCreate(defaults);
            Assert.That(created.Success, Is.True, created.Error);
            Assert.That(created.CreatedDefaults, Is.True);
            Assert.That(File.Exists(repository.SavePath), Is.True);
            Assert.That(repository.SavePath, Does.EndWith(Path.Combine("Saves", "anomalies.json")));

            Assert.That(repository.SaveStage(Room(1, "my-custom-object", false)).Success, Is.True);
            var loaded = new StageRepository(temporaryRoot).LoadOrCreate(defaults);
            Assert.That(loaded.CreatedDefaults, Is.False);
            Assert.That(loaded.Data.scenes[0].items.Single().prefabId, Is.EqualTo("my-custom-object"));
        }

        [TestCase("{ invalid-json")]
        [TestCase("null")]
        [TestCase("{}")]
        [TestCase("{\"scenes\":null}")]
        [TestCase("{\"scenes\":[{\"sceneId\":1,\"anomalyHouse\":true,\"items\":[null]}]}")]
        [TestCase("{\"scenes\":[],\"scenes\":[]}")]
        public void DamagedSaveIsPreservedAndCannotBeOverwritten(string damaged)
        {
            var repository = new StageRepository(temporaryRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(repository.SavePath));
            File.WriteAllText(repository.SavePath, damaged);
            var loaded = repository.LoadOrCreate(defaults);
            Assert.That(loaded.Success, Is.False);
            Assert.That(loaded.IsReadOnly, Is.True);
            Assert.That(loaded.Data.scenes.Count, Is.EqualTo(9), "Bundled rooms remain available for play.");
            Assert.That(repository.SaveStage(Room(1, "replacement", false)).Success, Is.False);
            Assert.That(File.ReadAllText(repository.SavePath), Is.EqualTo(damaged));
        }

        [Test]
        public void FileDamagedAfterLoadingIsAlsoProtected()
        {
            var repository = new StageRepository(temporaryRoot);
            repository.LoadOrCreate(defaults);
            File.WriteAllText(repository.SavePath, "damaged after load");
            var saved = repository.SaveStage(Room(1, "replacement", false));
            Assert.That(saved.Success, Is.False);
            Assert.That(repository.IsReadOnly, Is.True);
            Assert.That(File.ReadAllText(repository.SavePath), Is.EqualTo("damaged after load"));
        }

        [Test]
        public void SuccessfulOverwriteKeepsPreviousVersionAsBackup()
        {
            var repository = new StageRepository(temporaryRoot);
            repository.LoadOrCreate(defaults);
            string previous = File.ReadAllText(repository.SavePath);
            Assert.That(repository.SaveStage(Room(1, "replacement", false)).Success, Is.True);
            Assert.That(File.ReadAllText(repository.SavePath + ".bak"), Is.EqualTo(previous));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(repository.SavePath), "*.tmp"), Is.Empty);
        }

        [Test]
        public void MissingVectorComponentDoesNotSilentlyBecomeZero()
        {
            var json = JObject.Parse(defaults);
            ((JObject)json["scenes"][0]["items"][0]["position"]).Remove("x");
            Assert.That(StageRepository.Parse(json.ToString()).Success, Is.False);
        }

        private static StageData Room(int id, string prefab, bool anomaly)
        {
            var room = new StageData { sceneId = id, anomalyHouse = anomaly };
            room.items.Add(new StageItem { prefabId = prefab, isAnomaly = anomaly, position = new Float3(0, 0, 5) });
            return room;
        }

        private static void CompareJsonValues(JToken expected, JToken actual)
        {
            if (expected is JObject expectedObject)
            {
                Assert.That(actual, Is.TypeOf<JObject>());
                Assert.That(((JObject)actual).Properties().Select(property => property.Name),
                    Is.EquivalentTo(expectedObject.Properties().Select(property => property.Name)), expected.Path);
                foreach (var property in expectedObject.Properties()) CompareJsonValues(property.Value, actual[property.Name]);
            }
            else if (expected is JArray expectedArray)
            {
                Assert.That(actual, Is.TypeOf<JArray>());
                Assert.That(((JArray)actual).Count, Is.EqualTo(expectedArray.Count));
                for (int i = 0; i < expectedArray.Count; i++) CompareJsonValues(expectedArray[i], actual[i]);
            }
            else if (expected.Type == JTokenType.Integer || expected.Type == JTokenType.Float)
                Assert.That(actual.Value<double>(), Is.EqualTo(expected.Value<double>()).Within(0.000001), expected.Path);
            else Assert.That(JToken.DeepEquals(expected, actual), Is.True, expected.Path);
        }
    }
}
