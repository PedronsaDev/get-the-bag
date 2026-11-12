using NUnit.Framework;
using Project.AI.Alarm;
using Project.AI.Configuration;
using Project.AI.Decision;
using Project.AI.Perception;
using Project.Core.Raid;
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

    [Test]
    public void Machine_IgnoresEventsUntilTheRaidIsLive()
    {
        var machine = new AlarmStateMachine();
        machine.FollowRaid(RaidPhase.Loading);

        Assert.IsFalse(machine.TryRaise(AlarmReason.GuardAlerted, Vector3.zero));
        Assert.IsFalse(machine.RecordCivilianKill(Vector3.zero));
        Assert.AreEqual(0, machine.CivilianKills);

        machine.FollowRaid(RaidPhase.Live);
        Assert.IsTrue(machine.TryRaise(AlarmReason.GuardAlerted, Vector3.zero));
    }

    [Test]
    public void Machine_AcceptsEventsDuringExtraction()
    {
        var machine = new AlarmStateMachine();
        machine.FollowRaid(RaidPhase.Extraction);

        Assert.IsTrue(machine.TryRaise(AlarmReason.BodyFound, Vector3.zero));
    }

    [Test]
    public void Machine_KeepsItsCountersAfterTheRaidEnds()
    {
        var machine = new AlarmStateMachine();
        machine.TryRaise(AlarmReason.CivilianCall, Vector3.zero);
        machine.RecordCivilianKill(Vector3.zero);

        bool cleared = machine.FollowRaid(RaidPhase.Ended);

        Assert.IsFalse(cleared);
        Assert.AreEqual(AlarmState.Alarmed, machine.State);
        Assert.AreEqual(AlarmReason.CivilianCall, machine.Reason);
        Assert.AreEqual(1, machine.CivilianKills);
    }

    [Test]
    public void Machine_CountsNothingAfterTheRaidEnds()
    {
        var machine = new AlarmStateMachine();
        machine.FollowRaid(RaidPhase.Ended);

        Assert.IsFalse(machine.RecordCivilianKill(Vector3.zero));
        Assert.AreEqual(0, machine.CivilianKills);
        Assert.IsFalse(machine.TryRaise(AlarmReason.GuardAlerted, Vector3.zero));
    }

    [Test]
    public void Machine_ClearsWhenTheNextSceneLoads()
    {
        var machine = new AlarmStateMachine();
        machine.TryRaise(AlarmReason.CivilianCall, Vector3.zero);
        machine.FollowRaid(RaidPhase.Ended);

        bool cleared = machine.FollowRaid(RaidPhase.Live);

        Assert.IsTrue(cleared, "Leaving Ended (for example into the hub) starts a clean alarm.");
        Assert.AreEqual(AlarmState.Stealth, machine.State);
        Assert.IsNull(machine.Reason);
    }

    [Test]
    public void Machine_ClearsWhenANewLevelStartsLoading()
    {
        var machine = new AlarmStateMachine();
        machine.TryRaise(AlarmReason.GuardAlerted, Vector3.zero);

        Assert.IsTrue(machine.FollowRaid(RaidPhase.Loading));
        Assert.AreEqual(AlarmState.Stealth, machine.State);
        Assert.IsFalse(machine.FollowRaid(RaidPhase.Loading), "Nothing left to clear.");
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
