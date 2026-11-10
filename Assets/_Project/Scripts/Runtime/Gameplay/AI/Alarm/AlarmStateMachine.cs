using System;
using UnityEngine;

namespace Project.AI.Alarm
{
public class AlarmStateMachine
{
    public AlarmState State { get; private set; } = AlarmState.Stealth;
    public AlarmReason? Reason { get; private set; }
    public Vector3 Position { get; private set; }
    public int CivilianKills { get; private set; }

    public event Action<AlarmReason, Vector3> Raised;

    public bool TryRaise(AlarmReason reason, Vector3 position)
    {
        if (State == AlarmState.Alarmed)
            return false;

        State = AlarmState.Alarmed;
        Reason = reason;
        Position = position;
        Raised?.Invoke(reason, position);
        return true;
    }

    public bool RecordCivilianKill(Vector3 position)
    {
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
