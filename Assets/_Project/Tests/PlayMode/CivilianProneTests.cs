#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Project.AI.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.AI.Tests
{
public class CivilianProneTests
{
    [UnityTest]
    public IEnumerator LyingDown_TurnsTheBodyFlatAndCentresIt()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/AI/AI_PF_Civilian_A.prefab");
        // Plain instantiate: no NetworkManager is running, so bypass PurrNet's spawning proxy.
        var civilian = (GameObject)PurrNet.UnityProxy.InstantiateDirectly(prefab);
        yield return null;

        var prone = civilian.GetComponent<AICivilianProne>();
        Assert.IsNotNull(prone, "The civilian prefab needs an AICivilianProne.");
        var visual = civilian.transform.Find("Visual Root");
        Vector3 standingPosition = visual.localPosition;

        typeof(AICivilianProne).GetMethod("ApplyPose", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(prone, new object[] { 1f });

        Assert.Greater(Vector3.Dot(visual.up, civilian.transform.forward), 0.99f);
        Assert.Greater(Vector3.Dot(visual.forward, Vector3.down), 0.99f, "Face down.");
        Assert.AreEqual(0.2f, visual.localPosition.y - standingPosition.y, 0.001f, "Lifted onto the floor.");
        Assert.AreEqual(-0.9f, visual.localPosition.z - standingPosition.z, 0.001f, "Centred on the spot.");

        Object.Destroy(civilian);
    }
}
}
#endif
