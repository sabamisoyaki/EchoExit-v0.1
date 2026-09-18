using System;

namespace Door666.Core
{
    [Serializable]
    public sealed class ThreatProfile
    {
        public bool enabled;
        public float speed = 2.8f;
        public float captureDistance = 1.1f;
        public float graceSeconds = 1.2f;
        public float maxPlayerSpeedRatio = 0.75f;
    }

    public sealed class ThreatRuntime
    {
        private readonly ThreatProfile profile;
        private double elapsed;
        public bool IsRecognized { get; private set; }
        public bool IsCaught { get; private set; }
        public bool CanChase { get; private set; }
        public float Speed { get; }
        public double TimeSinceRecognition => elapsed;

        public ThreatRuntime(ThreatProfile profile, float playerMoveSpeed)
        {
            this.profile = profile ?? new ThreatProfile();
            Speed = Math.Max(0, Math.Min(this.profile.speed, playerMoveSpeed * Math.Min(0.75f, Math.Max(0, this.profile.maxPlayerSpeedRatio))));
        }

        public void Recognize()
        {
            if (IsRecognized || !profile.enabled) return;
            IsRecognized = true;
            elapsed = 0;
        }

        public bool Tick(double deltaTime, float distance, bool recognitionEffectActive, bool exitInteractionInProgress = false)
        {
            if (deltaTime < 0 || double.IsNaN(deltaTime) || double.IsInfinity(deltaTime)) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!profile.enabled || !IsRecognized || IsCaught) return false;
            elapsed += deltaTime;
            CanChase = elapsed + 0.000001 >= Math.Max(1.2, profile.graceSeconds) && !recognitionEffectActive && !exitInteractionInProgress;
            if (!CanChase || distance > profile.captureDistance) return false;
            IsCaught = true;
            CanChase = false;
            return true;
        }
    }
}
