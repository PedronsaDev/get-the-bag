using System;
using Project.Core.Raid;
using UnityEngine;

namespace Project.AI.Alarm
{
public class AlarmStateMachine
{
    private RaidPhase _raidPhase = RaidPhase.Live;

    public AlarmState State { get; private set; } = AlarmState.Stealth;
    public AlarmReason? Reason { get; private set; }
    public Vector3 Position { get; private set; }
    public int CivilianKills { get; private set; }

    public event Action<AlarmReason, Vector3> Raised;

    private bool AcceptsEvents => _raidPhase is RaidPhase.Live or RaidPhase.Extraction;

    private bool HasProgress => State != AlarmState.Stealth || CivilianKills != 0;

    /// <summary>
    /// Follows the raid: a new level (Loading) or leaving Ended starts from a clean slate. Returns true when
    /// it cleared something. The alarm and its counters stay readable from Ended until then.
    /// </summary>
    public bool FollowRaid(RaidPhase raidPhase)
    {
        RaidPhase previous = _raidPhase;
        _raidPhase = raidPhase;

        bool newRaid = raidPhase == RaidPhase.Loading || (previous == RaidPhase.Ended && raidPhase != RaidPhase.Ended);
        if (!newRaid || !HasProgress)
            return false;

        Reset();
        return true;
    }

    public bool TryRaise(AlarmReason reason, Vector3 position)
    {
        if (!AcceptsEvents || State == AlarmState.Alarmed)
            return false;

        State = AlarmState.Alarmed;
        Reason = reason;
        Position = position;
        Raised?.Invoke(reason, position);
        return true;
    }

    public bool RecordCivilianKill(Vector3 position)
    {
        if (!AcceptsEvents)
            return false;

        CivilianKills++;
        return TryRaise(AlarmReason.CivilianKilled, position);
    }

    public void Reset()
    {
        CivilianKills = 0;
        State = AlarmState.Stealth;
        Reason = null;
        Position = default;
    }
}
}
