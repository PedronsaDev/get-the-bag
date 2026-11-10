using NUnit.Framework;
using Project.AI.Patrol;
using Project.AI.Patrol.Editor;
using UnityEngine;

namespace Project.AI.Tests
{
public class PatrolPathUtilityTests
{
    private GameObject _routeObject;
    private AIPatrolRoute _route;

    [SetUp]
    public void SetUp()
    {
        _routeObject = new GameObject("Test Route");
        _route = _routeObject.AddComponent<AIPatrolRoute>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_routeObject);
    }

    private void AddSquare()
    {
        PatrolPathUtility.AddPoint(_route, new Vector3(0f, 0f, 0f));
        PatrolPathUtility.AddPoint(_route, new Vector3(10f, 0f, 0f));
        PatrolPathUtility.AddPoint(_route, new Vector3(10f, 0f, 10f));
        PatrolPathUtility.AddPoint(_route, new Vector3(0f, 0f, 10f));
    }

    [Test]
    public void AddPoint_AppendsAndNumbersPoints()
    {
        AddSquare();

        Assert.AreEqual(4, _route.PointCount);
        Assert.AreEqual("Point 01", _route.GetPoint(0).name);
        Assert.AreEqual("Point 04", _route.GetPoint(3).name);
        Assert.AreEqual(new Vector3(10f, 0f, 10f), _route.GetPoint(2).position);
    }

    [Test]
    public void AddPoint_WithIndexInsertsAndRenumbers()
    {
        AddSquare();

        int index = PatrolPathUtility.AddPoint(_route, new Vector3(5f, 0f, 0f), 1);

        Assert.AreEqual(1, index);
        Assert.AreEqual(5, _route.PointCount);
        Assert.AreEqual(new Vector3(5f, 0f, 0f), _route.GetPoint(1).position);
        Assert.AreEqual("Point 02", _route.GetPoint(1).name);
        Assert.AreEqual("Point 05", _route.GetPoint(4).name);
    }

    [Test]
    public void RemovePoint_RenumbersTheRest()
    {
        AddSquare();

        PatrolPathUtility.RemovePoint(_route, 1);

        Assert.AreEqual(3, _route.PointCount);
        Assert.AreEqual(new Vector3(10f, 0f, 10f), _route.GetPoint(1).position);
        Assert.AreEqual("Point 02", _route.GetPoint(1).name);
    }

    [Test]
    public void MovePoint_ReordersChildren()
    {
        AddSquare();

        PatrolPathUtility.MovePoint(_route, 0, 2);

        Assert.AreEqual(new Vector3(10f, 0f, 0f), _route.GetPoint(0).position);
        Assert.AreEqual(new Vector3(0f, 0f, 0f), _route.GetPoint(2).position);
        Assert.AreEqual("Point 03", _route.GetPoint(2).name);
    }

    [Test]
    public void Reverse_FlipsOrder()
    {
        AddSquare();

        PatrolPathUtility.Reverse(_route);

        Assert.AreEqual(new Vector3(0f, 0f, 10f), _route.GetPoint(0).position);
        Assert.AreEqual(new Vector3(0f, 0f, 0f), _route.GetPoint(3).position);
    }

    [Test]
    public void FaceAlongPath_PointsAtTheNextWaypoint()
    {
        AddSquare();

        PatrolPathUtility.FaceAlongPath(_route);

        AssertFacing(_route.GetPoint(0), Vector3.right);
        AssertFacing(_route.GetPoint(1), Vector3.forward);
        AssertFacing(_route.GetPoint(3), Vector3.back, "A looping route's last point faces the first.");
    }

    [Test]
    public void NearestSegmentStart_FindsTheClosestSide()
    {
        AddSquare();

        Assert.AreEqual(0, PatrolPathUtility.NearestSegmentStart(_route, new Vector3(5f, 0f, -1f)));
        Assert.AreEqual(1, PatrolPathUtility.NearestSegmentStart(_route, new Vector3(11f, 0f, 5f)));
        Assert.AreEqual(3, PatrolPathUtility.NearestSegmentStart(_route, new Vector3(-1f, 0f, 5f)),
            "The closing segment of a loop is considered.");
    }

    [Test]
    public void Validate_EmptyRouteIsAnError()
    {
        var issues = PatrolPathUtility.Validate(_route);

        Assert.AreEqual(1, issues.Count);
        Assert.AreEqual(PatrolIssueSeverity.Error, issues[0].Severity);
    }

    [Test]
    public void Validate_FlagsEveryPointWithoutNavMesh()
    {
        AddSquare();

        var issues = PatrolPathUtility.Validate(_route);

        int pointErrors = 0;
        foreach (var issue in issues)
        {
            if (issue.PointIndex >= 0 && issue.Severity == PatrolIssueSeverity.Error)
                pointErrors++;
        }
        Assert.AreEqual(4, pointErrors);
    }

    [Test]
    public void Validate_WarnsAboutDuplicatePoints()
    {
        PatrolPathUtility.AddPoint(_route, Vector3.zero);
        PatrolPathUtility.AddPoint(_route, new Vector3(0.1f, 0f, 0f));

        var issues = PatrolPathUtility.Validate(_route);

        Assert.IsTrue(issues.Exists(i => i.Severity == PatrolIssueSeverity.Warning && i.Message.Contains("same place")));
    }

    [Test]
    public void PathLength_AddsStraightSegmentsWithoutNavMesh()
    {
        AddSquare();

        Assert.AreEqual(40f, PatrolPathUtility.PathLength(_route), 0.01f);
    }

    [Test]
    public void EstimateCycleSeconds_IncludesWaiting()
    {
        AddSquare();

        float expected = 40f / 2f + 4 * _route.WaitSeconds;
        Assert.AreEqual(expected, PatrolPathUtility.EstimateCycleSeconds(_route, 2f), 0.01f);
        Assert.AreEqual(0f, PatrolPathUtility.EstimateCycleSeconds(_route, 0f));
    }

    private static void AssertFacing(Transform point, Vector3 expected, string message = null)
    {
        Assert.Greater(Vector3.Dot(point.forward, expected), 0.99f, message);
    }
}
}
