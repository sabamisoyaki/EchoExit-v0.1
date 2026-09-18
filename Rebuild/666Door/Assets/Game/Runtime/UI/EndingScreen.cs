using Door666.Core;
using TMPro;
using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>The end of a run (Ending.prefab). The wording of each ending is edited here.</summary>
    public sealed class EndingScreen : UIScreen
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;

        [Header("脱出（連続正解を達成）")]
        [SerializeField] private string escapedTitle = "扉の向こうへ";
        [SerializeField, TextArea(2, 4)] private string escapedBody = "六度の判断が、あなたを外へ連れ出した。\nそれでも、背後の扉を見てはいけない。";

        [Header("捕獲")]
        [SerializeField] private string caughtTitle = "もう、戻れない";
        [Tooltip("{0} は捕まえた異変の名前。")]
        [SerializeField, TextArea(2, 4)] private string caughtBody = "「{0}」に捕まった。\nこの部屋には、あなたの気配が残る。";

        [Header("時間切れ")]
        [SerializeField] private string timeUpTitle = "時間が、尽きた";
        [SerializeField, TextArea(2, 4)] private string timeUpBody = "扉の音は、もう聞こえない。\nこの部屋での探索は終わった。";

        public void Present(RunEndReason reason, string capturedBy)
        {
            if (reason == RunEndReason.Escaped) Present(escapedTitle, escapedBody);
            else if (reason == RunEndReason.Caught) Present(caughtTitle, Format(caughtBody, capturedBy));
            else Present(timeUpTitle, timeUpBody);
        }

        private void Present(string heading, string text)
        {
            SetText(title, heading);
            SetText(body, text);
        }
    }
}
