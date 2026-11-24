using Project.AI.Perception;
using Project.Core.Raid;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.AI.Alarm
{
[DisallowMultipleComponent]
public class AlarmDirector : NetworkBehaviour
{
    // The perception bus treats intensity as a radius.
    private const float LevelWideRadius = 10000f;

    public static AlarmDirector Instance { get; private set; }

    [SerializeField, Tooltip("A guard that finds a body raises the alarm. Turn off to let guards only investigate it.")]
    private bool _bodyFoundRaisesAlarm = true;

    private readonly SyncVar<AlarmState> _syncedState = new(AlarmState.Stealth);
    private readonly SyncVar<int> _syncedCivilianKills = new(0);
    private readonly SyncVar<byte> _syncedReason = new(ThreatStatus.NoAlarmReason);
    private readonly AlarmStateMachine _machine = new();

    public AlarmState State => _syncedState.value;
    public int CivilianKills => _syncedCivilianKills.value;
    public byte ReasonByte => _syncedReason.value;
    public bool BodyFoundRaisesAlarm => _bodyFoundRaisesAlarm;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            AppLog.LogWarning(LogChannel.AI, "More than one AlarmDirector found. Destroying duplicate.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _machine.Raised += HandleRaised;
    }

    protected override void OnDestroy()
    {
        _machine.Raised -= HandleRaised;
        if (Instance == this)
            Instance = null;
        base.OnDestroy();
    }

    protected override void OnSpawned()
    {
        if (isServer)
            ResetAlarm();
    }

    private void Update()
    {
        if (isServer)
            FollowRaid();
    }

    public bool Raise(AlarmReason reason, Vector3 position)
    {
        if (!isServer)
            return false;
        FollowRaid();
        return _machine.TryRaise(reason, position);
    }

    public void ReportCivilianKilled(Vector3 position)
    {
        if (!isServer)
            return;
        FollowRaid();
        _machine.RecordCivilianKill(position);
        _syncedCivilianKills.value = _machine.CivilianKills;
    }

    public void ResetAlarm()
    {
        if (!isServer)
            return;
        _machine.Reset();
        PublishCleared();
    }

    private void FollowRaid()
    {
        if (_machine.FollowRaid(RaidLifecycle.Phase))
            PublishCleared();
    }

    private void PublishCleared()
    {
        _syncedState.value = AlarmState.Stealth;
        _syncedCivilianKills.value = 0;
        _syncedReason.value = ThreatStatus.NoAlarmReason;
    }

    private void HandleRaised(AlarmReason reason, Vector3 position)
    {
        _syncedState.value = AlarmState.Alarmed;
        _syncedReason.value = (byte)reason;
        AppLog.Log(LogChannel.AI, "Alarm raised: {0} at {1}", reason, position, this);
        AIPerceptionBus.Emit(new StimulusPayload(StimulusType.Tactical, position, null, LevelWideRadius));
    }
}
}
