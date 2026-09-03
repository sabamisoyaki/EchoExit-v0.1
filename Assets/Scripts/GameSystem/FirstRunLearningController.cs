using TMPro;
using UnityEngine;

/// <summary>Owns the one-time learning-round state independently from the rules tutorial.</summary>
public class FirstRunLearningController : MonoBehaviour
{
    private const string LearningCompletedKey = "EchoExit.LearningRoundCompleted";

    [SerializeField] private TMP_Text optionalStatusText;

    public bool IsLearningRound { get; private set; }

    public bool BeginIfNeeded()
    {
        IsLearningRound = PlayerPrefs.GetInt(LearningCompletedKey, 0) == 0;
        if (IsLearningRound && optionalStatusText != null)
            optionalStatusText.text = "まず、この場所を覚えてください";
        return IsLearningRound;
    }

    public void CompleteLearningRound()
    {
        if (!IsLearningRound) return;

        IsLearningRound = false;
        PlayerPrefs.SetInt(LearningCompletedKey, 1);
        PlayerPrefs.Save();
    }
}
