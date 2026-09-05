public enum GameEndingKind
{
    None,
    Escaped,
    Caught,
    TimeExpired
}

public static class GameSessionState
{
    public static GameEndingKind Ending { get; private set; }
    public static string Detail { get; private set; }

    public static void Reset()
    {
        Ending = GameEndingKind.None;
        Detail = string.Empty;
    }

    public static void SetEnding(GameEndingKind ending, string detail)
    {
        Ending = ending;
        Detail = detail ?? string.Empty;
    }
}
