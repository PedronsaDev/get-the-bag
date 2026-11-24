using System.Reflection;
using NUnit.Framework;
using Project.AI.Waves;
using Project.Core.Raid;
using UnityEngine;

namespace Project.AI.Tests
{
public class ThreatStatusPublisherTests
{
    private GameObject _object;
    private AssaultDirector _director;

    [SetUp]
    public void SetUp()
    {
        ThreatStatus.Current = null;
        _object = new GameObject("Test Assault Director");
        _director = _object.AddComponent<AssaultDirector>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_object);
        ThreatStatus.Current = null;
        RaidLifecycle.ResetForTests();
    }

    private void Call(string method) =>
        typeof(AssaultDirector)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null)
            .Invoke(_director, null);

    [Test]
    public void Spawning_PublishesTheDirectorAsTheCurrentStatus()
    {
        Call("OnSpawned");

        Assert.AreSame(_director, ThreatStatus.Current);
    }

    [Test]
    public void BeforeAnyAlarm_TheStatusReadsAsStealth()
    {
        Call("OnSpawned");
        var status = ThreatStatus.Current;

        Assert.AreEqual(ThreatPhase.Stealth, status.Phase);
        Assert.AreEqual(0f, status.SecondsInPhase);
        Assert.AreEqual(0, status.AssaultNumber);
        Assert.AreEqual(ThreatStatus.NoAlarmReason, status.AlarmReason);
        Assert.AreEqual(0, status.CivilianKills);
        Assert.AreEqual(0, status.EnemiesAlive);
        Assert.IsFalse(ThreatStatus.AlarmRaised);
    }

    [Test]
    public void Despawning_ClearsTheStatusOnlyIfItIsStillTheDirector()
    {
        Call("OnSpawned");
        Call("OnDespawned");
        Assert.IsNull(ThreatStatus.Current);

        var other = new GameObject("Other Status").AddComponent<AssaultDirector>();
        ThreatStatus.Current = other;
        Call("OnDespawned");

        Assert.AreSame(other, ThreatStatus.Current, "A newer director keeps its place.");
        ThreatStatus.Current = null;
        Object.DestroyImmediate(other.gameObject);
    }
}
}
