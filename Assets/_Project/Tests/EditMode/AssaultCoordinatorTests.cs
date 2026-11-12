using NUnit.Framework;
using Project.AI.Waves;
using Project.Core.Raid;
using UnityEditor;
using UnityEngine;

namespace Project.AI.Tests
{
public class AssaultCoordinatorTests
{
    private GameObject _prefab;
    private AIWaveConfig _config;
    private AssaultCoordinator _coordinator;

    [SetUp]
    public void SetUp()
    {
        _prefab = new GameObject("Coordinator Test Prefab");
        _config = ScriptableObject.CreateInstance<AIWaveConfig>();

        // One enemy that costs 1, so the queue length equals the budget: 6 for assault 1, 9 for its final push.
        var serialized = new SerializedObject(_config);
        var entries = serialized.FindProperty("_entries");
        entries.arraySize = 1;
        var entry = entries.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("Prefab").objectReferenceValue = _prefab;
        entry.FindPropertyRelative("Cost").intValue = 1;
        entry.FindPropertyRelative("Weight").intValue = 1;
        entry.FindPropertyRelative("FirstWave").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        _coordinator = new AssaultCoordinator(_config, new System.Random(1));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_config);
        Object.DestroyImmediate(_prefab);
    }

    private void Advance(float seconds, RaidPhase raid, bool alarmed) => _coordinator.Advance(seconds, raid, alarmed, 1);

    private void RunToFirstAssault()
    {
        Advance(0.1f, RaidPhase.Live, true);
        Advance(46f, RaidPhase.Live, true);
    }

    [Test]
    public void IgnoresTheAlarmWhileTheLevelIsLoading()
    {
        Advance(100f, RaidPhase.Loading, true);

        Assert.AreEqual(AIWaveScheduler.Phase.Inactive, _coordinator.Scheduler.CurrentPhase);
        Assert.AreEqual(ThreatPhase.Stealth, _coordinator.CurrentThreatPhase);
    }

    [Test]
    public void StaysInStealthWithoutAnAlarm()
    {
        for (int i = 0; i < 5; i++)
            Advance(100f, RaidPhase.Live, false);

        Assert.AreEqual(ThreatPhase.Stealth, _coordinator.CurrentThreatPhase);
        Assert.IsEmpty(_coordinator.SpawnQueue);
    }

    [Test]
    public void AlarmWhileLive_CountsDownThenStartsAssaultOne()
    {
        Advance(0.1f, RaidPhase.Live, true);
        Assert.AreEqual(ThreatPhase.Alarmed, _coordinator.CurrentThreatPhase);

        Advance(46f, RaidPhase.Live, true);

        Assert.AreEqual(ThreatPhase.Assault, _coordinator.CurrentThreatPhase);
        Assert.AreEqual(1, _coordinator.Scheduler.AssaultNumber);
        Assert.AreEqual(6, _coordinator.SpawnQueue.Count);
    }

    [Test]
    public void Break_DropsWhatWasNotSpawnedYet()
    {
        RunToFirstAssault();

        Advance(91f, RaidPhase.Live, true);

        Assert.AreEqual(ThreatPhase.Break, _coordinator.CurrentThreatPhase);
        Assert.IsEmpty(_coordinator.SpawnQueue);
    }

    [Test]
    public void Extraction_StartsTheFinalPushWithABonusBudget()
    {
        RunToFirstAssault();

        Advance(1f, RaidPhase.Extraction, true);

        Assert.AreEqual(ThreatPhase.FinalPush, _coordinator.CurrentThreatPhase);
        Assert.AreEqual(1, _coordinator.Scheduler.AssaultNumber);
        Assert.AreEqual(9, _coordinator.SpawnQueue.Count, "6 points times 1.5.");
    }

    [Test]
    public void FinalPush_NeverGoesToBreak()
    {
        RunToFirstAssault();
        Advance(1f, RaidPhase.Extraction, true);

        for (int i = 0; i < 20; i++)
        {
            Advance(100f, RaidPhase.Extraction, true);
            Assert.AreEqual(ThreatPhase.FinalPush, _coordinator.CurrentThreatPhase);
        }
    }

    [Test]
    public void Extraction_WithoutAnAlarm_StaysQuiet()
    {
        Advance(10f, RaidPhase.Extraction, false);

        Assert.AreEqual(ThreatPhase.Stealth, _coordinator.CurrentThreatPhase);
        Assert.IsEmpty(_coordinator.SpawnQueue);
    }

    [Test]
    public void AlarmRaisedDuringExtraction_GoesStraightToTheFinalPush()
    {
        Advance(0.1f, RaidPhase.Extraction, true);

        Assert.AreEqual(ThreatPhase.FinalPush, _coordinator.CurrentThreatPhase);
        Assert.AreEqual(1, _coordinator.Scheduler.AssaultNumber);
    }

    [Test]
    public void Ended_ClearsTheQueueAndFreezesTheCounters()
    {
        RunToFirstAssault();
        Assert.IsNotEmpty(_coordinator.SpawnQueue);

        Advance(1f, RaidPhase.Ended, true);

        Assert.IsTrue(_coordinator.HasEnded);
        Assert.AreEqual(ThreatPhase.Ended, _coordinator.CurrentThreatPhase);
        Assert.IsEmpty(_coordinator.SpawnQueue);
        Assert.AreEqual(1, _coordinator.Scheduler.AssaultNumber, "The results still need the assault count.");

        Advance(500f, RaidPhase.Ended, true);
        Assert.AreEqual(1, _coordinator.Scheduler.AssaultNumber);
        Assert.IsEmpty(_coordinator.SpawnQueue);
    }

    [Test]
    public void Ended_WithoutAnAlarm_StillReportsEnded()
    {
        Advance(1f, RaidPhase.Ended, false);

        Assert.AreEqual(ThreatPhase.Ended, _coordinator.CurrentThreatPhase);
        Assert.AreEqual(0, _coordinator.Scheduler.AssaultNumber);
    }

    [Test]
    public void NextLevel_StartsFromACleanSlate()
    {
        RunToFirstAssault();
        Advance(1f, RaidPhase.Ended, true);

        Advance(0.1f, RaidPhase.Loading, false);

        Assert.IsFalse(_coordinator.HasEnded);
        Assert.AreEqual(ThreatPhase.Stealth, _coordinator.CurrentThreatPhase);
        Assert.AreEqual(0, _coordinator.Scheduler.AssaultNumber);
    }

    [Test]
    public void LoadingAgainMidRaid_AbandonsTheSchedule()
    {
        RunToFirstAssault();

        Advance(0.1f, RaidPhase.Loading, true);

        Assert.AreEqual(AIWaveScheduler.Phase.Inactive, _coordinator.Scheduler.CurrentPhase);
        Assert.IsEmpty(_coordinator.SpawnQueue);
    }
}
}
