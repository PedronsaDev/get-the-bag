# Weapon implementation roadmap

Equipment initialization, slot switching and local visual loading are implemented.
The controller applies synchronized state to the presenter on clients; a per-spawn
cancellation lifetime ends on network despawn. PresentAsync owns each pending load locally,
rejects stale results, retains the previous visual on failure, and transfers the handle only
when its replacement is ready. ClearVisuals cancels requests and releases active resources;
ReleaseActiveVisual performs replacement cleanup without canceling the new request.
Fire, reload, world transfer and animation hooks remain TODOs for implementation.
The original prototype is preserved in ignored `.codex/weapon-skeleton-backup/`.

## Ownership

| Type | Responsibility |
| --- | --- |
| WeaponDefinition / WeaponRegistry | Shared configuration and stable ID lookup |
| WeaponItemState / WeaponEquipmentState | Plain transferable data; no Unity objects |
| RuntimeWeapon | Individual server-owned ammunition/state |
| PlayerWeaponController | Owner requests, server validation, synchronized equipment |
| PlayerWeaponPresenter | Per-peer model loading, sockets, effects, cleanup |
| WeaponInstance | Visual prefab references: muzzle, grips, animator |
| WorldWeapon / WeaponSpawnService | Independently networked dropped/pickup weapons |

Combat owns data/world contracts. Player depends on Combat; Combat must not reference Player.
Keep the existing script metadata/GUIDs. No assembly changes are needed for this scaffold.

## Implement in order

1. **Authoritative equipment:** validate registry IDs and definitions; initialize both default
   slots on the server; add synchronized state; subscribe/apply it on every peer. Owner-only
   RequestEquip sends its RPC. Decide empty-slot behavior. Keep ammo when switching.
   Test host AND remote player, invalid slots and a reconnect/join in the lobby.
2. **Presentation:** implement ConfigureView, PresentAsync and ClearVisuals. One local
   equipped model per player/peer. Despawn cancels work even without GameObject destruction.
   Give each asynchronous request its own acquired-handle ownership, reject stale results,
   and clean them up. Ammo changes should not recreate models. Test rapid switches, load
   failure, disconnect during loading and repeated cleanup. Add Try/Catch around async calls.
3. **Arms and third-person:** configure sockets/visibility and compatible animation clips.
   Initially reuse the weapon mesh. Later separate first/third-person asset references without
   losing existing serialized references. IK adjusts grips; authored animations handle reload.
4. **Fire/reload:** implement ammo operations, fire interval/range config and server timers.
   Authority validates actions/damage; presentation handles effects. Define reload interruptions
   and reserve-ammo ownership first. Test empty ammo, repeated reload, switching during reload,
   downed state and UI input blocking. Cosmetic animation events never grant ammo/damage.
5. **Pickup/drop:** implement WorldWeapon synchronized item state and world-spawn overload.
   Player interaction validates sender/range, claims once and transfers atomically. Preserve
   magazine ammo; rollback failed transfers/spawns. Test simultaneous pickup by two players.

## Asset wiring after logic

- Add PlayerWeaponController and PlayerWeaponPresenter to the player prefab; assign sockets,
  input/facade and optional defaults. Presenter is local, not a separate network object.
- Add WeaponInstance to each equipped prefab and assign muzzle/grip references.
- Include the rifle definition in WeaponRegistry. Reject duplicate or empty authored IDs.
- For dropped objects, create world prefabs with WorldWeapon and required PurrNet components,
  then register those prefabs with PurrNet's Addressables provider. An Addressable entry alone
  does not establish network registration.
- Do not network-spawn first-person arms or camera visuals. Dedicated server needs gameplay
  state/world objects, but no first-person presentation.

## Deliberately deferred

Random parts, persistence integration, shot prediction/reconciliation, advanced recoil,
asset caching/preloading and detailed IK. Decide their policies after basic equipment works.
The existing WeaponSpawnService remains a prototype: its local Initialize call does not
replicate a definition or ammo to clients. Its current SpawnAsync is not used by the scaffold.
