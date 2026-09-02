# PurrNet Networking Playbook

This playbook establishes the networking standards for **proc-weapon-game**. Follow these rules to ensure smooth synchronization, lag-free gameplay, and zero race conditions.

---

## 1. Authority Model: Host Authority as "Single Source of Truth"

In our co-op extraction shooter, the Host operates as the authoritative server (server of 1 in solo play, server of N in co-op).

- **Host Responsible For**:
  - Procedural map seed distribution.
  - Enemy AI decisions and navigation targets.
  - Health pools and lethal damage calculations.
  - Loot roll generation (rarity, stats, affixes) and drop claims.
  - Objective / extraction progression.
- **Client Responsible For**:
  - Immediate local player input response (KCC prediction).
  - Immediate local camera rotation and recoil feel.
  - UI display and local audio/visual presentation.
  - Sending *Intent* to the Host (`[ServerRpc]`).

---

## 2. When to Use What: SyncVars vs. RPCs

| Mechanism | Purpose | When to Use | Example |
| :--- | :--- | :--- | :--- |
| **`[SyncVar]`** | Continuous or persistent state | Whenever late-joining players or reconnecting players need current value | Current Health, Player State (Alive/DBNO), Equipped Weapon Seed, Extraction Timer |
| **`[ServerRpc]`** | Client-to-Host intent | When a client wants to perform an action | RequestFire, RequestPickupItem, RequestRevive |
| **`[ObserversRpc]`** | Host-to-All transient event | One-time visual or audio notifications that don't need persistent state | MuzzleFlashVFX, ImpactDecal, WeaponInspectHypeSound |

---

## 3. Spawning & Despawning

PurrNet simplifies object spawning:
- Spawning is done via standard Unity `Instantiate()` of registered Network Prefabs.
- Despawning is done via `Destroy()`.
- **Rule**: Only the Host should spawn authoritative gameplay objects (enemies, loot drops, extraction consoles).

---

## 4. Ownership Checks

```csharp
if (isOwner)
{
    // Execute local input, local camera control, client-side prediction
}

if (isServer)
{
    // Execute authoritative damage calculations, AI decisions, loot drop rolls
}
```

---

## 5. Persistent User Data (PurrNet Cookies)
PurrNet provides a built-in cookie system for player reconnection:
- If a player's connection temporarily drops and recovers, the Host recognizes their session cookie and restores their player state without losing their inventory or stats.
