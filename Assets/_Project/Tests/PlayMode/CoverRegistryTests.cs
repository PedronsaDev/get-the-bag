using System.Collections.Generic;
using NUnit.Framework;
using Project.AI.Cover;
using UnityEngine;

namespace Project.AI.Tests
{
public class CoverRegistryTests
{
    private readonly List<GameObject> _created = new();
    private readonly List<CoverPoint> _results = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var created in _created)
            Object.DestroyImmediate(created);
        _created.Clear();
    }

    private CoverPoint AddPoint(Vector3 position)
    {
        var go = new GameObject("Test Cover");
        go.transform.position = position;
        _created.Add(go);
        return go.AddComponent<CoverPoint>();
    }

    private int Query(Vector3 centre, float radius) =>
        CoverRegistry.GetCoverPointsInRangeNonAlloc(centre, radius, _results);

    [Test]
    public void Query_FindsOnlyPointsInsideTheRadius()
    {
        var near = AddPoint(new Vector3(5000f, 0f, 5000f));
        var farAway = AddPoint(new Vector3(5040f, 0f, 5000f));

        Assert.AreEqual(1, Query(new Vector3(5000f, 0f, 5000f), 10f));
        Assert.AreSame(near, _results[0]);
        Assert.IsFalse(_results.Contains(farAway));
    }

    [Test]
    public void Query_FindsPointsAcrossGridCells()
    {
        AddPoint(new Vector3(5007f, 0f, 5000f));
        AddPoint(new Vector3(5009f, 0f, 5000f));
        AddPoint(new Vector3(4991f, 0f, 5000f));

        Assert.AreEqual(3, Query(new Vector3(5000f, 0f, 5000f), 12f));
    }

    [Test]
    public void Query_SkipsReservedPoints()
    {
        var point = AddPoint(new Vector3(5000f, 0f, 5000f));
        var owner = new GameObject("Owner");
        _created.Add(owner);
        Assert.IsTrue(point.TryReserve(owner.GetEntityId()));

        Assert.AreEqual(0, Query(new Vector3(5000f, 0f, 5000f), 5f));
    }

    [Test]
    public void Query_ForgetsDisabledPoints()
    {
        var point = AddPoint(new Vector3(5000f, 0f, 5000f));
        point.enabled = false;

        Assert.AreEqual(0, Query(new Vector3(5000f, 0f, 5000f), 5f));

        point.enabled = true;
        Assert.AreEqual(1, Query(new Vector3(5000f, 0f, 5000f), 5f));
    }

    [Test]
    public void DuplicateRegistry_DoesNotWipeTheRegisteredPoints()
    {
        AddPoint(new Vector3(5000f, 0f, 5000f));
        var first = new GameObject("Registry 1");
        var second = new GameObject("Registry 2");
        _created.Add(first);
        _created.Add(second);
        first.AddComponent<CoverRegistry>();
        second.AddComponent<CoverRegistry>();

        Object.DestroyImmediate(second);

        Assert.AreEqual(1, Query(new Vector3(5000f, 0f, 5000f), 5f));
    }
}
}
