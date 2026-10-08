# Health and damage

Every character uses the same health model. Ordinary damage removes shield first,
then health. Hitboxes can adjust the amount for a head or limb hit. There are no
elemental damage types, health categories, type resistances, or true-damage bypass.
Explosions use the same ordinary damage model.

## Authority

Only a spawned server component can change health. `HealthComponent` exposes:

* `TakeDamage(DamagePayload)`: returns the damage actually applied.
* `Heal(amount)`: restores living health up to its maximum.
* `RechargeShield(amount)`: restores living shield up to its maximum.
* `Revive(healthAmount)`: restores a zero-health character; the caller must also
  complete any player downed-state transition.
* `SetInvulnerable(value)`: updates replicated invulnerability.

These methods reject client and unspawned calls. Invalid, negative, zero, and
nonfinite amounts do not change state. A client requests a gameplay action such
as firing, using an item, or reviving a teammate. That action's server handler
validates the actor and conditions before calling health methods. There is no
health RPC accepting arbitrary damage, healing, revival, or invulnerability.

`CurrentHealth`, `CurrentShield`, and `Invulnerable` come from server-owned
SyncVars. `IsDead` is derived from zero health; no separate death flag can drift
out of sync. Healing and shield recharge cannot revive a zero-health character.
Shield regeneration runs on the server after the configured damage delay.

## Events and player life

One observer notification emits `OnDamaged`, `OnShieldBroken` when a hit exhausts
shield, and `OnDeath` for a fatal hit. It runs once on the server and on observing
clients. Persistent health/shield values replicate separately; presentation that
needs the exact hit snapshot should read `DamageResult` rather than assume a
SyncVar update has already arrived. Results cap effective loss at the available
health and shield, so overkill does not inflate applied damage.

`PlayerDownedState` owns standing/downed/true-death decisions. Zero health can mean
a downed player who is eligible for teammate revival; `PlayerDownedState.IsDead`
means true death, while `HealthComponent.IsDead` means zero health. Use the player
status component for player eligibility. Teammate revive/drag requests resolve the
actual sender, require a living teammate, and validate body distance on the server.
Gameplay revival requires a downed target and cannot be used to revive yourself
or a player who has already bled out. Hold-duration enforcement is still owned by
the existing interaction system; this pass does not add a new server hold timer.

Server administration methods are `ReviveServer`, `DownServer`, `KillServer`, and
`SetBleedOutTimerServer`. The health console's modifying commands now run on the
host/server; `hp_status` remains readable on clients. `hp_true_damage` is removed.

## Props and migration

Destructible props accept server damage only. Explosion damage is grouped by target
so a character receives one hit regardless of its number of colliders. Rigidbody
impulses are also applied once per body. Interaction triggers are excluded; explicit
damageable trigger hitboxes remain valid.

Existing health, shield, regeneration, and initial invulnerability fields retain
their serialized names. Removed `_healthType` and `_armorRating` values are ignored
by Unity and disappear when their assets are next serialized. No prefab or scene
GUIDs change. There is no generic offline fallback: standalone tests must start a
server session before applying health changes.
