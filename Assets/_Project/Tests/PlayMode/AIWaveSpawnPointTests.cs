using System.Collections.Generic;
using NUnit.Framework;
using Project.AI.Waves;
using UnityEngine;

namespace Project.AI.Tests
{
public class AIWaveSpawnPointTests
{
    private readonly List<GameObject> _created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var created in _created)
            Object.DestroyImmediate(created);
        _created.Clear();
    }

    private AIWaveSpawnPoint AddPoint(Vector3 position)
    {
        var go = new GameObject("Test Spawn Point");
        go.transform.position = position;
        _created.Add(go);
        return go.AddComponent<AIWaveSpawnPoint>();
    }

    [Test]
    public void Pick_SkipsPointsCloserThanTheMinimumDistance()
    {
        var near = AddPoint(new Vector3(5f, 0f, 0f));
        var far = AddPoint(new Vector3(50f, 0f, 0f));

        for (int i = 0; i < 20; i++)
        {
            Assert.IsTrue(AIWaveSpawnPoint.TryPick(new[] { Vector3.zero }, 18f, out var picked));
            Assert.AreSame(far, picked);
            Assert.AreNotSame(near, picked);
        }
    }

    [Test]
    public void Pick_FailsWhenEveryPointIsTooClose()
    {
        AddPoint(new Vector3(3f, 0f, 0f));

        Assert.IsFalse(AIWaveSpawnPoint.TryPick(new[] { Vector3.zero }, 18f, out var picked));
        Assert.IsNull(picked);
    }

    [Test]
    public void Pick_PrefersAPointNoPlayerCanSee()
    {
        var visible = AddPoint(new Vector3(30f, 0f, 0f));
        var hidden = AddPoint(new Vector3(0f, 0f, 30f));

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = new Vector3(0f, 1f, 15f);
        wall.transform.localScale = new Vector3(10f, 6f, 1f);
        _created.Add(wall);
        Physics.SyncTransforms();

        for (int i = 0; i < 20; i++)
        {
            Assert.IsTrue(AIWaveSpawnPoint.TryPick(new[] { Vector3.zero }, 18f, out var picked));
            Assert.AreSame(hidden, picked);
            Assert.AreNotSame(visible, picked);
        }
    }

    [Test]
    public void Pick_UsesAVisiblePointWhenNoneIsHidden()
    {
        var visible = AddPoint(new Vector3(30f, 0f, 0f));

        Assert.IsTrue(AIWaveSpawnPoint.TryPick(new[] { Vector3.zero }, 18f, out var picked));
        Assert.AreSame(visible, picked);
    }
}
}
