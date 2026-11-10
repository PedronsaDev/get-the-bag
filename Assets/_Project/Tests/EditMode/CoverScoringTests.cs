using NUnit.Framework;
using Project.AI.Cover;
using UnityEngine;

namespace Project.AI.Tests
{
public class CoverScoringTests
{
    private static readonly Vector3 Origin = new(3000f, 0f, 3000f);

    private GameObject _root;

    [SetUp]
    public void SetUp() => _root = new GameObject("Scoring Test Root");

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_root);

    private static float Utility(AICoverScoringConfig config, float travel = 5f, float facing = 1f, bool peek = true,
        float quality = 0.5f) =>
        CoverScoringEngine.Utility(travel, facing, peek, 12f, 12f, quality, config);

    [Test]
    public void Utility_PrefersShorterRuns()
    {
        var config = new AICoverScoringConfig();

        Assert.Greater(Utility(config, travel: 3f), Utility(config, travel: 12f));
    }

    [Test]
    public void Utility_PrefersCoverThatCanPeek()
    {
        var config = new AICoverScoringConfig();

        Assert.Greater(Utility(config, peek: true), Utility(config, peek: false));
    }

    [Test]
    public void Utility_PrefersHigherQuality()
    {
        var config = new AICoverScoringConfig();

        Assert.Greater(Utility(config, quality: 0.9f), Utility(config, quality: 0.2f));
    }

    [Test]
    public void Utility_IgnoresQualityWhenInfluenceIsZero()
    {
        var config = new AICoverScoringConfig { QualityInfluence = 0f };

        Assert.AreEqual(Utility(config, quality: 0.9f), Utility(config, quality: 0.1f), 0.0001f);
    }

    [Test]
    public void Utility_StaysPositiveForARunBeyondTheSearchRadius()
    {
        var config = new AICoverScoringConfig();

        Assert.GreaterOrEqual(Utility(config, travel: config.MaxSearchRadius * 3f), 0f);
    }

    private CoverPoint AddPoint()
    {
        var go = new GameObject("Point");
        go.transform.SetParent(_root.transform);
        go.transform.position = Origin;
        go.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        return go.AddComponent<CoverPoint>();
    }

    private void AddWall(float centreX, float width)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.SetParent(_root.transform);
        wall.transform.position = Origin + new Vector3(centreX, 1.5f, -1f);
        wall.transform.localScale = new Vector3(width, 3f, 0.5f);
        Physics.SyncTransforms();
    }

    [Test]
    public void Occlusion_WallBetweenPointAndThreatProtects()
    {
        var point = AddPoint();
        AddWall(0f, 6f);

        Assert.IsTrue(CoverScoringEngine.IsThreatOccluded(point, Origin + new Vector3(0f, 0f, -8f), ~0));
    }

    [Test]
    public void Occlusion_ThreatToTheSideSeesThePoint()
    {
        var point = AddPoint();
        AddWall(0f, 6f);

        Assert.IsFalse(CoverScoringEngine.IsThreatOccluded(point, Origin + new Vector3(12f, 0f, 0f), ~0));
    }

    [Test]
    public void Occlusion_NeedsMostOfTheBodyHidden()
    {
        var point = AddPoint();
        AddWall(0f, 0.4f);

        // A post hides the centre but not the shoulders.
        Assert.IsFalse(CoverScoringEngine.IsThreatOccluded(point, Origin + new Vector3(0f, 0f, -6f), ~0));
    }
}
}
