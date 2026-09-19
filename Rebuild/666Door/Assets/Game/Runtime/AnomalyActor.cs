using System;
using System.Collections.Generic;
using Door666.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Door666.Runtime
{
    public sealed class AnomalyActor : MonoBehaviour
    {
        public AnomalyDefinition Definition { get; private set; }
        public bool IsAnomaly { get; private set; }
        public bool IsRecognized => ritual != null && ritual.IsRecognized;
        public bool IsThreat => IsAnomaly && Definition != null && Definition.IsThreat;
        public bool RecognitionEffectActive => effects != null && effects.IsActive;
        public Vector3 AudioCuePosition { get; private set; }
        public Transform VisualRoot { get; private set; }
        public Transform Player { get; private set; }
        public Camera PlayerCamera { get; private set; }
        public event Action<AnomalyActor> Recognized;
        public event Action<AnomalyActor> Caught;
        public event Action<string> Subtitle;
        private RitualRuntime ritual;
        private ThreatRuntime threat;
        private AnomalyEffects effects;
        private NavMeshAgent agent;
        private bool suspended;
        private float age;
        private float clueClock;
        private float footstepTravel;
        private float pendingFootstep = -1;
        private float repathClock;
        private float chaseStepClock;
        private Vector3 previousPlayerPosition;
        private readonly List<Vector3> footstepTrail = new List<Vector3>();
        private readonly Dictionary<int, float> stepLogTimes = new Dictionary<int, float>();
        private bool pursuitWarned;

        /// <summary>"ANM-001 目を離した箱": how this actor appears in the log.</summary>
        public string LogName => Definition == null ? name : Definition.anomalyId + " " + Definition.displayName;

        public void Initialize(AnomalyDefinition definition, bool isAnomaly, Transform visualRoot, Transform player, Camera camera, float playerMoveSpeed)
        {
            Definition = definition;
            IsAnomaly = isAnomaly && definition != null;
            VisualRoot = visualRoot != null ? visualRoot : transform;
            Player = player;
            PlayerCamera = camera;
            ritual = new RitualRuntime(definition, IsAnomaly);
            if (!IsAnomaly) return;
            threat = new ThreatRuntime(definition.threatProfile, playerMoveSpeed);
            previousPlayerPosition = player != null ? player.position : transform.position;
            footstepTrail.Clear();
            footstepTrail.Add(previousPlayerPosition);
            AudioCuePosition = previousPlayerPosition;
            effects = new AnomalyEffects(this);
            effects.ApplyInitialClue();
        }

        public RitualPerception GetPerception(Vector3 playerPosition, float speed, bool gazing, bool hit)
        {
            var cueDirection = AudioCuePosition + Vector3.up * 1.2f - (PlayerCamera != null ? PlayerCamera.transform.position : playerPosition);
            return new RitualPerception
            {
                Distance = Vector3.Distance(playerPosition, transform.position),
                PlayerSpeed = speed,
                LocalPlayerZ = transform.InverseTransformPoint(playerPosition).z,
                Gazing = gazing,
                Hit = gazing && hit,
                LookingAtAudioCue = PlayerCamera != null && Vector3.Angle(PlayerCamera.transform.forward, cueDirection) <= 35
            };
        }

        public void Suspend(bool value)
        {
            suspended = value;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = value || threat == null || !threat.CanChase;
        }

        public void Tick(RitualPerception snapshot, float deltaTime)
        {
            if (!IsAnomaly || suspended || deltaTime <= 0 || Player == null || effects == null) return;
            age += deltaTime;
            if (!IsRecognized)
            {
                TickClue(ref snapshot, deltaTime);
                effects.TickClue(age);
            }
            int step = ritual.StepIndex;
            bool newlyRecognized = ritual.Tick(snapshot, deltaTime);
            if (!newlyRecognized && ritual.StepIndex != step && GameLog.Verbose) LogStep(step, ritual.StepIndex);
            if (newlyRecognized)
            {
                GameLog.Info("異変", LogName + ": 儀式が成立し、認識しました（出現から " + age.ToString("F1") + " 秒）" + (IsThreat ? "。追跡型です" : ""));
                effects.Begin();
                threat.Recognize();
                Recognized?.Invoke(this);
            }
            effects.Tick(snapshot, newlyRecognized ? 0 : deltaTime);
            if (IsThreat && IsRecognized)
            {
                Vector3 difference = Player.position - transform.position;
                difference.y = 0;
                float distance = difference.magnitude;
                // Physics prevents capture through a thin wall while navigation rounds it.
                if (distance <= Definition.threatProfile.captureDistance && !HasCaptureSight()) distance = float.PositiveInfinity;
                if (threat.Tick(newlyRecognized ? 0 : deltaTime, distance, effects.IsActive))
                {
                    if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                    GameLog.Info("異変", LogName + ": プレイヤーを捕まえました（認識から " + threat.TimeSinceRecognition.ToString("F1") + " 秒）");
                    Caught?.Invoke(this);
                }
                else TickPursuit(deltaTime);
            }
            previousPlayerPosition = Player.position;
        }

        private void TickClue(ref RitualPerception perception, float deltaTime)
        {
            var clue = Definition.clue;
            if (clue == null) return;
            if (clue.kind == "ExtraFootstep")
            {
                if ((Player.position - footstepTrail[footstepTrail.Count - 1]).sqrMagnitude >= 0.0025f)
                {
                    footstepTrail.Add(Player.position);
                    if (footstepTrail.Count > 128) footstepTrail.RemoveAt(0);
                }
                if (pendingFootstep >= 0)
                {
                    pendingFootstep -= deltaTime;
                    if (pendingFootstep <= 0)
                    {
                        pendingFootstep = -1;
                        perception.AudioCue = true;
                        GameLog.Detail("異変", LogName + ": 手がかり「" + clue.sound + "」を " + GameLog.Position(AudioCuePosition) + " で鳴らしました");
                        Emit(clue.sound, clue.subtitle, AudioCuePosition, clue.volume);
                    }
                }
                if (ritual.StepIndex == 0 && pendingFootstep < 0 && perception.Distance <= clue.distance && perception.PlayerSpeed > RitualRuntime.StillSpeed)
                {
                    footstepTravel += perception.PlayerSpeed * deltaTime;
                    if (footstepTravel >= 6.4f)
                    {
                        footstepTravel = 0;
                        pendingFootstep = 0.2f;
                        AudioCuePosition = FloorPoint(PreviousFootfall(1.8f));
                    }
                }
                return;
            }
            clueClock += deltaTime;
            if (!string.IsNullOrEmpty(clue.sound) && perception.Distance <= clue.distance && clueClock >= clue.interval)
            {
                clueClock = 0;
                GameLog.Detail("異変", LogName + ": 手がかり「" + clue.sound + "」を鳴らしました（距離 " + perception.Distance.ToString("F1") + "m）");
                Emit(clue.sound, clue.subtitle, transform.position, clue.volume);
            }
        }

        /// <summary>"Gaze 1.5 秒 → LookAway 1 秒": the ritual as the log describes it.</summary>
        public string DescribeSteps() => Definition == null ? "" : string.Join(" → ", Array.ConvertAll(Definition.ritualSteps, DescribeStep));

        private static string DescribeStep(RitualStep step)
        {
            return step.condition + (step.gazeTarget == "AudioCue" ? "（音の方向）" : "") + (step.radius > 0 ? " 半径 " + step.radius + "m" : "")
                + (step.duration > 0 ? " " + step.duration + " 秒" : "") + (step.timeWindow > 0 ? "（" + step.timeWindow + " 秒以内）" : "");
        }

        // Some rituals fall back and advance again every frame while the player walks (the breathing wall), so the same
        // step change is logged at most once every two seconds.
        private void LogStep(int from, int to)
        {
            int key = from * 64 + to;
            if (stepLogTimes.TryGetValue(key, out float last) && age - last < 2) return;
            stepLogTimes[key] = age;
            var steps = Definition.ritualSteps;
            GameLog.Info("異変", LogName + ": " + (to > from
                ? "段階 " + to + "/" + steps.Length + "（" + DescribeStep(steps[from]) + "）成立 → 次は " + DescribeStep(steps[to])
                : "段階 " + (from + 1) + "/" + steps.Length + "（" + DescribeStep(steps[from]) + "）が途切れ、段階 " + (to + 1) + "（" + DescribeStep(steps[to]) + "）からやり直し"));
        }

        private Vector3 PreviousFootfall(float behindDistance)
        {
            for (int i = footstepTrail.Count - 1; i > 0; --i)
            {
                float length = Vector3.Distance(footstepTrail[i], footstepTrail[i - 1]);
                if (length >= behindDistance) return Vector3.Lerp(footstepTrail[i], footstepTrail[i - 1], behindDistance / length);
                behindDistance -= length;
            }
            return footstepTrail[0];
        }

        private bool HasCaptureSight()
        {
            Vector3 origin = transform.position + Vector3.up * 0.85f;
            Vector3 destination = Player.position + Vector3.up * 0.85f;
            return !Physics.Linecast(origin, destination, out var obstruction, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                || obstruction.transform == Player || obstruction.transform.IsChildOf(Player);
        }

        private void TickPursuit(float deltaTime)
        {
            if (!threat.CanChase) return;
            if (agent == null)
            {
                if (!NavMesh.SamplePosition(transform.position, out var sample, 3, NavMesh.AllAreas))
                {
                    WarnPursuit("近くにナビメッシュがないため追跡できません");
                    return;
                }
                var visualPosition = VisualRoot.position;
                transform.position = sample.position;
                if (VisualRoot != transform) VisualRoot.position = visualPosition;
                foreach (var collider in GetComponents<Collider>()) collider.isTrigger = true;
                agent = gameObject.AddComponent<NavMeshAgent>();
                agent.radius = 0.35f;
                agent.height = 1.7f;
                agent.speed = threat.Speed;
                agent.acceleration = 8;
                agent.angularSpeed = 190;
                agent.stoppingDistance = 0.5f;
                agent.autoBraking = false;
                agent.autoRepath = true;
                agent.Warp(sample.position);
                GameLog.Info("異変", LogName + ": 追跡を開始（速度 " + threat.Speed.ToString("F1") + " m/s、" + GameLog.Position(sample.position) + " から）");
            }
            if (!agent.isOnNavMesh)
            {
                WarnPursuit("ナビメッシュの外に出たため追跡できません");
                return;
            }
            agent.isStopped = false;
            repathClock -= deltaTime;
            if (repathClock <= 0)
            {
                repathClock = 0.15f;
                if (NavMesh.SamplePosition(Player.position, out var destination, 2, NavMesh.AllAreas)) agent.SetDestination(destination.position);
            }
            chaseStepClock += deltaTime;
            if (agent.velocity.sqrMagnitude > 0.1f && chaseStepClock >= 0.42f)
            {
                chaseStepClock = 0;
                SpatialAudio.Emit(transform, transform.position, "footstep", 0.75f);
            }
        }

        // Called every frame while it lasts, so it is reported once per actor.
        private void WarnPursuit(string problem)
        {
            if (pursuitWarned) return;
            pursuitWarned = true;
            GameLog.Warning("異変", LogName + ": " + problem + "（位置 " + GameLog.Position(transform.position) + "）", this);
        }

        internal void Emit(string sound, string subtitle, Vector3 position, float volume = 0.7f)
        {
            SpatialAudio.Emit(transform, position, sound, volume);
            if (!string.IsNullOrEmpty(subtitle)) Subtitle?.Invoke(subtitle);
        }

        internal Vector3 BehindPlayer()
        {
            var forward = PlayerCamera != null ? PlayerCamera.transform.forward : Player.forward;
            forward.y = 0;
            return Player.position - forward.normalized * 1.5f + Vector3.up;
        }

        internal static Vector3 FloorPoint(Vector3 point)
        {
            var hits = Physics.RaycastAll(point + Vector3.up * 2, Vector3.down, 8, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity;
            var floor = new Vector3(point.x, 0.012f, point.z);
            foreach (var hit in hits)
            {
                if (hit.distance >= closest || hit.normal.y < 0.7f || hit.collider.GetComponentInParent<StageObject>() != null
                    || hit.collider.GetComponentInParent<CharacterController>() != null) continue;
                closest = hit.distance;
                floor = hit.point + Vector3.up * 0.012f;
            }
            if (closest < float.PositiveInfinity) return floor;
            return new Vector3(point.x, 0.012f, point.z);
        }

        private void OnDestroy() { if (effects != null) effects.Dispose(); }
    }
}
