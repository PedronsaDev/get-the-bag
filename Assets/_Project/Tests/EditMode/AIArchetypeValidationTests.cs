using NUnit.Framework;
using Project.AI.Configuration;
using UnityEditor;
using UnityEngine;

namespace Project.AI.Tests
{
public class AIArchetypeValidationTests
{
    private const string ArchetypeFolder = "Assets/_Project/Data/AI/Archetypes";

    [TestCase("ARCH_Cop_A")]
    [TestCase("ARCH_Cop_B")]
    public void AuthoredArchetype_IsValid(string assetName)
    {
        var archetype = AssetDatabase.LoadAssetAtPath<AIArchetypeConfig>($"{ArchetypeFolder}/{assetName}.asset");

        Assert.IsNotNull(archetype, assetName);
        Assert.IsTrue(archetype.TryValidate(out string error), error);
        Assert.IsNull(error);
    }

    [Test]
    public void BlankArchetype_IsRejectedWithReason()
    {
        var blank = ScriptableObject.CreateInstance<AIArchetypeConfig>();

        bool valid = blank.TryValidate(out string error);

        Object.DestroyImmediate(blank);
        Assert.IsFalse(valid);
        Assert.IsNotEmpty(error);
    }
}
}
