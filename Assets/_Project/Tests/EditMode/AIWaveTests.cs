using System.Collections.Generic;
using NUnit.Framework;
using Project.AI.Waves;
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
    public void Scheduler_StaysQuietUntilStarted()
    {
        var scheduler = new AIWaveScheduler();

        Assert.AreEqual(0, scheduler.Tick(100f, 0, 12, 60f));
        Assert.AreEqual(AIWaveScheduler.Phase.Inactive, scheduler.CurrentPhase);
    }

    [Test]
    public void Scheduler_FirstWaveComesAfterTheDelayThenEveryInterval()
    {
        var scheduler = new AIWaveScheduler();
        scheduler.Start(45f);

        Assert.AreEqual(0, scheduler.Tick(44f, 0, 12, 60f));
        Assert.AreEqual(1, scheduler.Tick(1.5f, 0, 12, 60f));
        Assert.AreEqual(0, scheduler.Tick(59f, 0, 12, 60f));
        Assert.AreEqual(2, scheduler.Tick(2f, 0, 12, 60f));
        Assert.AreEqual(2, scheduler.WaveNumber);
    }

    [Test]
    public void Scheduler_HoldsALaterWaveWhileTooManyAreAlive()
    {
        var scheduler = new AIWaveScheduler();
        scheduler.Start(0f);
        Assert.AreEqual(1, scheduler.Tick(0.1f, 0, 12, 60f));

        Assert.AreEqual(0, scheduler.Tick(61f, 12, 12, 60f), "At the cap: wait.");
        Assert.AreEqual(0, scheduler.Tick(5f, 12, 12, 60f), "Still waiting.");
        Assert.AreEqual(2, scheduler.Tick(0.1f, 5, 12, 60f), "Room again: the wave goes immediately.");
    }

    [Test]
    public void Scheduler_StopResetsEverything()
    {
        var scheduler = new AIWaveScheduler();
        scheduler.Start(0f);
        scheduler.Tick(1f, 0, 12, 60f);

        scheduler.Stop();

        Assert.AreEqual(AIWaveScheduler.Phase.Inactive, scheduler.CurrentPhase);
        Assert.AreEqual(0, scheduler.WaveNumber);
        scheduler.Start(10f);
        Assert.AreEqual(1, scheduler.Tick(11f, 0, 12, 60f), "A new alarm starts again from wave 1.");
    }
}
}
