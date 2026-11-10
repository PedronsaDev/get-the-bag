using NUnit.Framework;
using Project.AI.Blackboard;
using Project.AI.Configuration;
using Project.AI.Decision;
using Project.AI.Decision.States;
using Project.AI.Locomotion;
using Project.AI.Patrol;
using Project.AI.Perception;
using UnityEditor;
using UnityEngine;

namespace Project.AI.Tests
{
public class AIPatrolTests
{
    private const string CopAssetPath = "Assets/_Project/Data/AI/Archetypes/ARCH_Cop_A.asset";

    private GameObject _routeObject;
    private GameObject _guardObject;
    private AIPatrolRoute _route;
    private AIBlackboard _blackboard;

    [SetUp]
    public void SetUp()
    {
        _routeObject = new GameObject("Route");
        _route = _routeObject.AddComponent<AIPatrolRoute>();
        AddPoint("A", new Vector3(0f, 0f, 10f));
        AddPoint("B", new Vector3(10f, 0f, 10f));
        AddPoint("C", new Vector3(10f, 0f, 0f));

        _guardObject = new GameObject("Guard");
        _blackboard = _guardObject.AddComponent<AIBlackboard>();
        var serialized = new SerializedObject(_blackboard);
        serialized.FindProperty("_archetypeConfig").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<AIArchetypeConfig>(CopAssetPath);
        serialized.FindProperty("_patrolRoute").objectReferenceValue = _route;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.IsTrue(_blackboard.TryInitializeArchetype());
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_routeObject);
        Object.DestroyImmediate(_guardObject);
    }

    private void AddPoint(string pointName, Vector3 position)
    {
        var point = new GameObject(pointName).transform;
        point.SetParent(_routeObject.transform);
        point.position = position;
    }

    [Test]
    public void NextIndex_LoopWrapsAround()
    {
        int direction = 1;

        Assert.AreEqual(1, AIPatrolRoute.NextIndex(0, 3, true, ref direction));
        Assert.AreEqual(0, AIPatrolRoute.NextIndex(2, 3, true, ref direction));
    }

    [Test]
    public void NextIndex_PingPongTurnsAtBothEnds()
    {
        int direction = 1;
        int index = 0;
        var visited = new System.Collections.Generic.List<int>();

        for (int i = 0; i < 6; i++)
        {
            index = AIPatrolRoute.NextIndex(index, 3, false, ref direction);
            visited.Add(index);
        }

        CollectionAssert.AreEqual(new[] { 1, 2, 1, 0, 1, 2 }, visited);
    }

    [Test]
    public void NextIndex_SinglePointStaysPut()
    {
        int direction = 1;

        Assert.AreEqual(0, AIPatrolRoute.NextIndex(0, 1, false, ref direction));
    }

    [Test]
    public void Selection_PatrolsOnlyWhenUnawareGuardHasRoute()
    {
        Assert.AreEqual(AIStateKind.Patrol, AIStateSelection.Select(AIRole.Guard, AlertState.Unaware, true));
        Assert.AreEqual(AIStateKind.Patrol, AIStateSelection.Select(AIRole.Police, AlertState.Unaware, true));
        Assert.AreEqual(AIStateKind.Idle, AIStateSelection.Select(AIRole.Guard, AlertState.Unaware, false));
        Assert.AreEqual(AIStateKind.Idle, AIStateSelection.Select(AIRole.Civilian, AlertState.Unaware, true));
        Assert.AreEqual(AIStateKind.Combat, AIStateSelection.Select(AIRole.Guard, AlertState.Alerted, true));
    }

    [Test]
    public void Patrol_StartsAtNearestPoint()
    {
        _guardObject.transform.position = new Vector3(9f, 0f, 1f);
        var state = new AIPatrolState();

        state.OnEnter(_blackboard);

        Assert.IsTrue(_blackboard.HasMoveDestination);
        Assert.AreEqual(new Vector3(10f, 0f, 0f), _blackboard.MoveDestination);
        Assert.AreEqual(_blackboard.ArchetypeConfig.MovementConfig.PatrolSpeed, _blackboard.DesiredSpeed);
    }

    [Test]
    public void Patrol_WaitsAtPointThenWalksToNext()
    {
        _guardObject.transform.position = new Vector3(0f, 0f, 10f);
        var state = new AIPatrolState();
        state.OnEnter(_blackboard);
        Assert.AreEqual(new Vector3(0f, 0f, 10f), _blackboard.MoveDestination);

        Arrive();
        state.OnUpdate(_blackboard, 0.1f);
        Assert.IsFalse(_blackboard.HasMoveDestination, "Standing at the point.");

        state.OnUpdate(_blackboard, _route.WaitSeconds + 0.1f);
        Assert.AreEqual(new Vector3(10f, 0f, 10f), _blackboard.MoveDestination);
    }

    [Test]
    public void Patrol_SkipsAnUnreachablePoint()
    {
        _guardObject.transform.position = new Vector3(0f, 0f, 10f);
        var state = new AIPatrolState();
        state.OnEnter(_blackboard);

        _blackboard.ReportMoveResult(_blackboard.MoveRequestVersion, MovementStatus.Failed);
        state.OnUpdate(_blackboard, 0.1f);

        Assert.AreEqual(new Vector3(10f, 0f, 10f), _blackboard.MoveDestination);
    }

    [Test]
    public void Patrol_ExitClearsMovement()
    {
        var state = new AIPatrolState();
        state.OnEnter(_blackboard);

        state.OnExit(_blackboard);

        Assert.IsFalse(_blackboard.HasMoveDestination);
    }

    [Test]
    public void Corpse_IsDiscoveredOnlyOnce()
    {
        var bodyObject = new GameObject("Body");
        bodyObject.SetActive(false);
        var corpse = bodyObject.AddComponent<AICorpse>();
        corpse.enabled = false;
        bodyObject.SetActive(true);

        // Edit Mode does not call OnEnable, so only the discovery rule is checked here.
        Assert.IsTrue(corpse.TryDiscover());
        Assert.IsFalse(corpse.TryDiscover());
        Assert.IsTrue(corpse.Discovered);
        Object.DestroyImmediate(bodyObject);
    }

    private void Arrive()
    {
        _blackboard.ReportMoveResult(_blackboard.MoveRequestVersion, MovementStatus.Arrived);
    }
}
}
