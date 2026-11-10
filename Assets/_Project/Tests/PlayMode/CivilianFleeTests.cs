#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using Project.AI.Alarm;
using Project.AI.Blackboard;
using Project.AI.Configuration;
using Project.AI.Decision.States;
using Project.AI.Locomotion;
using UnityEditor;
using UnityEngine;

namespace Project.AI.Tests
{
public class CivilianFleeTests
{
    private GameObject _civilianObject;
    private GameObject _phoneObject;
    private AIBlackboard _blackboard;
    private AIPhonePoint _phone;

    [SetUp]
    public void SetUp()
    {
        _civilianObject = new GameObject("Test Civilian");
        _blackboard = _civilianObject.AddComponent<AIBlackboard>();
        var archetype = AssetDatabase.LoadAssetAtPath<AIArchetypeConfig>(
            "Assets/_Project/Data/AI/Archetypes/ARCH_Civilian_A.asset");
        typeof(AIBlackboard).GetField("_archetypeConfig", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_blackboard, archetype);
        Assert.IsTrue(_blackboard.TryInitializeArchetype());

        _phoneObject = new GameObject("Test Phone");
        _phoneObject.transform.position = new Vector3(8f, 0f, 0f);
        _phone = _phoneObject.AddComponent<AIPhonePoint>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_civilianObject);
        Object.DestroyImmediate(_phoneObject);
    }

    [Test]
    public void Phone_IsReservedByOneCivilianAtATime()
    {
        var first = new GameObject("First").GetEntityId();
        var second = new GameObject("Second").GetEntityId();

        Assert.IsTrue(AIPhonePoint.TryReserveNearest(Vector3.zero, first, out var phone));
        Assert.AreSame(_phone, phone);
        Assert.IsFalse(AIPhonePoint.TryReserveNearest(Vector3.zero, second, out _));

        phone.Release(second);
        Assert.IsFalse(AIPhonePoint.TryReserveNearest(Vector3.zero, second, out _), "Only the owner can release.");

        phone.Release(first);
        Assert.IsTrue(AIPhonePoint.TryReserveNearest(Vector3.zero, second, out _));
    }

    [Test]
    public void Flee_RunsToPhoneThenCallsThenCowers()
    {
        var state = new AIFleeState();

        state.OnEnter(_blackboard);

        Assert.IsTrue(state.IsBusy);
        Assert.AreEqual(_phone.Position, _blackboard.MoveDestination);
        Assert.AreEqual(_blackboard.ArchetypeConfig.MovementConfig.FleeSpeed, _blackboard.DesiredSpeed);

        _blackboard.ReportMoveResult(_blackboard.MoveRequestVersion, MovementStatus.Arrived);
        state.OnUpdate(_blackboard, 0.1f);
        Assert.IsTrue(state.IsBusy, "Calling counts as busy.");

        Assert.IsFalse(_blackboard.WantsToLieDown, "Still standing while calling.");

        state.OnUpdate(_blackboard, _phone.CallSeconds + 0.1f);
        Assert.IsFalse(state.IsBusy, "After the call the civilian cowers.");
        Assert.IsTrue(_blackboard.WantsToLieDown, "A cowering civilian lies on the floor.");

        state.OnExit(_blackboard);
        Assert.IsFalse(_blackboard.WantsToLieDown, "Leaving the state stands the civilian up.");
    }

    [Test]
    public void Flee_ExitFreesThePhone()
    {
        var state = new AIFleeState();
        state.OnEnter(_blackboard);
        Assert.IsFalse(AIPhonePoint.TryReserveNearest(Vector3.zero, new GameObject("Other").GetEntityId(), out _));

        state.OnExit(_blackboard);

        Assert.IsTrue(AIPhonePoint.TryReserveNearest(Vector3.zero, new GameObject("Other 2").GetEntityId(), out _));
    }

    [Test]
    public void Flee_UnreachablePhoneIsGivenUp()
    {
        var state = new AIFleeState();
        state.OnEnter(_blackboard);

        _blackboard.ReportMoveResult(_blackboard.MoveRequestVersion, MovementStatus.Failed);
        state.OnUpdate(_blackboard, 0.1f);

        Assert.IsFalse(state.IsBusy);
        Assert.IsTrue(_blackboard.WantsToLieDown);
        Assert.IsTrue(AIPhonePoint.TryReserveNearest(Vector3.zero, new GameObject("Other").GetEntityId(), out _),
            "The phone is free again.");
    }
}
}
#endif
