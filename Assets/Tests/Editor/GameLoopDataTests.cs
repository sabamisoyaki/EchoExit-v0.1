using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class GameLoopDataTests
{
    [Test]
    public void ExistingJsonSchema_RoundTripsWithoutChangingFieldNames()
    {
        const string json =
            "{\"scenes\":[{\"sceneId\":3,\"anomalyHouse\":true,\"items\":[" +
            "{\"prefabId\":\"bears\",\"position\":{\"x\":1,\"y\":2,\"z\":3}," +
            "\"rotation\":{\"x\":0,\"y\":90,\"z\":0},\"isAnomaly\":true}]}]}";

        var data = JsonUtility.FromJson<SceneDataFile>(json);
        data.Normalize();

        Assert.That(data.scenes, Has.Count.EqualTo(1));
        Assert.That(data.scenes[0].sceneId, Is.EqualTo(3));
        Assert.That(data.scenes[0].items[0].prefabId, Is.EqualTo("bears"));
        Assert.That(data.scenes[0].items[0].position, Is.EqualTo(new Vector3(1f, 2f, 3f)));

        string roundTrip = JsonUtility.ToJson(data);
        StringAssert.Contains("\"scenes\"", roundTrip);
        StringAssert.Contains("\"sceneId\"", roundTrip);
        StringAssert.Contains("\"anomalyHouse\"", roundTrip);
        StringAssert.Contains("\"prefabId\"", roundTrip);
        StringAssert.Contains("\"isAnomaly\"", roundTrip);
    }

    [Test]
    public void Upsert_ReplacesEveryDuplicateSceneIdAndSortsScenes()
    {
        var data = new SceneDataFile
        {
            scenes = new List<SceneDataEntry>
            {
                CreateScene(4, "old-a"),
                CreateScene(2, "keep"),
                CreateScene(4, "old-b")
            }
        };

        data.Upsert(CreateScene(4, "replacement"));

        Assert.That(data.scenes, Has.Count.EqualTo(2));
        Assert.That(data.scenes[0].sceneId, Is.EqualTo(2));
        Assert.That(data.scenes[1].sceneId, Is.EqualTo(4));
        Assert.That(data.scenes[1].items, Has.Count.EqualTo(1));
        Assert.That(data.scenes[1].items[0].prefabId, Is.EqualTo("replacement"));
    }

    [Test]
    public void MergedScene_PreservesItemsFromDuplicateBlocksIncludingUnknownPrefabs()
    {
        var data = new SceneDataFile
        {
            scenes = new List<SceneDataEntry>
            {
                CreateScene(7, "known"),
                CreateScene(7, "missing-prefab")
            }
        };

        var merged = data.GetMergedScene(7);
        data.Upsert(merged);

        Assert.That(data.scenes, Has.Count.EqualTo(1));
        Assert.That(data.scenes[0].items, Has.Count.EqualTo(2));
        Assert.That(data.scenes[0].items.Exists(item => item.prefabId == "missing-prefab"), Is.True);
    }

    [Test]
    public void NextAvailableSceneId_UsesFirstPositiveGap()
    {
        var data = new SceneDataFile
        {
            scenes = new List<SceneDataEntry>
            {
                CreateScene(1, "one"),
                CreateScene(3, "three"),
                CreateScene(4, "four")
            }
        };

        Assert.That(data.GetNextAvailableSceneId(), Is.EqualTo(2));
    }

    [Test]
    public void PrefabResolver_SearchesAbnormalityAndRootFolders()
    {
        Assert.That(PrefabResolver.Load("ChairPrefab"), Is.Not.Null);
        Assert.That(PrefabResolver.Load("wall"), Is.Not.Null);
        Assert.That(PrefabResolver.Load("footstepEcho"), Is.Not.Null);
        Assert.That(PrefabResolver.Load("trap"), Is.Not.Null);
        Assert.That(PrefabResolver.Load("definitely-missing-prefab"), Is.Null);
    }

    [Test]
    public void RoundSelection_UsesDesiredPoolAndExcludesCurrentWhenPossible()
    {
        var result = RoundSelectionUtility.Pick(
            new[] { 1, 2 },
            new[] { 3 },
            wantAnomaly: true,
            excludeSceneId: 1,
            pickIndex: _ => 0);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.SceneId, Is.EqualTo(2));
        Assert.That(result.HasAnomaly, Is.True);
        Assert.That(result.UsedFallback, Is.False);
    }

    [Test]
    public void RoundSelection_ReportsActualClassificationWhenUsingAlternatePool()
    {
        var result = RoundSelectionUtility.Pick(
            new int[0],
            new[] { 9 },
            wantAnomaly: true,
            excludeSceneId: -1,
            pickIndex: _ => 0);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.SceneId, Is.EqualTo(9));
        Assert.That(result.HasAnomaly, Is.False);
        Assert.That(result.UsedFallback, Is.True);
    }

    [Test]
    public void RoundSelection_ReturnsInvalidWhenNoCandidatesExist()
    {
        var result = RoundSelectionUtility.Pick(
            new int[0],
            new int[0],
            wantAnomaly: false,
            excludeSceneId: -1,
            pickIndex: _ => 0);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.SceneId, Is.EqualTo(-1));
    }

    [Test]
    public void StageValidator_RejectsAnomalyCountAboveMvpLimit()
    {
        var scene = CreateScene(1, "bears");
        scene.items[0].isAnomaly = true;
        scene.items.Add(new SceneItemData { prefabId = "changeColorBox", isAnomaly = true });

        var result = StageValidator.Validate(scene, maxAnomalies: 1);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(error => error.Contains("最大1個")), Is.True);
    }

    [Test]
    public void StageValidator_AllowsNormalStageAndWarnsForUnknownPrefab()
    {
        var scene = CreateScene(2, "definitely-missing-prefab");

        var result = StageValidator.Validate(scene, maxAnomalies: 3);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Count.EqualTo(1));
    }

    [Test]
    public void StageValidator_RejectsMultipleAggressiveAnomalies()
    {
        var scene = CreateScene(10, "bears");
        scene.items[0].isAnomaly = true;
        scene.items.Add(new SceneItemData { prefabId = "bears", isAnomaly = true });

        var result = StageValidator.Validate(scene, maxAnomalies: 3);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(error => error.Contains("追跡型")), Is.True);
    }

    [Test]
    public void AnomalyProfiles_KeepDistinctRitualsAndAggression()
    {
        var colorBox = AnomalyRuntimeFactory.GetProfile("changeColorBox");
        var shrinkBox = AnomalyRuntimeFactory.GetProfile("anomaryShirinkBox");
        var returningDoll = AnomalyRuntimeFactory.GetProfile("DollPrefab");
        var bears = AnomalyRuntimeFactory.GetProfile("bears");
        var chair = AnomalyRuntimeFactory.GetProfile("ChairPrefab");
        var wall = AnomalyRuntimeFactory.GetProfile("wall");
        var footstep = AnomalyRuntimeFactory.GetProfile("footstepEcho");

        Assert.That(colorBox.RitualKind, Is.EqualTo(AnomalyRitualKind.GazeThenLookAway));
        Assert.That(shrinkBox.RitualKind, Is.EqualTo(AnomalyRitualKind.Approach));
        Assert.That(returningDoll.RitualKind, Is.EqualTo(AnomalyRitualKind.ApproachThenRetreat));
        Assert.That(bears.RitualKind, Is.EqualTo(AnomalyRitualKind.Hit));
        Assert.That(bears.Aggressive, Is.True);
        Assert.That(chair.RitualKind, Is.EqualTo(AnomalyRitualKind.CrossBehindThenGaze));
        Assert.That(wall.RitualKind, Is.EqualTo(AnomalyRitualKind.EnterRadiusLookAwayStill));
        Assert.That(footstep.RitualKind, Is.EqualTo(AnomalyRitualKind.FootstepStopThenLookBack));
        Assert.That(returningDoll.ExitDistance, Is.GreaterThan(returningDoll.ApproachDistance));
    }

    [UnityTest]
    public IEnumerator RoundFeedbackPresenter_CompletesWithoutAudioClips()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Single);
        var manager = Object.FindFirstObjectByType<GameManager>();
        Assert.That(manager, Is.Not.Null);

        var presenter = manager.GetComponent<RoundFeedbackPresenter>();
        Assert.That(presenter, Is.Not.Null);

        typeof(RoundFeedbackPresenter)
            .GetField("displayDuration", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(presenter, 0.02f);
        typeof(RoundFeedbackPresenter)
            .GetField("fadeDuration", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(presenter, 0.005f);

        yield return presenter.ShowResult(isCorrect: true, count: 1, threshold: 6);

        presenter.SetRoundTimer(29f);
        var timerText = typeof(RoundFeedbackPresenter)
            .GetField("timerText", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(presenter) as TMPro.TMP_Text;
        Assert.That(timerText, Is.Not.Null);
        Assert.That(timerText.text, Is.EqualTo("00:29"));

        var canvasGroup = typeof(RoundFeedbackPresenter)
            .GetField("canvasGroup", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(presenter) as CanvasGroup;
        Assert.That(canvasGroup, Is.Not.Null);
        Assert.That(canvasGroup.gameObject.activeSelf, Is.False);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static SceneDataEntry CreateScene(int sceneId, string prefabId)
    {
        return new SceneDataEntry
        {
            sceneId = sceneId,
            items = new List<SceneItemData>
            {
                new SceneItemData { prefabId = prefabId }
            }
        };
    }
}
