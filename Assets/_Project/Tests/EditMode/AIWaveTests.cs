using System.Collections.Generic;
using NUnit.Framework;
using Project.AI.Waves;
using UnityEditor;
using UnityEngine;

namespace Project.AI.Tests
{
public class AIWaveTests
{
    private GameObject _prefab;

    [SetUp]
    public void SetUp() => _prefab = new GameObject("Wave Test Prefab");

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_prefab);

    private AIWaveConfig.Entry Entry(int cost, int firstWave = 1, int weight = 1) =>
        new() { Prefab = _prefab, Cost = cost, FirstWave = firstWave, Weight = weight };

    private static AIWaveConfig NewConfig() => ScriptableObject.CreateInstance<AIWaveConfig>();

    [Test]
    public void Budget_GrowsWithWavesAndExtraPlayers()
    {
        var config = NewConfig();

        Assert.AreEqual(6, AIWavePlanner.Budget(config, 1, 1));
        Assert.AreEqual(10, AIWavePlanner.Budget(config, 3, 1));
        Assert.AreEqual(19, AIWavePlanner.Budget(config, 3, 4)); // 6 + 2 * 2 + 3 * 3
        Object.DestroyImmediate(config);
    }

    [Test]
    public void MaxAlive_GrowsWithPlayers()
    {
        var config = NewConfig();

        Assert.AreEqual(12, AIWavePlanner.MaxAlive(config, 1));
        Assert.AreEqual(24, AIWavePlanner.MaxAlive(config, 4));
        Object.DestroyImmediate(config);
    }

    [Test]
    public void Plan_NeverSpendsMoreThanTheBudget()
    {
        var entries = new List<AIWaveConfig.Entry> { Entry(1), Entry(3), Entry(5) };
        var picked = new List<int>();

        for (int seed = 0; seed < 50; seed++)
        {
            AIWavePlanner.Plan(entries, 1, 11, new System.Random(seed), picked);

            int spent = 0;
            foreach (int index in picked)
                spent += entries[index].Cost;
            Assert.LessOrEqual(spent, 11, $"seed {seed}");
        }
    }

    [Test]
    public void Plan_SpendsTheWholeBudgetWhenACheapEnemyExists()
    {
        var entries = new List<AIWaveConfig.Entry> { Entry(1), Entry(4) };
        var picked = new List<int>();

        AIWavePlanner.Plan(entries, 1, 9, new System.Random(1), picked);

        int spent = 0;
        foreach (int index in picked)
            spent += entries[index].Cost;
        Assert.AreEqual(9, spent);
    }

    [Test]
    public void Plan_OnlyUsesEnemiesUnlockedForTheWave()
    {
        var entries = new List<AIWaveConfig.Entry> { Entry(1, firstWave: 1), Entry(1, firstWave: 3) };
        var picked = new List<int>();

        AIWavePlanner.Plan(entries, 2, 20, new System.Random(7), picked);

        Assert.IsNotEmpty(picked);
        CollectionAssert.DoesNotContain(picked, 1);

        AIWavePlanner.Plan(entries, 3, 40, new System.Random(7), picked);
        CollectionAssert.Contains(picked, 1);
    }

    [Test]
    public void Plan_IsEmptyWhenNothingIsAffordable()
    {
        var entries = new List<AIWaveConfig.Entry> { Entry(10) };
        var picked = new List<int> { 99 };

        AIWavePlanner.Plan(entries, 1, 5, new System.Random(0), picked);

        Assert.IsEmpty(picked);
    }

    [Test]
    public void Plan_SameSeedGivesTheSameWave()
    {
        var entries = new List<AIWaveConfig.Entry> { Entry(1), Entry(2), Entry(3) };
        var first = new List<int>();
        var second = new List<int>();

        AIWavePlanner.Plan(entries, 1, 15, new System.Random(42), first);
        AIWavePlanner.Plan(entries, 1, 15, new System.Random(42), second);

        CollectionAssert.AreEqual(first, second);
    }

