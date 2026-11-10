namespace Project.AI.Waves
{
public class AIWaveScheduler
{
    public enum Phase : byte
    {
        Inactive,
        Countdown,
        Running
    }

    public Phase CurrentPhase { get; private set; } = Phase.Inactive;
    public int WaveNumber { get; private set; }
    public float SecondsToNextWave { get; private set; }

    public void Start(float firstWaveDelay)
    {
        CurrentPhase = Phase.Countdown;
        WaveNumber = 0;
        SecondsToNextWave = firstWaveDelay;
    }

    public void Stop()
    {
        CurrentPhase = Phase.Inactive;
        WaveNumber = 0;
        SecondsToNextWave = 0f;
    }

    public int Tick(float deltaTime, int aliveCount, int maxAlive, float waveInterval)
    {
        if (CurrentPhase == Phase.Inactive)
            return 0;

        SecondsToNextWave -= deltaTime;
        if (SecondsToNextWave > 0f)
            return 0;

        if (CurrentPhase == Phase.Running && aliveCount >= maxAlive)
        {
            SecondsToNextWave = 0f;
            return 0;
        }

        CurrentPhase = Phase.Running;
        WaveNumber++;
        SecondsToNextWave = waveInterval;
        return WaveNumber;
    }
}
}
