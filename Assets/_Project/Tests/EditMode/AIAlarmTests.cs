using NUnit.Framework;
using Project.AI.Alarm;
using Project.AI.Configuration;
using Project.AI.Decision;
using Project.AI.Perception;
using UnityEditor;
using UnityEngine;

namespace Project.AI.Tests
{
public class AIAlarmTests
{
    [Test]
    public void Machine_StartsInStealth()
    {
        var machine = new AlarmStateMachine();

        Assert.AreEqual(AlarmState.Stealth, machine.State);
        Assert.IsNull(machine.Reason);
    }

    [Test]
    public void Machine_FirstRaiseWinsAndFiresOnce()
    {
        var machine = new AlarmStateMachine();
        int raised = 0;
        machine.Raised += (_, _) => raised++;

        bool first = machine.TryRaise(AlarmReason.BodyFound, new Vector3(1f, 0f, 2f));
        bool second = machine.TryRaise(AlarmReason.CivilianCall, new Vector3(9f, 0f, 9f));

        Assert.IsTrue(first);
        Assert.IsFalse(second);
        Assert.AreEqual(1, raised);
        Assert.AreEqual(AlarmState.Alarmed, machine.State);
        Assert.AreEqual(AlarmReason.BodyFound, machine.Reason);
        Assert.AreEqual(new Vector3(1f, 0f, 2f), machine.Position);
    }

    [Test]
    public void Machine_ResetAllowsANewAlarm()
    {
        var machine = new AlarmStateMachine();
        machine.TryRaise(AlarmReason.GuardAlerted, Vector3.zero);

        machine.Reset();

        Assert.AreEqual(AlarmState.Stealth, machine.State);
        Assert.IsTrue(machine.TryRaise(AlarmReason.CivilianCall, Vector3.one));
        Assert.AreEqual(AlarmReason.CivilianCall, machine.Reason);
    }

    [Test]
    public void Machine_CivilianKillsAreCountedAndEndStealthOnce()
    {
        var machine = new AlarmStateMachine();

        bool first = machine.RecordCivilianKill(Vector3.zero);
        bool second = machine.RecordCivilianKill(Vector3.one);

        Assert.IsTrue(first, "The first kill ends stealth.");
        Assert.IsFalse(second, "Later kills only add to the count.");
        Assert.AreEqual(2, machine.CivilianKills);
        Assert.AreEqual(AlarmReason.CivilianKilled, machine.Reason);

        machine.Reset();
        Assert.AreEqual(0, machine.CivilianKills);
    }

    [TestCase(AIRole.Civilian, AlertState.Alerted, AIStateKind.Flee)]
    [TestCase(AIRole.Civilian, AlertState.Searching, AIStateKind.Flee)]
    [TestCase(AIRole.Civilian, AlertState.Suspicious, AIStateKind.Idle)]
    [TestCase(AIRole.Pedestrian, AlertState.Alerted, AIStateKind.Flee)]
    [TestCase(AIRole.Guard, AlertState.Alerted, AIStateKind.Combat)]
    [TestCase(AIRole.Guard, AlertState.Searching, AIStateKind.Investigate)]
    public void Selection_ScaredRolesFlee(AIRole role, AlertState alert, AIStateKind expected)
    {
        Assert.AreEqual(expected, AIStateSelection.Select(role, alert));
    }

    [Test]
    public void OnlyCiviliansCallThePolice()
    {
        Assert.IsTrue(AIRole.Civilian.CallsPolice());
        Assert.IsFalse(AIRole.Pedestrian.CallsPolice());
        Assert.IsFalse(AIRole.Guard.CallsPolice());
    }

    [Test]
    public void CivilianArchetype_IsAValidUnarmedRole()
    {
        var civilian = AssetDatabase.LoadAssetAtPath<AIArchetypeConfig>("Assets/_Project/Data/AI/Archetypes/ARCH_Civilian_A.asset");

        Assert.IsNotNull(civilian);
        Assert.AreEqual(AIRole.Civilian, civilian.Role);
        Assert.IsTrue(civilian.TryValidate(out string error), error);
    }
}
}
