using NUnit.Framework;
using Project.Core.Raid;
using Project.Development.Overlays;

namespace Project.AI.Tests
{
public class ThreatEventLogTests
{
    private class FakeStatus : IThreatStatus
    {
        public ThreatPhase Phase { get; set; }
        public float SecondsInPhase { get; set; }
        public int AssaultNumber { get; set; }
        public byte AlarmReason { get; set; } = ThreatStatus.NoAlarmReason;
        public int CivilianKills { get; set; }
        public int EnemiesAlive { get; set; }
    }

    private ThreatEventLog _log;
    private FakeStatus _status;

    [SetUp]
    public void SetUp()
    {
        _log = new ThreatEventLog();
        _status = new FakeStatus();
        _log.Observe(0f, RaidPhase.Live, _status);
    }

    private bool HasLine(string text)
    {
        foreach (var line in _log.Lines)
        {
            if (line.Contains(text))
                return true;
        }
        return false;
    }

    [Test]
    public void FirstObservation_OnlyRecordsABaseline()
    {
        Assert.IsEmpty(_log.Lines);
    }

    [Test]
    public void RaidPhaseChange_IsLogged()
    {
        _log.Observe(5f, RaidPhase.Extraction, _status);

        Assert.IsTrue(HasLine("Raid is now Extraction"));
    }

    [Test]
    public void AlarmRaised_NamesTheReason()
    {
        _status.AlarmReason = 1;
        _status.Phase = ThreatPhase.Alarmed;
        _status.SecondsInPhase = 45f;

        _log.Observe(75f, RaidPhase.Live, _status);

        Assert.IsTrue(HasLine("ALARM raised: BodyFound"));
        Assert.IsTrue(HasLine("Police are coming"));
        Assert.IsTrue(_log.Lines[0].StartsWith("1:15"), "Lines start with the time as m:ss.");
    }

    [Test]
    public void ThreatPhases_AreLoggedOnceEach()
    {
        _status.Phase = ThreatPhase.Assault;
        _status.AssaultNumber = 2;
        _log.Observe(10f, RaidPhase.Live, _status);
        _log.Observe(11f, RaidPhase.Live, _status);
        _status.Phase = ThreatPhase.Break;
        _log.Observe(12f, RaidPhase.Live, _status);
        _status.Phase = ThreatPhase.FinalPush;
        _log.Observe(13f, RaidPhase.Extraction, _status);

        Assert.IsTrue(HasLine("Assault 2 began"));
        Assert.IsTrue(HasLine("Break began"));
        Assert.IsTrue(HasLine("Final push began"));
        int assaultLines = 0;
        foreach (var line in _log.Lines)
            assaultLines += line.Contains("Assault 2 began") ? 1 : 0;
        Assert.AreEqual(1, assaultLines);
    }

    [Test]
    public void CivilianKills_AreLoggedWhenTheCountGrows()
    {
        _status.CivilianKills = 1;
        _log.Observe(20f, RaidPhase.Live, _status);
        _status.CivilianKills = 2;
        _log.Observe(21f, RaidPhase.Live, _status);

        Assert.IsTrue(HasLine("Civilian killed (1 so far)"));
        Assert.IsTrue(HasLine("Civilian killed (2 so far)"));
    }

    [Test]
    public void WithoutAStatus_OnlyTheRaidPhaseIsWatched()
    {
        _log.Observe(3f, RaidPhase.Ended, null);

        Assert.AreEqual(1, _log.Lines.Count);
        Assert.IsTrue(HasLine("Raid is now Ended"));
    }

    [Test]
    public void OnlyTheNewestEightLinesAreKept()
    {
        for (int i = 0; i < 12; i++)
            _log.Add(i, $"event {i}");

        Assert.AreEqual(8, _log.Lines.Count);
        Assert.IsTrue(_log.Lines[0].Contains("event 4"));
        Assert.IsTrue(_log.Lines[7].Contains("event 11"));
    }

    [Test]
    public void Clock_ShowsMinutesAndSeconds()
    {
        Assert.AreEqual("0:00", ThreatEventLog.FormatClock(-3f));
        Assert.AreEqual("0:45", ThreatEventLog.FormatClock(45.9f));
        Assert.AreEqual("2:05", ThreatEventLog.FormatClock(125f));
    }
}
}
