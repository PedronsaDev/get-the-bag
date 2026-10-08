using PurrNet;
using UnityEngine;

namespace Project.Combat.Weapons
{
// Independently networked dropped/pickup weapon, distinct from an equipped visual.
// Keep player-specific inventory code in Project.Player; Combat must not depend on Player.
public class WorldWeapon : NetworkBehaviour
{
    [SerializeField] private WeaponInstance _visual;
    private WeaponItemState _item;
    private bool _claimed;

    public WeaponItemState Item => _item;

    public void InitializeServer(WeaponItemState item)
    {
        // TODO: Require server, validate registry ID/ammo, publish synchronized item state.
        // A plain _item assignment does not initialize client copies.
    }

    public bool TryClaimServer(out WeaponItemState item)
    {
        item = default;
        // TODO: Atomically claim once, returning state only after server validation.
        // PlayerWeaponController owns sender/range/slot validation and inventory transfer.
        // Failed transfer must restore availability; successful transfer despawns this object.
        return false;
    }

    protected override void OnSpawned()
    {
        // TODO: Subscribe to synchronized item state and resolve the visual on each peer.
    }

    protected override void OnDespawned()
    {
        // TODO: Unsubscribe and cancel any pending local presentation work.
    }
}
}