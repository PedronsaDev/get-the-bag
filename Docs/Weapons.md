# Player firing and reloading

## Configure a weapon

On each `WeaponDefinition`, set magazine size, starting reserve ammo, damage, reload
duration (seconds), fire rate (rounds per minute), range, hit mask, and fire mode.
Existing definitions default to semi-automatic and 90 reserve rounds until configured.
Burst mode also uses Burst Size and Burst Cooldown (measured after the last shot).
Keep the definition in `WeaponRegistry`; the equipped prefab is still loaded locally
through Addressables by `PlayerWeaponPresenter`.

The current input provider already connects primary fire, reload, and slot selection.
`RequestFire(direction)` represents a trigger tap. `SetFireInput(held, direction)`
represents sustained input. These methods require ownership of a spawned player.
Gameplay input updates aim while the trigger is held; a half-second without updates
stops sustained firing and requires release before restarting.

## Responsibility and rules

* Definitions contain shared configuration, never mutable player state.
* Each server-owned `RuntimeWeapon` owns magazine/reserve ammo, its cadence deadline,
  and its own strategy instance. Switching preserves ammo and cadence.
* `PlayerWeaponController` accepts ownership-checked RPCs, validates the active slot,
  equip revision, living state, finite aim, and action, then ticks firing/reloading
  on the server. Remote clients do not create authoritative runtime weapons.
* Fire strategies schedule shots. `RuntimeWeapon.TryFire` independently enforces
  ammunition and the weapon's maximum RPM, including for custom strategies.
* `WeaponHitscan` resolves the nearest solid collider or damageable trigger, skips
  the shooter hierarchy and non-damageable interaction triggers, and calls the
  existing `IDamageable` API. Walls consume an accepted round and block damage.
* Magazine/reserve/action state is replicated through the existing equipment SyncVar.
  `WeaponItemState`, `WeaponEquipmentState`, and shot messages use PurrNet `IPackedAuto`.
  Equip revision rejects input queued for an older equipped weapon.

Semi-automatic fires once per press. Full automatic fires while held. Burst commits
one burst per press and completes after release; presses during the burst/cooldown
are discarded. Shots are capped at one per server update rather than catching up
with many shots during a stalled frame. Semi-automatic can buffer one press until
the cadence deadline, including a quick tap released before that deadline.

Reload starts only with missing magazine rounds and nonzero reserve. Ammo transfers
when the server timer finishes, bounded by magazine capacity and reserve. Repeated
reload requests and firing during reload are rejected. Switching, downing, death,
and despawn cancel pending reload/burst work; cancellation never spends reserve.
A trigger held across an interruption must be released before firing resumes.
Reserve currently belongs to the individual weapon, not an inventory ammo pool.
`CompleteReloadFromReserve` uses that pool; `TryCompleteReload(available, out consumed)`
is the lower-level transfer primitive for a future inventory-owned pool, whose
caller must deduct `consumed` from that external pool.

## Add a custom mode

Create a concrete `WeaponFireModeDefinition` ScriptableObject in Combat (or an
assembly referencing Definitions). Its `CreateStrategy(WeaponDefinition)` returns
a **new** object implementing `IWeaponFireStrategy` for each weapon. Assign that
asset to Custom Fire Mode on the weapon; it overrides the built-in selector.

The contract is:

* `SetTrigger(bool held)` records trigger input; derive press edges locally.
* `TryScheduleShot(double now)` returns whether one shot should fire at the supplied
  server game time. It is called only when the weapon has ammo and its cadence
  permits a shot. It must not mutate ammo, instantiate visuals, or apply damage.
* `Reset(bool requireRelease)` cancels pending work. When `requireRelease` is true,
  ignore held input until a subsequent release. `TriggerFireStrategy` provides that
  latch and press-edge handling for policies that want to inherit it.

Charging, delayed shots, or unusual patterns can live entirely in this strategy.
Projectile simulation or multi-pellet damage belongs in a shot executor rather than
in the scheduling policy; this first version uses hitscan.

## Presentation

`PlayerWeaponController.ShotAccepted` emits accepted shot data on observing clients
(including the host). Use its origin, endpoint, hit normal, and sequence for tracer
and impact effects. The presenter only animates the matching currently loaded
weapon; ammo updates reuse its Addressables instance instead of restarting a load.

Optional Animator parameters on arms/body/weapon controllers:

* `Fire`: Trigger.
* `Reload`: Trigger.
* `IsReloading`: Bool; transition out when false to support cancellation.

The weapon visual also exposes `Fired` and `ReloadingChanged` events; the presenter
exposes equivalent events. These are cosmetic only. Actual clips, muzzle flashes,
sound, IK, and recoil can be attached without changing authoritative combat rules.

## Current boundaries and verification

The server derives the ray origin from the optional Aim Origin transform, otherwise
the camera pitch pivot or body-height fallback. Clients supply direction, never
damage or a ray origin. For remote crouching/animation, configure a body eye anchor
whose position is updated on the server. Local camera presentation alone is not a
server pose model. This version has no prediction, lag compensation, or synchronized
reload clip seeking; presentation follows confirmed state.

Unity compilation and temporary host Play Mode checks covered all three built-in
modes, cadence, ammo snapshots/depletion, reserve transfer, shield damage, shooter
and trigger filtering, wall obstruction, invalid aim, duplicate requests, reload
completion/cancellation, burst interruption, downed blocking, and heartbeat timeout.
A separate remote client, dedicated server, and player build remain untested.
Health and destructible props now reject client damage calls and expose no generic
damage/heal/revive RPC. See [Health.md](Health.md) for the server-owned damage flow.
The health simplification has also been checked with a separate Multiplayer Play
Mode client; the broader weapon tests above remain the original host-only suite.
