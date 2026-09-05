using System;
using UnityEngine;

public enum AnomalyRitualKind
{
    Gaze,
    GazeThenLookAway,
    Hit,
    Approach,
    ApproachThenRetreat,
    CrossBehindThenGaze,
    EnterRadiusLookAwayStill,
    FootstepStopThenLookBack
}

public readonly struct AnomalyRitualProfile
{
    public AnomalyRitualProfile(
        string displayName,
        AnomalyRitualKind ritualKind,
        float primaryDuration,
        float secondaryDuration,
        float approachDistance,
        bool aggressive,
        float exitDistance = 4.5f,
        float timeWindow = 8f,
        float stationarySpeed = 0.12f)
    {
        DisplayName = displayName;
        RitualKind = ritualKind;
        PrimaryDuration = Mathf.Max(0.05f, primaryDuration);
        SecondaryDuration = Mathf.Max(0.05f, secondaryDuration);
        ApproachDistance = Mathf.Max(0.1f, approachDistance);
        ExitDistance = Mathf.Max(ApproachDistance + 0.1f, exitDistance);
        TimeWindow = Mathf.Max(0.5f, timeWindow);
        StationarySpeed = Mathf.Max(0.01f, stationarySpeed);
        Aggressive = aggressive;
    }

    public string DisplayName { get; }
    public AnomalyRitualKind RitualKind { get; }
    public float PrimaryDuration { get; }
    public float SecondaryDuration { get; }
    public float ApproachDistance { get; }
    public float ExitDistance { get; }
    public float TimeWindow { get; }
    public float StationarySpeed { get; }
    public bool Aggressive { get; }
}

/// <summary>
/// 異変を認識するための共通ランタイム。視線だけでなく、接近後の離脱、通過後の注視、
/// 静止、音源への振り返りを組み合わせた儀式を扱う。
/// </summary>
public sealed class AnomalyRitualController : MonoBehaviour
{
    public event Action<AnomalyRitualController> Primed;
    public event Action<AnomalyRitualController> Recognized;

    public string DisplayName => profile.DisplayName;
    public AnomalyRitualKind RitualKind => profile.RitualKind;
    public bool IsRecognized { get; private set; }
    public bool IsPrimed => primed;
    public bool IsAggressive => profile.Aggressive;

    private AnomalyRitualProfile profile;
    private GameManager gameManager;
    private Transform player;
    private Transform view;
    private AggressiveAnomaly aggressiveAnomaly;
    private float primaryProgress;
    private float secondaryProgress;
    private float stateDeadline;
    private float playerSpeed;
    private float previousLocalZ;
    private Vector3 previousPlayerPosition;
    private Vector3 cuePosition;
    private bool primaryCompleted;
    private bool observedThisFrame;
    private bool hasPreviousPlayerPosition;
    private bool hasPreviousLocalZ;
    private bool primed;
    private bool configured;

    public void Configure(
        AnomalyRitualProfile ritualProfile,
        GameManager owner,
        Transform playerTransform,
        bool threatsEnabled)
    {
        profile = ritualProfile;
        gameManager = owner;
        player = playerTransform;
        view = ResolveView(playerTransform);
        configured = true;

        ResetProgress(clearPrime: true);
        if (player != null)
        {
            previousPlayerPosition = player.position;
            previousLocalZ = transform.InverseTransformPoint(player.position).z;
            hasPreviousPlayerPosition = true;
            hasPreviousLocalZ = true;
        }

        if (profile.Aggressive && threatsEnabled)
        {
            aggressiveAnomaly = GetComponent<AggressiveAnomaly>();
            if (aggressiveAnomaly == null)
            {
                aggressiveAnomaly = gameObject.AddComponent<AggressiveAnomaly>();
            }

            aggressiveAnomaly.Configure(owner, playerTransform);
        }
    }

    public void Observe(float deltaTime)
    {
        if (!configured || IsRecognized) return;

        observedThisFrame = true;
        bool acceptsGaze = profile.RitualKind == AnomalyRitualKind.Gaze ||
                           profile.RitualKind == AnomalyRitualKind.GazeThenLookAway ||
                           (profile.RitualKind == AnomalyRitualKind.CrossBehindThenGaze && primed);
        if (!acceptsGaze) return;

        primaryProgress += Mathf.Max(0f, deltaTime);
        if (primaryProgress < profile.PrimaryDuration) return;

        primaryCompleted = true;
        if (profile.RitualKind == AnomalyRitualKind.Gaze ||
            profile.RitualKind == AnomalyRitualKind.CrossBehindThenGaze)
        {
            CompleteRitual();
        }
    }

    public void Hit()
    {
        if (!configured || IsRecognized || profile.RitualKind != AnomalyRitualKind.Hit) return;
        CompleteRitual();
    }

    /// <summary>音を起点にする異変が、聞こえた位置と制限時間を儀式へ渡す。</summary>
    public void NotifyAudioCue(Vector3 worldPosition)
    {
        if (!configured || IsRecognized || profile.RitualKind != AnomalyRitualKind.FootstepStopThenLookBack) return;

        cuePosition = worldPosition;
        Prime();
    }