    [Test]
    public void Plan_WeightsChangeHowOftenAnEnemyIsPicked()
    {
        var entries = new List<AIWaveConfig.Entry> { Entry(1, weight: 9), Entry(1, weight: 1) };
        var picked = new List<int>();
        var random = new System.Random(3);
        int common = 0;
        int rare = 0;

        for (int i = 0; i < 100; i++)
        {
            AIWavePlanner.Plan(entries, 1, 10, random, picked);
            foreach (int index in picked)
            {
                if (index == 0) common++;
                else rare++;
            }
        }

        Assert.Greater(common, rare * 4);
    }

    [Test]
    public void Config_NeedsEntriesWithPrefabs()
    {
        var config = NewConfig();
        Assert.IsFalse(config.TryValidate(out string error));
        StringAssert.Contains("entry", error);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void FinalPushBudget_IsTheNormalBudgetTimesTheMultiplier()
    {
        var config = NewConfig();

        // Defaults: assault 2 with one player is 8 points, assault 3 is 10; the multiplier is 1.5.
        Assert.AreEqual(12, AIWavePlanner.FinalPushBudget(config, 2, 1));
        Assert.AreEqual(15, AIWavePlanner.FinalPushBudget(config, 3, 1));
        Object.DestroyImmediate(config);
    }

    [Test]
    public void Config_RejectsAFinalPushSmallerThanANormalAssault()
    {
        var config = NewConfig();
        var serialized = new SerializedObject(config);
        var entries = serialized.FindProperty("_entries");
        entries.arraySize = 1;
        var entry = entries.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("Prefab").objectReferenceValue = _prefab;
        entry.FindPropertyRelative("Cost").intValue = 1;
        entry.FindPropertyRelative("Weight").intValue = 1;
        entry.FindPropertyRelative("FirstWave").intValue = 1;
        serialized.FindProperty("_finalPushBudgetMultiplier").floatValue = 0.5f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.IsFalse(config.TryValidate(out string error));
        StringAssert.Contains("multiplier", error);
        Object.DestroyImmediate(config);
    }

    private static AIWaveScheduler StartedScheduler()
    {
        var scheduler = new AIWaveScheduler();
        scheduler.Start(45f, 90f, 30f);
        return scheduler;
    }

    private static AIWaveScheduler SchedulerInAssault(int assaultNumber)
    {
        var scheduler = StartedScheduler();
        scheduler.Tick(45f);
        for (int number = 2; number <= assaultNumber; number++)
        {
            scheduler.Tick(90f);
            scheduler.Tick(30f);
        }
        return scheduler;
    }

    [Test]
    public void Scheduler_StaysQuietUntilStarted()
    {
        var scheduler = new AIWaveScheduler();

        Assert.AreEqual(AIWaveScheduler.Change.None, scheduler.Tick(100f));
        Assert.AreEqual(AIWaveScheduler.Phase.Inactive, scheduler.CurrentPhase);
    }

    [Test]
    public void Scheduler_RunsCountdownThenAssaultThenBreakThenAssault()
    {
        var scheduler = StartedScheduler();
        Assert.AreEqual(AIWaveScheduler.Phase.Countdown, scheduler.CurrentPhase);

        Assert.AreEqual(AIWaveScheduler.Change.None, scheduler.Tick(44f));
        Assert.AreEqual(AIWaveScheduler.Change.AssaultStarted, scheduler.Tick(1.5f));
        Assert.AreEqual(AIWaveScheduler.Phase.Assault, scheduler.CurrentPhase);
        Assert.AreEqual(1, scheduler.AssaultNumber);

        Assert.AreEqual(AIWaveScheduler.Change.None, scheduler.Tick(89f));
        Assert.AreEqual(AIWaveScheduler.Change.BreakStarted, scheduler.Tick(2f));
        Assert.AreEqual(AIWaveScheduler.Phase.Break, scheduler.CurrentPhase);
        Assert.AreEqual(1, scheduler.AssaultNumber, "A break does not start a new assault.");

        Assert.AreEqual(AIWaveScheduler.Change.None, scheduler.Tick(29f));
        Assert.AreEqual(AIWaveScheduler.Change.AssaultStarted, scheduler.Tick(2f));
        Assert.AreEqual(2, scheduler.AssaultNumber);
    }

    [Test]
    public void Scheduler_ReportsTheSecondsLeftInEachTimedPhase()
    {
        var scheduler = StartedScheduler();
        scheduler.Tick(5f);
        Assert.AreEqual(40f, scheduler.SecondsInPhase, 0.001f);

        scheduler.Tick(40.5f);
        Assert.AreEqual(90f, scheduler.SecondsInPhase, 0.001f);

        scheduler.Tick(91f);
        Assert.AreEqual(30f, scheduler.SecondsInPhase, 0.001f);
    }

    [Test]
    public void FinalPush_DuringAnAssault_KeepsItsNumberAndNeverBreaks()
    {
        var scheduler = SchedulerInAssault(2);

        Assert.AreEqual(AIWaveScheduler.Change.FinalPushStarted, scheduler.BeginFinalPush());
        Assert.AreEqual(AIWaveScheduler.Phase.FinalPush, scheduler.CurrentPhase);
        Assert.AreEqual(2, scheduler.AssaultNumber);

        for (int i = 0; i < 20; i++)
        {
            Assert.AreEqual(AIWaveScheduler.Change.None, scheduler.Tick(100f));
            Assert.AreEqual(AIWaveScheduler.Phase.FinalPush, scheduler.CurrentPhase, "The final push has no break.");
        }
        Assert.AreEqual(0f, scheduler.SecondsInPhase);
    }

    [Test]
    public void FinalPush_DuringABreak_TakesTheNextAssaultNumber()
    {
        var scheduler = SchedulerInAssault(1);
        scheduler.Tick(91f);
        Assert.AreEqual(AIWaveScheduler.Phase.Break, scheduler.CurrentPhase);

        Assert.AreEqual(AIWaveScheduler.Change.FinalPushStarted, scheduler.BeginFinalPush());
        Assert.AreEqual(2, scheduler.AssaultNumber);
    }

    [Test]
    public void FinalPush_DuringTheCountdown_IsTheFirstAssault()
    {
        var scheduler = StartedScheduler();

        Assert.AreEqual(AIWaveScheduler.Change.FinalPushStarted, scheduler.BeginFinalPush());
        Assert.AreEqual(1, scheduler.AssaultNumber);
    }

    [Test]
    public void FinalPush_IsIgnoredWhenInactiveOrAlreadyStarted()
    {
        var inactive = new AIWaveScheduler();
        Assert.AreEqual(AIWaveScheduler.Change.None, inactive.BeginFinalPush());
        Assert.AreEqual(AIWaveScheduler.Phase.Inactive, inactive.CurrentPhase);

        var scheduler = SchedulerInAssault(1);
        scheduler.BeginFinalPush();
        int number = scheduler.AssaultNumber;
        Assert.AreEqual(AIWaveScheduler.Change.None, scheduler.BeginFinalPush());
        Assert.AreEqual(number, scheduler.AssaultNumber);
    }

    [Test]
    public void Scheduler_StopResetsEverything()
    {
        var scheduler = SchedulerInAssault(2);

        scheduler.Stop();

        Assert.AreEqual(AIWaveScheduler.Phase.Inactive, scheduler.CurrentPhase);
        Assert.AreEqual(0, scheduler.AssaultNumber);
        scheduler.Start(10f, 90f, 30f);
        Assert.AreEqual(AIWaveScheduler.Change.AssaultStarted, scheduler.Tick(11f));
        Assert.AreEqual(1, scheduler.AssaultNumber, "A new alarm starts again from assault 1.");
    }
}
}
