using Project.Combat.Core;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.Development.Combat
{
public class DEV_TargetDummy : NetworkBehaviour, IDamageable
{
    [Header("Prefab Reference")]
    [SerializeField] private DEV_DamagePopup _damagePopupPrefab;

    [SerializeField] private Transform _targetDummy;
    
    public DamageResult TakeDamage(DamagePayload payload)
    {
        if (!isServer || !float.IsFinite(payload.Amount) || payload.Amount <= 0f) return default;
        AppLog.Log(LogChannel.Combat, $"Dummy took {payload.Amount} damage from {payload.AttackerId}", this);
        DEV_DamagePopup.Create(_targetDummy.position + Vector3.up * 2, Mathf.RoundToInt(payload.Amount), payload.Location, _damagePopupPrefab);
        return new DamageResult { Applied = true, RawDamage = payload.Amount, EffectiveDamage = payload.Amount };
    }
}
}
