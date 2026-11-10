using System.Collections.Generic;
using NUnit.Framework;
using Project.AI.Cover;
using Project.AI.Cover.Editor;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Project.AI.Tests
{
public class CoverBakerTests
{
    // Far from the real levels, so their NavMesh never takes part in these tests.
    private static readonly Vector3 Origin = new(2000f, 0f, 2000f);

    private GameObject _level;
    private NavMeshSurface _surface;
    private CoverBakeSettings _settings;

    [SetUp]
    public void SetUp()
    {
        _level = new GameObject("Cover Test Level");
        _level.transform.position = Origin;
        _settings = new CoverBakeSettings();

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(_level.transform, false);
        floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(40f, 1f, 40f);

        _surface = _level.AddComponent<NavMeshSurface>();
    }

    [TearDown]
    public void TearDown()
    {
        _surface.RemoveData();
        Object.DestroyImmediate(_level);
    }

    private GameObject AddWall(float height, float yaw = 0f)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.SetParent(_level.transform, false);
        wall.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        wall.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        wall.transform.localScale = new Vector3(6f, height, 0.5f);
        return wall;
    }

    private List<CoverCandidate> Collect()
    {
        Physics.SyncTransforms();
        _surface.BuildNavMesh();
        var results = new List<CoverCandidate>();
        CoverBakerEngine.Collect(_level, _settings, results);
        return results;
    }

    [Test]
    public void HighWall_GivesHighCoverOnBothSides()
    {
        AddWall(3f);

        var results = Collect();

        Assert.IsNotEmpty(results);
        bool front = false;
        bool back = false;
        foreach (var candidate in results)
        {
            Assert.AreEqual(CoverHeight.High, candidate.Height);
            Assert.AreEqual(1f, Mathf.Abs(candidate.Normal.z), 0.05f, "Faces away from the long wall.");
            Assert.That(candidate.Quality, Is.InRange(0f, 1f));
            front |= candidate.Normal.z > 0f;
            back |= candidate.Normal.z < 0f;
        }
        Assert.IsTrue(front && back);
    }

    [Test]
    public void HighWall_PointsStandJustOutsideTheWall()
    {
        AddWall(3f);

        foreach (var candidate in Collect())
        {
            float distanceFromCentre = Mathf.Abs(candidate.Position.z - Origin.z);
            if (Mathf.Abs(candidate.Normal.z) > 0.9f)
                Assert.That(distanceFromCentre, Is.InRange(0.6f, 0.95f));
        }
    }

    [Test]
    public void WallEnds_GetSidePeeksThatTheRuntimeWouldAccept()
    {
        AddWall(3f);

        var results = Collect();

        int peekers = 0;
        foreach (var candidate in results)
        {
            Vector3 right = Vector3.Cross(Vector3.up, candidate.Normal);
            AssertPeek(candidate, right, candidate.RightOffset, CoverPeekDirection.Right, ref peekers);
            AssertPeek(candidate, -right, candidate.LeftOffset, CoverPeekDirection.Left, ref peekers);
        }
        Assert.Greater(peekers, 0, "Points near the ends of the wall need a side peek.");
    }

    private void AssertPeek(CoverCandidate candidate, Vector3 side, float offset, CoverPeekDirection flag, ref int peekers)
    {
        if (offset <= 0f)
        {
            Assert.AreEqual(CoverPeekDirection.None, candidate.Peeks & flag, "No offset means no flag.");
            return;
        }

        peekers++;
        Assert.AreNotEqual(CoverPeekDirection.None, candidate.Peeks & flag);
        Assert.LessOrEqual(offset, _settings.MaxPeekDistance + 0.001f);
        Assert.IsTrue(NavMesh.SamplePosition(candidate.Position + side * offset, out _, _settings.MaxNavigationSnap, NavMesh.AllAreas));
        Assert.Greater(Mathf.Abs(candidate.Position.x - Origin.x), 2f, "Only points near a wall end can peek around it.");
    }

    [Test]
    public void LowWall_IsLowCoverWithAnOverPeek()
    {
        AddWall(1.4f);

        var results = Collect();

        Assert.IsNotEmpty(results);
        foreach (var candidate in results)
        {
            Assert.AreEqual(CoverHeight.Low, candidate.Height);
            Assert.AreNotEqual(CoverPeekDirection.None, candidate.Peeks & CoverPeekDirection.Over);
        }
    }

    [Test]
    public void WallBelowLowCoverHeight_IsNotCover()
    {
        AddWall(0.6f);

        Assert.IsEmpty(Collect());
    }

    [Test]
    public void RotatedWall_GivesPointsFacingTheRotatedSurfaces()
    {
        var wall = AddWall(3f, 45f);

        var results = Collect();

        Assert.IsNotEmpty(results);
        Vector3 along = wall.transform.right;
        Vector3 across = wall.transform.forward;
        foreach (var candidate in results)
        {
            float aligned = Mathf.Max(Mathf.Abs(Vector3.Dot(candidate.Normal, along)), Mathf.Abs(Vector3.Dot(candidate.Normal, across)));
            Assert.Greater(aligned, 0.9f, "Normals follow the rotated wall, not the world axes.");
        }
    }

    [Test]
    public void Candidates_RespectTheMinimumSpacing()
    {
        AddWall(3f);
        _settings.MinSpacing = 1.2f;

        var results = Collect();

        for (int i = 0; i < results.Count; i++)
        {
            for (int j = i + 1; j < results.Count; j++)
                Assert.GreaterOrEqual(Vector3.Distance(results[i].Position, results[j].Position), 1.2f - 0.001f);
        }
    }

    [Test]
    public void Bake_ReplacesEarlierPointsInsteadOfStackingThem()
    {
        AddWall(3f);
        Physics.SyncTransforms();
        _surface.BuildNavMesh();

        int first = CoverBakerEngine.Bake(_level, _settings);
        int second = CoverBakerEngine.Bake(_level, _settings);

        Assert.Greater(first, 0);
        Assert.AreEqual(first, second);
        Assert.AreEqual(second, _level.GetComponentsInChildren<CoverPoint>().Length);
    }

    [Test]
    public void BakedPoints_ReportTheirSidePeeks()
    {
        AddWall(3f);
        Physics.SyncTransforms();
        _surface.BuildNavMesh();

        CoverBakerEngine.Bake(_level, _settings);

        bool anySidePeek = false;
        foreach (var point in _level.GetComponentsInChildren<CoverPoint>())
        {
            anySidePeek |= (point.PeekDirections & (CoverPeekDirection.Left | CoverPeekDirection.Right)) != 0;
            Assert.That(point.Quality, Is.InRange(0f, 1f));
        }
        Assert.IsTrue(anySidePeek, "Baked points must carry peek offsets, not just peek flags.");
    }

    [Test]
    public void Validator_AcceptsAFreshBake()
    {
        AddWall(3f);
        Physics.SyncTransforms();
        _surface.BuildNavMesh();
        CoverBakerEngine.Bake(_level, _settings);

        var issues = CoverValidator.Validate(_level, _settings);

        Assert.IsFalse(issues.Exists(i => i.Severity == CoverIssueSeverity.Error), Describe(issues));
    }

    [Test]
    public void Validator_FlagsAPointMovedOffTheNavMesh()
    {
        AddWall(3f);
        Physics.SyncTransforms();
        _surface.BuildNavMesh();
        CoverBakerEngine.Bake(_level, _settings);
        var point = _level.GetComponentInChildren<CoverPoint>();
        point.transform.position += Vector3.up * 5f;

        var issues = CoverValidator.Validate(_level, _settings);

        Assert.IsTrue(issues.Exists(i => i.Point == point && i.Message.Contains("NavMesh")));
    }

    [Test]
    public void Validator_FlagsAPeekWithoutAnOffset()
    {
        AddWall(3f);
        Physics.SyncTransforms();
        _surface.BuildNavMesh();
        CoverBakerEngine.Bake(_level, _settings);

        CoverPoint victim = null;
        foreach (var point in _level.GetComponentsInChildren<CoverPoint>())
        {
            if ((point.PeekDirections & CoverPeekDirection.Right) != 0)
                victim = point;
        }
        Assert.IsNotNull(victim, "The bake should have produced a right peek.");

        var serialized = new SerializedObject(victim);
        serialized.FindProperty("_rightLocalOffset").floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var issues = CoverValidator.Validate(_level, _settings);

        Assert.IsTrue(issues.Exists(i => i.Point == victim && i.Message.Contains("peek")));
    }

    private static string Describe(List<CoverIssue> issues)
    {
        var lines = new List<string>();
        foreach (var issue in issues)
            lines.Add($"{issue.Severity} {issue.Point.name}: {issue.Message}");
        return string.Join("\n", lines);
    }
}
}
