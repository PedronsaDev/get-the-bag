using NUnit.Framework;
using Project.AI.Perception;
using Project.Combat.Weapons;
using UnityEngine;

namespace Project.AI.Tests
{
public class PlayerGunfireNoiseTests
{
    private class FakeReceiver : IAISensoryReceiver
    {
        public Vector3 SensoryOrigin { get; set; }
        public int SoundCount;
        public float LastIntensity;

        public void ReceiveStimulus(in StimulusPayload stimulus)
        {
            if (stimulus.Type != StimulusType.Sound)
                return;
            SoundCount++;
            LastIntensity = stimulus.Intensity;
        }
    }

    private GameObject _createdBusObject;
    private AIPerceptionBus _bus;
    private FakeReceiver _receiver;

    [SetUp]
    public void SetUp()
    {
        // Startup services may already provide the scene bus; use it instead of creating a duplicate.
        _bus = AIPerceptionBus.Instance;
        if (!_bus)
        {
            _createdBusObject = new GameObject("Test Perception Bus");
            _bus = _createdBusObject.AddComponent<AIPerceptionBus>();
        }
        _bus.enabled = true;
    }

    [TearDown]
    public void TearDown()
    {
        if (_receiver != null)
            _bus.UnregisterReceiver(_receiver);
        _bus.enabled = true;
        if (_createdBusObject)
            Object.DestroyImmediate(_createdBusObject);
    }

    private FakeReceiver AddReceiver(Vector3 position)
    {
        _receiver = new FakeReceiver { SensoryOrigin = position };
        _bus.RegisterReceiver(_receiver);
        return _receiver;
    }

    [Test]
    public void PlayerShot_ReachesReceiverInsideRadius()
    {
        var near = AddReceiver(new Vector3(20f, 0f, 0f));

        GunfireNoise.Raise(Vector3.zero, null);

        Assert.AreEqual(1, near.SoundCount);
        Assert.Greater(near.LastIntensity, 0f);
    }

    [Test]
    public void PlayerShot_DoesNotReachReceiverOutsideRadius()
    {
        var far = AddReceiver(new Vector3(500f, 0f, 0f));

        GunfireNoise.Raise(Vector3.zero, null);

        Assert.AreEqual(0, far.SoundCount);
    }

    [Test]
    public void DisabledBus_StopsListening()
    {
        var near = AddReceiver(new Vector3(5f, 0f, 0f));
        _bus.enabled = false;

        GunfireNoise.Raise(Vector3.zero, null);

        Assert.AreEqual(0, near.SoundCount);
    }
}
}
