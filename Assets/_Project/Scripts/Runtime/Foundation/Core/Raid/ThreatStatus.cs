using UnityEngine;

namespace Project.Core.Raid
{
/// <summary>Where Match finds the level's <see cref="IThreatStatus"/>. Null when the level has no AI.</summary>
public static class ThreatStatus
{
    public const byte NoAlarmReason = 255;

    /// <summary>Set by the AI side on every peer when its publisher spawns, cleared when it despawns.</summary>
    public static IThreatStatus Current { get; set; }

    public static bool AlarmRaised => Current != null && Current.AlarmReason != NoAlarmReason;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Current = null;
}
}
