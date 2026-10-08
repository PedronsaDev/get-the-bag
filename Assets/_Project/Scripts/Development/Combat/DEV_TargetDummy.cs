using Project.Combat.Core;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.Development.Combat
{
public class DEV_TargetDummy : NetworkBehaviour, IDamageable
{
    public DamageResult TakeDamage(DamagePayload payload)
    {
        if (!isServer || !float.IsFinite(payload.Amount) || payload.Amount <= 0f) return default;
        AppLog.Log(LogChannel.Combat, $"Dummy took {payload.Amount} damage from {payload.AttackerId}", this);
        return new DamageResult { Applied = true, RawDamage = payload.Amount, EffectiveDamage = payload.Amount };
    }
}
}
