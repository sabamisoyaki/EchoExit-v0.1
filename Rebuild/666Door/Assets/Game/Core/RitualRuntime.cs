using System;

namespace Door666.Core
{
    public struct RitualPerception
    {
        public float Distance;
        public float PlayerSpeed;
        public float LocalPlayerZ;
        public bool Gazing;
        public bool Hit;
        public bool LookingAtAudioCue;
        public bool AudioCue;
    }

    [Serializable]
    public sealed class RitualStep
    {
        public string condition;
        public float duration;
        public float timeWindow;
        public int resetStep;
        public bool resetOnMismatch;
        public float radius;
        public float maxDistance;
        public bool requireLookAway;
        public bool requireStill;
        public bool resetOnGuardFailure;
        public string gazeTarget;
    }

    /// <summary>Pure sequence evaluator. All elapsed time and perception enter through Tick.</summary>
    public sealed class RitualRuntime
    {
        public const float StillSpeed = 0.15f;
        private const double Epsilon = 0.000001;
        private readonly RitualStep[] steps;
        private readonly bool enabled;
        private double stepElapsed;
        private double held;
        private bool thresholdArmed;
        public int StepIndex { get; private set; }
        public bool IsRecognized { get; private set; }
        public double Elapsed { get; private set; }
        public double RecognizedAt { get; private set; } = -1;
        public double HoldTime => held;

        public RitualRuntime(AnomalyDefinition definition, bool isAnomaly = true)
            : this(definition == null ? Array.Empty<RitualStep>() : definition.ritualSteps, isAnomaly) { }

        public RitualRuntime(RitualStep[] sequence, bool isAnomaly = true)
        {
            steps = sequence ?? Array.Empty<RitualStep>();
            enabled = isAnomaly && steps.Length > 0;
            foreach (var step in steps) Validate(step, steps.Length);
        }

        public static void Validate(RitualStep step, int length)
        {
            if (step == null || step.duration < 0 || step.timeWindow < 0 || step.resetStep < 0 || step.resetStep >= length)
                throw new ArgumentException("Invalid ritual timing or reset index.");
            switch (step.condition)
            {
                case "Gaze": case "LookAway": case "Hit": case "EnterRadius": case "ExitRadius":
                case "RemainStill": case "CrossThreshold": case "AudioCue": break;
                default: throw new ArgumentException("Unknown ritual condition: " + step.condition);
            }
        }

        /// <returns>True once, on the frame the complete sequence succeeds.</returns>
        public bool Tick(RitualPerception perception, double deltaTime)
        {
            if (deltaTime < 0 || double.IsNaN(deltaTime) || double.IsInfinity(deltaTime)) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            Elapsed += deltaTime;
            if (!enabled || IsRecognized || deltaTime == 0) return false;
            var step = steps[StepIndex];
            if (perception.LocalPlayerZ >= 0 && (step.radius <= 0 || perception.Distance <= step.radius)) thresholdArmed = true;
            if (step.condition == "CrossThreshold" && step.radius > 0 && perception.Distance > step.radius) thresholdArmed = false;

            stepElapsed += deltaTime;
            if (step.timeWindow > 0 && stepElapsed > step.timeWindow + Epsilon)
            {
                ResetTo(step.resetStep, true);
                return false;
            }
            if (step.resetOnGuardFailure && !GuardsMatch(step, perception))
            {
                ResetTo(step.resetStep, true);
                return false;
            }
            if (!Matches(step, perception))
            {
                held = 0;
                if (step.resetOnMismatch) ResetTo(step.resetStep, step.resetStep != StepIndex);
                return false;
            }
            held += deltaTime;
            if (held + Epsilon < step.duration) return false;
            if (step.condition == "CrossThreshold") thresholdArmed = false;
            ++StepIndex;
            held = 0;
            stepElapsed = 0;
            if (StepIndex < steps.Length) return false;
            IsRecognized = true;
            RecognizedAt = Elapsed;
            return true;
        }

        private bool Matches(RitualStep step, RitualPerception p)
        {
            if (!GuardsMatch(step, p)) return false;
            switch (step.condition)
            {
                case "Gaze": return step.gazeTarget == "AudioCue" ? p.LookingAtAudioCue : p.Gazing;
                case "LookAway": return !p.Gazing;
                case "Hit": return p.Gazing && p.Hit;
                case "EnterRadius": return p.Distance <= step.radius;
                case "ExitRadius": return p.Distance >= step.radius;
                case "RemainStill": return p.PlayerSpeed <= StillSpeed;
                case "CrossThreshold": return thresholdArmed && p.LocalPlayerZ < -0.15f && p.Distance <= step.radius;
                case "AudioCue": return p.AudioCue;
                default: return false;
            }
        }

        private static bool GuardsMatch(RitualStep step, RitualPerception p)
        {
            return (step.maxDistance <= 0 || p.Distance <= step.maxDistance)
                && (!step.requireLookAway || !p.Gazing)
                && (!step.requireStill || p.PlayerSpeed <= StillSpeed);
        }

        private void ResetTo(int index, bool resetDeadline)
        {
            StepIndex = index;
            held = 0;
            if (resetDeadline) stepElapsed = 0;
            if (index == 0) thresholdArmed = false;
        }
    }
}
