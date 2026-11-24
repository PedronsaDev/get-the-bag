using System.Collections.Generic;
using Project.Core.Raid;
using Project.Logging;
using UnityEngine;

namespace Project.AI.Waves
{
/// <summary>
/// The server's assault logic without networking or spawning: it follows the raid and the alarm, runs the
/// schedule and decides which enemies each assault will send. The director does the actual spawning.
/// </summary>
public sealed class AssaultCoordinator
{
    private readonly AIWaveConfig _config;
    private readonly System.Random _random;
    private readonly List<int> _plan = new(16);

    public AssaultCoordinator(AIWaveConfig config, System.Random random)
    {
        _config = config;
        _random = random;
    }

    public AIWaveScheduler Scheduler { get; } = new();

    /// <summary>Enemies the current assault still has to send.</summary>
    public Queue<GameObject> SpawnQueue { get; } = new(16);

    /// <summary>True once the raid has ended. Everything is frozen so the results can read it.</summary>
    public bool HasEnded { get; private set; }

    public ThreatPhase CurrentThreatPhase
    {
        get
        {
            if (HasEnded)
                return ThreatPhase.Ended;

            switch (Scheduler.CurrentPhase)
            {
                case AIWaveScheduler.Phase.Countdown: return ThreatPhase.Alarmed;
                case AIWaveScheduler.Phase.Assault: return ThreatPhase.Assault;
                case AIWaveScheduler.Phase.Break: return ThreatPhase.Break;
                case AIWaveScheduler.Phase.FinalPush: return ThreatPhase.FinalPush;
                default: return ThreatPhase.Stealth;
            }
        }
    }

    public void Advance(float deltaTime, RaidPhase raid, bool alarmed, int players)
    {
        if (HasEnded)
        {
            if (raid == RaidPhase.Ended)
                return;
            Reset();
        }

        if (raid == RaidPhase.Ended)
        {
            End();
            return;
        }

        if (raid == RaidPhase.Loading)
        {
            Reset();
            return;
        }

        FollowAlarm(alarmed);
        if (Scheduler.CurrentPhase == AIWaveScheduler.Phase.Inactive)
            return;

        // Calling this every frame is safe: it only acts once, and not at all when the alarm never rang.
        if (raid == RaidPhase.Extraction)
            Handle(Scheduler.BeginFinalPush(), players);

        Handle(Scheduler.Tick(deltaTime), players);
    }

    private void FollowAlarm(bool alarmed)
    {
        if (alarmed && Scheduler.CurrentPhase == AIWaveScheduler.Phase.Inactive)
        {
            Scheduler.Start(_config.FirstWaveDelay, _config.AssaultSeconds, _config.BreakSeconds);
            AppLog.Log(LogChannel.AI, "Assault starts in {0} seconds.", _config.FirstWaveDelay);
        }
        else if (!alarmed && Scheduler.CurrentPhase != AIWaveScheduler.Phase.Inactive)
        {
            Reset();
        }
    }

    private void Handle(AIWaveScheduler.Change change, int players)
    {
        switch (change)
        {
            case AIWaveScheduler.Change.AssaultStarted:
                QueueAssault(AIWavePlanner.Budget(_config, Scheduler.AssaultNumber, players));
                break;
            case AIWaveScheduler.Change.FinalPushStarted:
                QueueAssault(AIWavePlanner.FinalPushBudget(_config, Scheduler.AssaultNumber, players));
                break;
            case AIWaveScheduler.Change.BreakStarted:
                SpawnQueue.Clear();
                AppLog.Log(LogChannel.AI, "Break for {0} seconds.", _config.BreakSeconds);
                break;
        }
    }

    private void QueueAssault(int budget)
    {
        int assault = Scheduler.AssaultNumber;
        SpawnQueue.Clear();
        AIWavePlanner.Plan(_config.Entries, assault, budget, _random, _plan);
        foreach (int index in _plan)
            SpawnQueue.Enqueue(_config.Entries[index].Prefab);

        AppLog.Log(LogChannel.AI, "Assault {0}: {1} enemies from a budget of {2}.", assault, _plan.Count, budget);
    }

    private void End()
    {
        HasEnded = true;
        SpawnQueue.Clear();
    }

    private void Reset()
    {
        HasEnded = false;
        Scheduler.Stop();
        SpawnQueue.Clear();
    }
}
}
