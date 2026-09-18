using TMPro;
using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>What is shown while walking, in the game and in the editor (HUD.prefab): streak, time, crosshair,
    /// the line under the crosshair, banners and subtitles.</summary>
    public sealed class PlayHud : UIScreen
    {
        [SerializeField] private TMP_Text streak;
        [SerializeField] private TMP_Text timer;
        [SerializeField] private TMP_Text prompt;
        [SerializeField] private TMP_Text banner;
        [SerializeField] private TMP_Text subtitle;

        [Header("連続正解")]
        [Tooltip("{0} 連続正解数、{1} 必要な数、{2} 印の列。")]
        [SerializeField, TextArea(2, 3)] private string streakFormat = "連続正解  {0} / {1}\n{2}";
        [SerializeField] private string reachedMark = "●";
        [SerializeField] private string remainingMark = "○";

        [Header("残り時間")]
        [Tooltip("残りがこの秒数以下になったら色を変える。")]
        [SerializeField, Min(0)] private int warningSeconds = 30;
        [SerializeField] private Color warningColor = new Color(.92f, .49f, .36f);

        [Header("扉の前")]
        [Tooltip("{0} 操作キー、{1} 扉ごとの文言。")]
        [SerializeField] private string doorPromptFormat = "[{0}]  {1}";
        [SerializeField] private string forwardDoor = "前へ進む";
        [SerializeField] private string backwardDoor = "引き返す";
        [SerializeField] private string finalDoor = "666号扉を開ける";

        [Header("バナー")]
        [SerializeField, Min(0)] private float bannerSeconds = 2.3f;
        [Tooltip("{0} 連続正解数、{1} 必要な数。")]
        [SerializeField, TextArea(1, 3)] private string correctDoor = "扉の先へ。  {0} / {1}";
        [Tooltip("{1} 必要な数。")]
        [SerializeField, TextArea(1, 3)] private string wrongDoor = "ここへ、戻された。\n連続正解  0 / {1}";
        [SerializeField] private string recognized = "異変を認識した。";
        [SerializeField] private string recognizedThreat = "異変を認識した。  出口へ逃げろ。";
        [Tooltip("編集画面で、追ってくる異変を認識したとき。")]
        [SerializeField] private string recognizedThreatWhileEditing = "異変を認識した。  追ってくる。";
        [Tooltip("編集画面で捕まったとき。{0} は異変の名前。")]
        [SerializeField] private string caughtWhileEditing = "「{0}」に捕まった。元の位置に戻した。";

        [Header("音の字幕")]
        [Tooltip("{0} は異変定義（AnomalyDefinitions.json）の subtitle。")]
        [SerializeField] private string subtitleFormat = "{0}";
        [SerializeField, Min(0)] private float subtitleSeconds = 2.4f;

        private Color timerColor;
        private bool colorKnown;
        private float bannerUntil;
        private float subtitleUntil;

        /// <summary>Removes the sample text the prefab shows for layout.</summary>
        public void Clear()
        {
            SetText(streak, "");
            SetText(timer, "");
            SetText(prompt, "");
            SetText(banner, "");
            SetText(subtitle, "");
        }

        public void SetRun(int count, int required, double seconds)
        {
            SetText(streak, Format(streakFormat, count, required, Repeat(reachedMark, count) + Repeat(remainingMark, required - count)));
            if (timer == null) return;
            if (!colorKnown) { timerColor = timer.color; colorKnown = true; }
            int time = Mathf.CeilToInt((float)seconds);
            timer.text = (time / 60).ToString("00") + ":" + (time % 60).ToString("00");
            timer.color = time <= warningSeconds ? warningColor : timerColor;
        }

        /// <summary>Editing has no streak or time limit.</summary>
        public void HideRun()
        {
            SetText(streak, "");
            SetText(timer, "");
        }

        public void Prompt(string text) => SetText(prompt, text);

        public void DoorPrompt(DoorTarget door, string key)
        {
            if (door == null) { Prompt(""); return; }
            Prompt(Format(doorPromptFormat, key, door.IsFinalExit ? finalDoor : door.IsForward ? forwardDoor : backwardDoor));
        }

        public void DoorResult(bool correct, int count, int required, float seconds) =>
            Banner(Format(correct ? correctDoor : wrongDoor, count, required), seconds);

        public void Recognized(bool threat, bool editing) =>
            Banner(!threat ? recognized : editing ? recognizedThreatWhileEditing : recognizedThreat, bannerSeconds);

        public void CaughtWhileEditing(string anomalyName) => Banner(Format(caughtWhileEditing, anomalyName), bannerSeconds);

        public void Subtitle(string text)
        {
            SetText(subtitle, Format(subtitleFormat, text));
            subtitleUntil = Time.unscaledTime + subtitleSeconds;
        }

        private void Banner(string text, float seconds)
        {
            SetText(banner, text);
            bannerUntil = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            if (banner != null && banner.text.Length > 0 && Time.unscaledTime > bannerUntil) banner.text = "";
            if (subtitle != null && subtitle.text.Length > 0 && Time.unscaledTime > subtitleUntil) subtitle.text = "";
        }

        private static string Repeat(string mark, int count)
        {
            if (string.IsNullOrEmpty(mark) || count <= 0) return "";
            var text = new System.Text.StringBuilder(mark.Length * count);
            for (int i = 0; i < count; i++) text.Append(mark);
            return text.ToString();
        }
    }
}
