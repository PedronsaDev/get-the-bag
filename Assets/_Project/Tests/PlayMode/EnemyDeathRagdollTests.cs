#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.AI.Perception;
using Project.Combat.Core;
using Project.Combat.Health;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Project.AI.Tests
{
public class EnemyDeathRagdollTests
{
    [UnityTest]
    public IEnumerator Death_SwitchesEnemyAToRagdoll() => CheckRagdoll("AI_Test_Enemy_A");

    [UnityTest]
    public IEnumerator Death_SwitchesEnemyBToRagdoll() => CheckRagdoll("AI_Test_Enemy_B");

    private static IEnumerator CheckRagdoll(string prefabName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Enemy/{prefabName}.prefab");
        // Plain instantiate: no NetworkManager is running, so bypass PurrNet's spawning proxy.
        var enemy = (GameObject)PurrNet.UnityProxy.InstantiateDirectly(prefab);
        yield return null; // Ragdoll deactivates itself in Start

        var animator = enemy.GetComponentInChildren<Animator>();
        var bodies = enemy.GetComponentsInChildren<Rigidbody>();
        Assert.Greater(bodies.Length, 0);
        Assert.IsTrue(animator.enabled);
        Assert.IsTrue(bodies[0].isKinematic, "Bodies must start kinematic.");

        var corpse = enemy.GetComponent<AICorpse>();
        Assert.IsFalse(corpse.enabled, "A living enemy is not a body.");
        Assert.IsFalse(AICorpse.All.Contains(corpse));

        RaiseDeath(enemy.GetComponentInChildren<HealthComponent>());

        Assert.IsTrue(AICorpse.All.Contains(corpse), "A dead enemy can be discovered.");

        Assert.IsFalse(animator.enabled);
        foreach (var body in bodies)
            Assert.IsFalse(body.isKinematic);
        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent)
            Assert.IsFalse(agent.enabled);

        Object.Destroy(enemy);
    }

    // HealthComponent raises OnDeath through a network RPC; invoke the event directly.
    private static void RaiseDeath(HealthComponent health)
    {
        var field = typeof(HealthComponent).GetField("OnDeath", BindingFlags.Instance | BindingFlags.NonPublic);
        var handlers = (System.Action<DamageResult>)field.GetValue(health);
        handlers?.Invoke(default);
    }
}
}
#endif
