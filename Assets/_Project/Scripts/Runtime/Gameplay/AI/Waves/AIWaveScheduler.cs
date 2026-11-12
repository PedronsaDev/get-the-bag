namespace Project.AI.Waves
{
public class AIWaveScheduler
{
    public enum Phase : byte
    {
        Inactive,
        Countdown,
        Assault,
        Break,
        FinalPush
    }

    public enum Change : byte
    {
        None,
        AssaultStarted,
        BreakStarted,
        FinalPushStarted
    }

    private float _assaultSeconds;
    private float _breakSeconds;

    public Phase CurrentPhase { get; private set; } = Phase.Inactive;
    public int AssaultNumber { get; private set; }

    /// <summary>Seconds left in the current timed phase. Zero in the final push, which only ends with the raid.</summary>
    public float SecondsInPhase { get; private set; }

    public void Start(float countdownSeconds, float assaultSeconds, float breakSeconds)
    {
        _assaultSeconds = assaultSeconds;
        _breakSeconds = breakSeconds;
        CurrentPhase = Phase.Countdown;
        AssaultNumber = 0;
        SecondsInPhase = countdownSeconds;
    }

    public void Stop()
    {
        CurrentPhase = Phase.Inactive;
        AssaultNumber = 0;
        SecondsInPhase = 0f;
    }

    /// <summary>
    /// One last, larger assault with no break after it. From a break or the countdown it takes the number of
    /// the assault that was due next; from an assault it replaces the running one.
    /// </summary>
    public Change BeginFinalPush()
    {
        if (CurrentPhase is Phase.Inactive or Phase.FinalPush)
            return Change.None;

        if (CurrentPhase != Phase.Assault)
            AssaultNumber++;

        CurrentPhase = Phase.FinalPush;
        SecondsInPhase = 0f;
        return Change.FinalPushStarted;
    }

    public Change Tick(float deltaTime)
    {
        if (CurrentPhase is Phase.Inactive or Phase.FinalPush)
            return Change.None;

        SecondsInPhase -= deltaTime;
        if (SecondsInPhase > 0f)
            return Change.None;

        return CurrentPhase == Phase.Assault ? StartBreak() : StartAssault();
    }

    private Change StartAssault()
    {
        AssaultNumber++;
        CurrentPhase = Phase.Assault;
        SecondsInPhase = _assaultSeconds;
        return Change.AssaultStarted;
    }

    private Change StartBreak()
    {
        CurrentPhase = Phase.Break;
        SecondsInPhase = _breakSeconds;
        return Change.BreakStarted;
    }
}
}
