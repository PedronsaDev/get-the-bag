using Project.Combat.Core;
using Project.Combat.Data;
using PurrNet;
using UnityEngine;

namespace Project.Combat.Weapons
{
public sealed class WeaponHitscan
{
    private readonly RaycastHit[] _hits = new RaycastHit[32];

    public WeaponShot Execute(WeaponDefinition definition, WeaponResolvedStats stats, Transform shooter,
        PlayerID? attacker, Vector3 origin, Vector3 direction)
    {
        var count = Physics.RaycastNonAlloc(origin, direction, _hits, stats.Range,
            definition.HitMask, QueryTriggerInteraction.Collide);
        var hits = _hits;
        if (count == _hits.Length)
        {
            hits = Physics.RaycastAll(origin, direction, stats.Range,
                definition.HitMask, QueryTriggerInteraction.Collide);
            count = hits.Length;
        }

        var nearest = float.PositiveInfinity;
        RaycastHit closest = default;
        IDamageable target = null;
        for (var i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.collider.transform.IsChildOf(shooter) || hit.distance >= nearest) continue;
            var damageable = hit.collider.GetComponent<IDamageable>();
            if (hit.collider.isTrigger && damageable == null) continue;
            damageable ??= hit.collider.GetComponentInParent<IDamageable>();
            nearest = hit.distance;
            closest = hit;
            target = damageable;
        }

        var shot = new WeaponShot
        {
            WeaponId = definition.Id, Origin = origin, Hit = closest.collider,
            EndPoint = closest.collider ? closest.point : origin + direction * stats.Range,
            HitNormal = closest.normal
        };
        target?.TakeDamage(new DamagePayload(stats.Damage, hitPoint: shot.EndPoint,
            hitNormal: shot.HitNormal, impactDirection: direction, attackerId: attacker));
        return shot;
    }
}
}
