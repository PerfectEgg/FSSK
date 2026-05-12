using System;

public static class GameEvents
{
    public static bool IsGameOver { get; private set; }
    public static GameResultType LastResultType { get; private set; } = GameResultType.Draw;
    public static bool IsTimeoutGameOver => IsGameOver && LastResultType == GameResultType.Timeout;
    public static bool IsTimeoutEndingSequenceActive { get; private set; }
    public static bool AllowsTimeoutWaveEvents => IsTimeoutGameOver && IsTimeoutEndingSequenceActive;

    public static Action OnGameOverTriggered;
    public static Action<bool> OnGameOverResult;
    public static Action OnTimeoutEndingSequenceFinished;

    public static void ResetGameOverState()
    {
        IsGameOver = false;
        LastResultType = GameResultType.Draw;
        IsTimeoutEndingSequenceActive = false;
    }

    public static void TriggerGameOver()
    {
        TriggerGameOver(GameResultType.Draw);
    }

    public static void TriggerGameOver(
        GameResultType resultType,
        bool keepTimeoutEndingSequenceActive = false)
    {
        if (IsGameOver)
        {
            return;
        }

        IsGameOver = true;
        LastResultType = resultType;
        IsTimeoutEndingSequenceActive =
            resultType == GameResultType.Timeout && keepTimeoutEndingSequenceActive;
        OnGameOverTriggered?.Invoke();
    }

    public static void FinishTimeoutEndingSequence()
    {
        if (!IsTimeoutEndingSequenceActive)
        {
            return;
        }

        IsTimeoutEndingSequenceActive = false;
        OnTimeoutEndingSequenceFinished?.Invoke();
    }

    public static void TriggerGameOverResult(bool isWin)
    {
        OnGameOverResult?.Invoke(isWin);
    }
}