    private void Update()
    {
        if (!configured || IsRecognized) return;
        ResolvePlayer();
        if (player == null) return;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        playerSpeed = hasPreviousPlayerPosition
            ? Vector3.Distance(player.position, previousPlayerPosition) / deltaTime
            : 0f;

        float distance = Vector3.Distance(player.position, transform.position);
        switch (profile.RitualKind)
        {
            case AnomalyRitualKind.Approach:
                if (distance <= profile.ApproachDistance) CompleteRitual();
                break;

            case AnomalyRitualKind.ApproachThenRetreat:
                if (!primed && distance <= profile.ApproachDistance)
                {
                    Prime();
                }
                else if (primed && distance >= profile.ExitDistance)
                {
                    CompleteRitual();
                }
                break;

            case AnomalyRitualKind.CrossBehindThenGaze:
                UpdateCrossBehind(distance);
                break;
        }

        if (primed && Time.time > stateDeadline)
        {
            ResetProgress(clearPrime: true);
        }

        previousPlayerPosition = player.position;
        hasPreviousPlayerPosition = true;
    }

    private void LateUpdate()
    {
        if (!configured || IsRecognized)
        {
            observedThisFrame = false;
            return;
        }

        switch (profile.RitualKind)
        {
            case AnomalyRitualKind.Gaze:
            case AnomalyRitualKind.CrossBehindThenGaze:
                if (!observedThisFrame) primaryProgress = 0f;
                break;

            case AnomalyRitualKind.GazeThenLookAway:
                UpdateGazeThenLookAway();
                break;

            case AnomalyRitualKind.EnterRadiusLookAwayStill:
                UpdateLookAwayStill();
                break;

            case AnomalyRitualKind.FootstepStopThenLookBack:
                UpdateFootstepRitual();
                break;
        }

        observedThisFrame = false;
    }

    private void UpdateCrossBehind(float distance)
    {
        float currentLocalZ = transform.InverseTransformPoint(player.position).z;
        if (!primed && hasPreviousLocalZ && previousLocalZ >= 0f && currentLocalZ < -0.15f &&
            distance <= profile.ApproachDistance)
        {
            Prime();
        }

        previousLocalZ = currentLocalZ;
        hasPreviousLocalZ = true;
    }

    private void UpdateGazeThenLookAway()
    {
        if (!primaryCompleted && !observedThisFrame)
        {
            primaryProgress = 0f;
        }
        else if (primaryCompleted && !observedThisFrame)
        {
            secondaryProgress += Time.deltaTime;
            if (secondaryProgress >= profile.SecondaryDuration) CompleteRitual();
        }
        else if (observedThisFrame)
        {
            secondaryProgress = 0f;
        }
    }

    private void UpdateLookAwayStill()
    {
        if (player == null) return;

        bool inside = Vector3.Distance(player.position, transform.position) <= profile.ApproachDistance;
        bool stationary = playerSpeed <= profile.StationarySpeed;
        if (inside && !observedThisFrame && stationary)
        {
            if (!primed) Prime();
            primaryProgress += Time.deltaTime;
            if (primaryProgress >= profile.PrimaryDuration) CompleteRitual();
        }
        else
        {
            primaryProgress = 0f;
            if (!inside) ResetProgress(clearPrime: true);
        }
    }

    private void UpdateFootstepRitual()
    {
        if (!primed || player == null) return;

        if (!primaryCompleted)
        {
            if (playerSpeed <= profile.StationarySpeed)
            {
                secondaryProgress += Time.deltaTime;
                primaryCompleted = secondaryProgress >= profile.SecondaryDuration;
            }
            else
            {
                secondaryProgress = 0f;
            }
        }

        if (!primaryCompleted) return;

        if (view == null) view = ResolveView(player);
        Vector3 origin = view != null ? view.position : player.position + Vector3.up * 0.6f;
        Vector3 direction = cuePosition - origin;
        if (direction.sqrMagnitude <= 0.01f) return;

        Vector3 forward = view != null ? view.forward : player.forward;
        if (Vector3.Dot(forward.normalized, direction.normalized) >= Mathf.Cos(35f * Mathf.Deg2Rad))
        {
            CompleteRitual();
        }
    }

    private void Prime()
    {
        primed = true;
        primaryProgress = 0f;
        secondaryProgress = 0f;
        primaryCompleted = false;
        stateDeadline = Time.time + profile.TimeWindow;
        Primed?.Invoke(this);
    }

    private void ResetProgress(bool clearPrime)
    {
        primaryProgress = 0f;
        secondaryProgress = 0f;
        primaryCompleted = false;
        if (clearPrime) primed = false;
    }

    private void ResolvePlayer()
    {
        if (player != null) return;
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;
        view = ResolveView(player);
    }

    private static Transform ResolveView(Transform playerTransform)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null) return mainCamera.transform;
        Camera childCamera = playerTransform != null ? playerTransform.GetComponentInChildren<Camera>(true) : null;
        return childCamera != null ? childCamera.transform : playerTransform;
    }

    private void CompleteRitual()
    {
        if (IsRecognized) return;

        IsRecognized = true;
        Recognized?.Invoke(this);
        gameManager?.NotifyAnomalyRecognized(this);

        if (aggressiveAnomaly != null)
        {
            aggressiveAnomaly.Activate();
        }
    }
}
