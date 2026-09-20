using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>
    /// The game's log lines, each starting with "[666Door][category]". Filtering the Console with "[666Door]" (or searching
    /// Editor.log / Player.log for it) shows what the game did: each scene's start-up and self-check, rounds, door decisions,
    /// recognised anomalies, captures and stage saves. Categories: 起動, 画面, ラウンド, 部屋, 異変, 編集, 入力, 音, 初期化.
    /// Normal progress is logged without a stack trace so the log files keep one line per event; warnings and errors keep theirs.
    /// </summary>
    public static class GameLog
    {
        public const string Prefix = "[666Door]";

        /// <summary>Also logs frequent details: every ritual step, clues, button presses and editor actions.
        /// Set from <see cref="GameSettings.verboseLogging"/> when a screen scene starts.</summary>
        public static bool Verbose { get; set; }

        public static void Info(string category, string message)
            => Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", Format(category, message));

        public static void Detail(string category, string message)
        {
            if (Verbose) Info(category, message);
        }

        public static void Warning(string category, string message, Object context = null) => Debug.LogWarning(Format(category, message), context);
        public static void Error(string category, string message, Object context = null) => Debug.LogError(Format(category, message), context);

        public static string Format(string category, string message) => Prefix + "[" + category + "] " + message;
        public static string Position(Vector3 value) => "(" + value.x.ToString("F2") + ", " + value.y.ToString("F2") + ", " + value.z.ToString("F2") + ")";
    }
}
