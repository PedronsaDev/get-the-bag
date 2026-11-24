using System.Collections.Generic;
using Project.AI.Alarm;
using Project.Core.Raid;

namespace Project.Development.Overlays
{
/// <summary>
/// Turns changes in the raid and threat status into short readable lines, newest last.
/// It only watches the synced status, so it works the same on a host and on a client.
/// </summary>
public sealed class ThreatEventLog
{
    private const int Capacity = 8;

    private readonly List<string> _lines = new(Capacity);
    private bool _hasBaseline;
    private RaidPhase _raid;
    private ThreatPhase _threat;
    private byte _alarmReason = ThreatStatus.NoAlarmReason;
    private int _civilianKills;

    public IReadOnlyList<string> Lines => _lines;

    public void Add(float seconds, string message)
    {
        if (_lines.Count == Capacity)
            _lines.RemoveAt(0);
        _lines.Add($"{FormatClock(seconds)}  {message}");
    }

    /// <summary>Call every frame. The first call only records where things stand.</summary>
    public void Observe(float seconds, RaidPhase raid, IThreatStatus status)
    {
        if (!_hasBaseline)
        {
            _hasBaseline = true;
            Remember(raid, status);
            return;
        }

        if (raid != _raid)
            Add(seconds, $"Raid is now {raid}");

        if (status != null)
            ObserveThreat(seconds, status);

        Remember(raid, status);
    }

    public static string FormatClock(float seconds)
    {
        int whole = seconds < 0f ? 0 : (int)seconds;
        return $"{whole / 60}:{whole % 60:00}";
    }

    private void ObserveThreat(float seconds, IThreatStatus status)
    {
        bool alarmJustRaised = status.AlarmReason != ThreatStatus.NoAlarmReason && status.AlarmReason != _alarmReason;
        if (alarmJustRaised)
            Add(seconds, $"ALARM raised: {(AlarmReason)status.AlarmReason}");

        if (status.Phase != _threat)
            Add(seconds, Describe(status));

        if (status.CivilianKills > _civilianKills)
            Add(seconds, $"Civilian killed ({status.CivilianKills} so far)");
    }

    private static string Describe(IThreatStatus status)
    {
        switch (status.Phase)
        {
            case ThreatPhase.Alarmed: return $"Police are coming ({status.SecondsInPhase:0} s)";
            case ThreatPhase.Assault: return $"Assault {status.AssaultNumber} began";
            case ThreatPhase.Break: return "Break began";
            case ThreatPhase.FinalPush: return "Final push began";
            case ThreatPhase.Ended: return "Threat ended";
            default: return "Back to stealth";
        }
    }

    private void Remember(RaidPhase raid, IThreatStatus status)
    {
        _raid = raid;
        _threat = status != null ? status.Phase : ThreatPhase.Stealth;
        _alarmReason = status != null ? status.AlarmReason : ThreatStatus.NoAlarmReason;
        _civilianKills = status != null ? status.CivilianKills : 0;
    }
}
}
