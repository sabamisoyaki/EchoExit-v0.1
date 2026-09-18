using System;

namespace Door666.Core
{
    public enum DoorChoice { Forward, Backward }
    public enum RunEndReason { None, Escaped, Caught, TimeExpired }

    public sealed class RoundDecision
    {
        public bool Accepted { get; internal set; }
        public bool IsCorrect { get; internal set; }
        public int Streak { get; internal set; }
        public RunEndReason EndReason { get; internal set; }
    }

    /// <summary>Owns one run. All time is injected, and no state survives through static fields.</summary>
    public sealed class RunSession
    {
        public const int DefaultRequiredStreak = 6;
        public const double DefaultRoundSeconds = 180;
        public int RequiredStreak { get; }
        public double RoundSeconds { get; }
        public int Streak { get; private set; }
        public int RoundsPlayed { get; private set; }
        public int CurrentSceneId { get; private set; }
        public int PlacedAnomalyCount { get; private set; }
        public bool HasAnomaly => PlacedAnomalyCount > 0;
        public double RemainingSeconds { get; private set; }
        public bool RunActive { get; private set; }
        public bool RoundActive { get; private set; }
        public RunEndReason EndReason { get; private set; }
        public string CapturedBy { get; private set; } = string.Empty;
        public string EndingId { get; private set; } = string.Empty;

        public RunSession(double seconds = DefaultRoundSeconds, int required = DefaultRequiredStreak)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (required < 1) throw new ArgumentOutOfRangeException(nameof(required));
            RoundSeconds = seconds;
            RequiredStreak = required;
            RemainingSeconds = seconds;
        }

        public void StartRun()
        {
            Streak = 0;
            RoundsPlayed = 0;
            CurrentSceneId = 0;
            PlacedAnomalyCount = 0;
            RemainingSeconds = RoundSeconds;
            RunActive = true;
            RoundActive = false;
            EndReason = RunEndReason.None;
            CapturedBy = string.Empty;
            EndingId = string.Empty;
        }

        public void BeginRound(int sceneId, int placedAnomalyCount)
        {
            if (!RunActive || EndReason != RunEndReason.None)
                throw new InvalidOperationException("新しいランを開始してから部屋を構築してください。");
            if (RoundActive) throw new InvalidOperationException("進行中のラウンドは置き換えられません。");
            if (sceneId < 1) throw new ArgumentOutOfRangeException(nameof(sceneId));
            if (placedAnomalyCount < 0) throw new ArgumentOutOfRangeException(nameof(placedAnomalyCount));
            CurrentSceneId = sceneId;
            PlacedAnomalyCount = placedAnomalyCount;
            RemainingSeconds = RoundSeconds;
            RoundActive = true;
            RoundsPlayed++;
        }

        public void Tick(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (!RoundActive) return;
            RemainingSeconds = Math.Max(0, RemainingSeconds - deltaSeconds);
            if (RemainingSeconds <= 0.000000001)
            {
                RemainingSeconds = 0;
                Finish(RunEndReason.TimeExpired);
            }
        }

        public RoundDecision Decide(DoorChoice choice)
        {
            if (choice != DoorChoice.Forward && choice != DoorChoice.Backward)
                throw new ArgumentOutOfRangeException(nameof(choice));
            if (!RoundActive) return new RoundDecision { Streak = Streak, EndReason = EndReason };

            // Lock the round synchronously so repeated input and threats cannot race the door.
            RoundActive = false;
            bool correct = HasAnomaly ? choice == DoorChoice.Backward : choice == DoorChoice.Forward;
            Streak = correct ? Streak + 1 : 0;
            if (Streak >= RequiredStreak) Finish(RunEndReason.Escaped);
            return new RoundDecision { Accepted = true, IsCorrect = correct, Streak = Streak, EndReason = EndReason };
        }

        public bool Catch(string anomalyName, string endingId = null)
        {
            if (!RoundActive) return false;
            CapturedBy = anomalyName ?? string.Empty;
            EndingId = endingId ?? string.Empty;
            Finish(RunEndReason.Caught);
            return true;
        }

        private void Finish(RunEndReason reason)
        {
            EndReason = reason;
            RoundActive = false;
            RunActive = false;
        }
    }
}
