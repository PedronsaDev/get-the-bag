namespace Project.Core.Raid
{
/// <summary>
/// Read-only view of the level's threat, implemented by the AI side from SyncVars so it is valid on every peer.
/// Counters (alarm reason, civilian kills, assault number) are final the moment the raid resolves;
/// <see cref="Phase"/> may lag by a frame.
/// </summary>
public interface IThreatStatus
{
    ThreatPhase Phase { get; }

    /// <summary>Whole seconds left in the current timed phase, updated about once a second. 0 when untimed.</summary>
    float SecondsInPhase { get; }

    int AssaultNumber { get; }

    /// <summary>
    /// The value of Project.AI.Alarm.AlarmReason as a byte, or <see cref="ThreatStatus.NoAlarmReason"/>.
    /// Fixed values: GuardAlerted 0, BodyFound 1, CivilianCall 2, CivilianKilled 3. New reasons are only appended.
    /// </summary>
    byte AlarmReason { get; }

    int CivilianKills { get; }

    /// <summary>Living Guard and Police agents, hand-placed ones included.</summary>
    int EnemiesAlive { get; }
}
}
