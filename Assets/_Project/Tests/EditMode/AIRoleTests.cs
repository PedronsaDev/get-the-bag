using NUnit.Framework;
using Project.AI.Configuration;
using Project.AI.Decision;
using Project.AI.Perception;
using UnityEditor;
using UnityEngine;

namespace Project.AI.Tests
{
public class AIRoleTests
{
    private const string CopAssetPath = "Assets/_Project/Data/AI/Archetypes/ARCH_Cop_A.asset";

    [TestCase(AIRole.Guard, AlertState.Unaware, AIStateKind.Idle)]
    [TestCase(AIRole.Guard, AlertState.Suspicious, AIStateKind.Investigate)]
    [TestCase(AIRole.Guard, AlertState.Searching, AIStateKind.Investigate)]
    [TestCase(AIRole.Guard, AlertState.Alerted, AIStateKind.Combat)]
    [TestCase(AIRole.Police, AlertState.Alerted, AIStateKind.Combat)]
    [TestCase(AIRole.Civilian, AlertState.Suspicious, AIStateKind.Idle)]
    [TestCase(AIRole.Civilian, AlertState.Alerted, AIStateKind.Flee)]
    [TestCase(AIRole.Pedestrian, AlertState.Searching, AIStateKind.Flee)]
    [TestCase(AIRole.Pedestrian, AlertState.Alerted, AIStateKind.Flee)]
    public void StateSelection_FollowsRole(AIRole role, AlertState alert, AIStateKind expected)
    {
        Assert.AreEqual(expected, AIStateSelection.Select(role, alert));
    }

    [Test]
    public void Guard_IsTheDefaultRoleSoOldAssetsStillFight()
    {
        Assert.AreEqual(0, (int)AIRole.Guard);
        Assert.IsTrue(AIRole.Guard.Fights());
    }

    [Test]
    public void BrokenWeapon_FailsFighters_ButNotCivilians()
    {
        Assert.IsFalse(CopWith(AIRole.Police, rpm: 0f).TryValidate(out _));
        Assert.IsFalse(CopWith(AIRole.Guard, rpm: 0f).TryValidate(out string guardError), guardError);
        Assert.IsTrue(CopWith(AIRole.Civilian, rpm: 0f).TryValidate(out string civilianError), civilianError);
        Assert.IsTrue(CopWith(AIRole.Pedestrian, rpm: 0f).TryValidate(out string pedestrianError), pedestrianError);
    }

    [Test]
    public void BadCombatTuning_FailsFightersOnly()
    {
        var guard = CopWith(AIRole.Guard, rpm: 600f, reflankMin: 3f, reflankMax: 1f);
        var civilian = CopWith(AIRole.Civilian, rpm: 600f, reflankMin: 3f, reflankMax: 1f);

        Assert.IsFalse(guard.TryValidate(out string error));
        StringAssert.Contains("tuning", error);
        Assert.IsTrue(civilian.TryValidate(out _));
    }

    [Test]
    public void DefaultCombatTuning_MatchesFormerHardCodedValues()
    {
        var tuning = new AICombatTuningConfig();

        Assert.AreEqual(0.5f, tuning.CoverCheckInterval);
        Assert.AreEqual(1.5f, tuning.ReflankMinDelay);
        Assert.AreEqual(2.5f, tuning.ReflankMaxDelay);
        Assert.AreEqual(60f, tuning.FlankAngleDegrees);
        Assert.AreEqual(4f, tuning.FlankSampleRadius);
        Assert.AreEqual(4f, tuning.RetreatStepDistance);
        Assert.AreEqual(2f, tuning.RetreatSampleRadius);
    }

    private static AIArchetypeConfig CopWith(AIRole role, float rpm, float reflankMin = 1.5f, float reflankMax = 2.5f)
    {
        var copy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AIArchetypeConfig>(CopAssetPath));
        var serialized = new SerializedObject(copy);
        serialized.FindProperty("_role").enumValueIndex = (int)role;
        serialized.FindProperty("_weaponConfig.RoundsPerMinute").floatValue = rpm;
        serialized.FindProperty("_combatTuning._reflankMinDelay").floatValue = reflankMin;
        serialized.FindProperty("_combatTuning._reflankMaxDelay").floatValue = reflankMax;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return copy;
    }
}
}
