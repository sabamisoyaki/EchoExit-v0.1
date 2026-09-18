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
            bool newlyRecognized = ritual.Tick(snapshot, deltaTime);
            if (newlyRecognized)
            {
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
                Emit(clue.sound, clue.subtitle, transform.position, clue.volume);
            }
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
                if (!NavMesh.SamplePosition(transform.position, out var sample, 3, NavMesh.AllAreas)) return;
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
            }
            if (!agent.isOnNavMesh) return;
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
