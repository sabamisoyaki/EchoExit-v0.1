using System;
using UnityEngine;

/// <summary>JSON の prefabId を、異変ごとの儀式と演出へ結び付ける。</summary>
public static class AnomalyRuntimeFactory
{
    public static AnomalyRitualProfile GetProfile(string prefabId)
    {
        switch ((prefabId ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "changecolorbox":
                return new AnomalyRitualProfile(
                    "変色する箱",
                    AnomalyRitualKind.GazeThenLookAway,
                    primaryDuration: 1.5f,
                    secondaryDuration: 1f,
                    approachDistance: 3f,
                    aggressive: false);

            case "anomaryshirinkbox":
                return new AnomalyRitualProfile(
                    "縮む箱",
                    AnomalyRitualKind.Approach,
                    primaryDuration: 0.1f,
                    secondaryDuration: 0.1f,
                    approachDistance: 2.5f,
                    aggressive: false);

            case "dollprefab":
                return new AnomalyRitualProfile(
                    "戻ってくる人形",
                    AnomalyRitualKind.ApproachThenRetreat,
                    primaryDuration: 0.1f,
                    secondaryDuration: 0.1f,
                    approachDistance: 2.5f,
                    aggressive: false,
                    exitDistance: 5f,
                    timeWindow: 9f);

            case "bears":
                return new AnomalyRitualProfile(
                    "追ってくる人形",
                    AnomalyRitualKind.Hit,
                    primaryDuration: 0.1f,
                    secondaryDuration: 0.1f,
                    approachDistance: 2.5f,
                    aggressive: true);

            case "chairprefab":
                return new AnomalyRitualProfile(
                    "席を数える椅子",
                    AnomalyRitualKind.CrossBehindThenGaze,
                    primaryDuration: 0.65f,
                    secondaryDuration: 0.1f,
                    approachDistance: 3.2f,
                    aggressive: false,
                    timeWindow: 6f);

            case "wall":
                return new AnomalyRitualProfile(
                    "呼吸する壁",
                    AnomalyRitualKind.EnterRadiusLookAwayStill,
                    primaryDuration: 2.2f,
                    secondaryDuration: 0.1f,
                    approachDistance: 2.4f,
                    aggressive: false,
                    timeWindow: 8f,
                    stationarySpeed: 0.18f);

            case "footstepecho":
                return new AnomalyRitualProfile(
                    "一歩多い足音",
                    AnomalyRitualKind.FootstepStopThenLookBack,
                    primaryDuration: 0.1f,
                    secondaryDuration: 0.8f,
                    approachDistance: 5f,
                    aggressive: false,
                    timeWindow: 6f,
                    stationarySpeed: 0.18f);

            default:
                return new AnomalyRitualProfile(
                    string.IsNullOrWhiteSpace(prefabId) ? "名称不明の異変" : prefabId,
                    AnomalyRitualKind.Gaze,
                    primaryDuration: 2f,
                    secondaryDuration: 0.1f,
                    approachDistance: 2.5f,
                    aggressive: false);
        }
    }

    public static AnomalyRitualController Configure(
        GameObject instance,
        string prefabId,
        bool isAnomaly,
        GameManager gameManager,
        Transform player,
        bool threatsEnabled)
    {
        if (instance == null) return null;
        if (!isAnomaly) return null;

        if (instance.GetComponent<AbnormalityInstanceMarker>() == null)
        {
            instance.AddComponent<AbnormalityInstanceMarker>();
        }

        var ritual = instance.GetComponent<AnomalyRitualController>();
        if (ritual == null)
        {
            ritual = instance.AddComponent<AnomalyRitualController>();
        }

        ritual.Configure(GetProfile(prefabId), gameManager, player, threatsEnabled);
        ConfigureBuiltInPhenomenon(instance, prefabId, ritual, player, gameManager != null);

        // ?? は Unity の == オーバーロードを迂回し擬似 null を通してしまうため使わない。
        if (!instance.TryGetComponent(out AnomalyRecognitionAudio recognitionAudio))
        {
            recognitionAudio = instance.AddComponent<AnomalyRecognitionAudio>();
        }
        recognitionAudio.Configure(ritual);
        return ritual;
    }


    private static void ConfigureBuiltInPhenomenon(
        GameObject instance,
        string prefabId,
        AnomalyRitualController ritual,
        Transform player,
        bool isPlayRound)
    {
        if (string.Equals(prefabId, "anomaryShirinkBox", StringComparison.OrdinalIgnoreCase))
        {
            if (!instance.TryGetComponent(out SmoothDistanceShrink phenomenon))
            {
                phenomenon = instance.AddComponent<SmoothDistanceShrink>();
            }
            phenomenon.Configure(player);
        }
        else if (string.Equals(prefabId, "changeColorBox", StringComparison.OrdinalIgnoreCase))
        {
            if (!instance.TryGetComponent(out LookAwayColorPhenomenon phenomenon))
            {
                phenomenon = instance.AddComponent<LookAwayColorPhenomenon>();
            }
            phenomenon.Configure(ritual);
        }
        else if (string.Equals(prefabId, "DollPrefab", StringComparison.OrdinalIgnoreCase))
        {
            if (!instance.TryGetComponent(out ReturningDollPhenomenon phenomenon))
            {
                phenomenon = instance.AddComponent<ReturningDollPhenomenon>();
            }
            phenomenon.Configure(ritual);
        }
        else if (string.Equals(prefabId, "ChairPrefab", StringComparison.OrdinalIgnoreCase))
        {
            if (!instance.TryGetComponent(out CountingChairPhenomenon phenomenon))
            {
                phenomenon = instance.AddComponent<CountingChairPhenomenon>();
            }
            phenomenon.Configure(ritual);
        }
        else if (string.Equals(prefabId, "wall", StringComparison.OrdinalIgnoreCase))
        {
            if (!instance.TryGetComponent(out BreathingWallPhenomenon phenomenon))
            {
                phenomenon = instance.AddComponent<BreathingWallPhenomenon>();
            }
            phenomenon.Configure(ritual, player);
        }
        else if (string.Equals(prefabId, "footstepEcho", StringComparison.OrdinalIgnoreCase))
        {
            if (!instance.TryGetComponent(out ExtraFootstepAnomaly phenomenon))
            {
                phenomenon = instance.AddComponent<ExtraFootstepAnomaly>();
            }
            phenomenon.Configure(ritual, player, hideMarker: isPlayRound);
        }
    }
}
