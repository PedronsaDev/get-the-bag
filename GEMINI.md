# Proc-Weapon-Game Development Rules

## 1. PurrNet Networking Guidelines

### Attribute Parameter Syntax
- Constructor arguments in PurrNet attributes MUST use colon (`:`) syntax, not equals (`=`):
  - Correct: `[ObserversRpc(runLocally: false)]`, `[ServerRpc(requireOwnership: false)]`
  - Incorrect: `[ObserversRpc(runLocally = false)]` (causes CS0246)

### ServerRpc Caller Identification & Ownership
- When a client invokes a ServerRpc on an unowned or scene-wide manager, always specify `[ServerRpc(requireOwnership: false)]`.
- Never manually pass the client's own `PlayerID` in the RPC parameters. Instead, append `RPCInfo info = default` as the last parameter; PurrNet's IL weaver automatically injects `info.sender`:
  ```csharp
  [ServerRpc(requireOwnership: false)]
  private void RpcNotifyServer(RPCInfo info = default)
  {
      PlayerID sender = info.sender;
  }
  ```

### Player Collection & NetworkManager Submodules
- `NetworkManager.main` does NOT expose `players` directly. All player tracking is handled by `playerModule`:
  - Player list: `net.playerModule.players` (returns `IReadOnlyList<PlayerID>`)
  - Player count: `net.playerModule.players.Count`
  - Local player ID: `net.localPlayer` (returns `PlayerID`) or `net.playerModule.localPlayerId` (returns `PlayerID?`)

### Transport Connection & Disconnection
- `NetworkManager` does not have a `.Close()` or `.Disconnect(PlayerID)` method.
- **Stopping local host/client**:
  ```csharp
  if (net.isServer) net.StopServer();
  if (net.isClient) net.StopClient();
  ```
- **Kicking/disconnecting a remote player**:
  ```csharp
  if (net.playerModule.TryGetConnection(targetPlayerId, out Connection conn))
  {
      net.rawTransport.CloseConnection(conn);
  }
  ```

### Prefab Asset vs. Runtime Instance in Spawning RPCs
- When spawning networked objects inside a `ServerRpc` (e.g. `Instantiate(_prefab, position, rotation)`):
  - NEVER call components, modify variables, or apply physics to the serialized prefab field (`_prefab`).
  - ALWAYS perform all initialization, component queries, and physics operations on the returned runtime clone instance:
    ```csharp
    // Correct:
    var instance = Instantiate(_prefab, pos, rot);
    NetworkManager.main.Spawn(instance.gameObject);
    instance.Initialize(data);
    if (instance.TryGetComponent(out Rigidbody rb)) rb.linearVelocity = vel;

    // Incorrect (mutates or queries the asset in project files):
    if (_prefab.TryGetComponent(out Rigidbody rb)) rb.AddForce(...);
    ```

### Package Immutability & Extensibility Interfaces
- NEVER modify or replace core PurrNet files inside `Library/PackageCache/dev.purrnet.purrnet` (e.g. `PlayerSpawner.cs`).
- When extending or customizing networked spawning and instantiation, always implement and register PurrNet's supported interfaces rather than altering core classes:
  - Custom spawn points / respawn filtering: Implement `IProvideSpawnPoints` and call `playerSpawner.SetRespawnPointProvider(provider)`.
  - Instantiation hooks: Implement `IProvidePrefabInstantiated` and call `playerSpawner.SetPrefabInstantiatedProvider(provider)`.
  - In the Editor, synchronize `playerSpawner.spawnPoints` via `SerializedObject` without modifying `PlayerSpawner.cs`.

---

## 2. Unity 6 Modern APIs & Deprecations
- **Object Finding**:
  - Do NOT use `FindFirstObjectByType<T>()`. Always use `FindAnyObjectByType<T>()`.
  - Do NOT use deprecated `FindObjectsSortMode` parameter in `FindObjectsByType<T>()`. In Unity 6, use `FindObjectsByType<T>()` or `FindObjectsByType<T>(FindObjectsInactive.Include)`.
- **Development Build Checks**: Do NOT use `#if DEVELOPMENT_BUILD` preprocessor directives. Use `Debug.isDebugBuild` or `!Application.isEditor && !Debug.isDebugBuild`.

### Namespace Shadowing Discipline (UnityEngine.Physics)
- `Project.Core.Physics` shadows `UnityEngine.Physics` inside any `Project.Core.*` namespaces.
- Never write unqualified `Physics.Raycast`, `Physics.OverlapCapsule`, etc. inside `Project.Core.*`.
- Always qualify explicitly as `UnityEngine.Physics` or add an alias at the top of the file:
  ```csharp
  using UnityPhysics = UnityEngine.Physics;
  ```

### Prefab Serialization Renaming Discipline
- When renaming any serialized field (`[SerializeField]`) on a MonoBehaviour that is linked inside active Prefabs (especially `Player.prefab` or UI prefabs):
  - ALWAYS add `[FormerlySerializedAs("oldFieldName")]` (from `UnityEngine.Serialization`).
  - ALWAYS include an auto-healing fallback in `Awake()` (`GetComponent<T>() ?? GetComponentInChildren<T>()`) to guarantee references never remain null if serialization is mismatched.

---

## 3. Editor Tooling & UI Input Discipline
- **UI Framework**: For all Unity Editor tooling, custom inspectors, editor windows, and in-editor debugging panels, ALWAYS use **UI Toolkit** (`UnityEngine.UIElements`, `UnityEditor.UIElements`, `.uxml`, `.uss`, `VisualElement`).
- Do NOT use legacy IMGUI (`OnGUI`, `GUILayout`, `EditorGUILayout`) for editor tools.
- **UI Blocker Action Gating**: Any fullscreen menu, inventory panel, modal, or dialog inheriting from `InputBlockerUI` MUST set `_blocksActions = true`. Never set `_blocksActions = false` as a workaround for UI input. All UI actions must be routed via `UIInput` (or Unity EventSystem) while gameplay actions remain completely blocked.

---

## 4. Weapon Ballistics, Spread & Firing Patterns

### AAA Straight Flight & Parallax Handling
- **No Mid-Air Visual Curving**: Never interpolate or lerp a projectile's visual mesh between weapon barrel offset and camera center in mid-air. When strafing or jumping, visual offset lerping produces an unnatural, floaty "banana bullet" curve.
- **Physical Trajectory**: Physical flight must be 100% straight from `MuzzlePoint` directly to the crosshair raycast impact (`targetPoint`).
- **Point-Blank Inversion Safety**: When shooting directly at feet or into a point-blank wall (`targetForwardReach <= muzzleForwardReach + 0.1f`), always discharge straight along `aimForward` to prevent backwards or inverted trajectory calculations.

### Viewplane Ricochet Normalization
- When bouncing projectiles hit ground surfaces (`hit.normal.y > 0.5f`), do not allow barrel parallax to deflect the rebound sideways into walls.
- Re-align the horizontal velocity component directly along the camera's initial sightline heading (`_shotHeading`), preserving 100% restitution energy and bounce height without horizontal drift.

### Pellet Pattern Spread Scaling (Direct Cone Mapping)
- When scaling geometric pellet patterns (`PelletPatternDefinition`) or multi-pellet dispersion with weapon accuracy:
  - **Avoid Pure Relative Ratios**: Do not rely exclusively on `_spacingDegrees * (currentSpread / baseSpread)`. If `baseSpread` is large or equal to `maxSpread`, the ratio is 1.0 and spread fails to scale.
  - **Direct Cone Mapping**: Ensure the outer pattern boundary expands directly with the weapon's cone spread angle (`coneScale = currentSpread * _spreadMultiplier`).
  - **Dynamic Bloom Expansion**: Support additive expansion for authored patterns (`bloomScale = _spacingDegrees + (extraBloom * _spreadMultiplier)`).

### Gunplay Spread Accumulation & Recovery Discipline
- **Spread Step per Shot**: Firing must expand spread by at least 20–25% of the weapon's spread range (`MaxSpread - BaseSpread`) or a scaled recoil kick, ensuring 4–5 rapid shots reach maximum bloom.
- **Recovery Cooldown**: Never immediately recover spread at $0\,\text{ms}$ after firing. Apply a brief cooldown ($\approx 0.15\text{--}0.18\,\text{s}$) before recovery kicks in so continuous and rapid fire noticeably builds up spread.
- **Locomotion Penalties**: Jumping and sprinting must dynamically apply spread penalties (e.g. $+2.5^\circ$ airborne, $+1.5^\circ$ sprint) to make hipfire accuracy feel reactive and alive.

---

## 5. Channel-Based Subsystem Logging (AppLog)

### Mandatory Logging via AppLog
- Do NOT use raw `Debug.Log`, `Debug.LogWarning`, or `Debug.LogError` for gameplay, networking, match, AI, combat, persistence, or UI diagnostics.
- ALWAYS use `AppLog` from `Project.Logging` with the appropriate `LogChannel`:
  ```csharp
  using Project.Logging;

  AppLog.Log(LogChannel.Match, "Round started with {0} players", playerCount, this);
  AppLog.LogWarning(LogChannel.Network, "Connection latency elevated: {0}ms", ping, this);
  AppLog.LogError(LogChannel.Combat, "Invalid damage calculation payload received", this);
  ```

### Channel Selection Guide
- `LogChannel.Network`: Scene loading, PurrNet RPCs, sync state, connections.
- `LogChannel.Match`: Train transit, raid timer, extraction/infil zones, round states.
- `LogChannel.Combat`: Weapons, ballistics, projectiles, damage, health.
- `LogChannel.Player`: Locomotion, camera, inventory, player state machine.
- `LogChannel.AI`: Locomotion, behavior tree/planner, sensory perception.
- `LogChannel.UI`: Menus, HUD, prompts, canvas blockers.
- `LogChannel.Persistence`: Save/load, profile data, file I/O.
- `LogChannel.Audio`: Sound events, banks, volume bus changes.
- `LogChannel.General`: Miscellaneous engine/utility diagnostics.

### Hierarchy Context & Allocation Discipline
- Always pass `this` or the relevant `UnityEngine.Object context` as the last argument so double-clicking the console entry highlights the GameObject in the Hierarchy.
- In high-frequency methods (`Update`, `FixedUpdate`, or per-frame queries), guard formatting allocations with `AppLog.IsChannelEnabled(channel)` or use the format string overload:
  ```csharp
  if (AppLog.IsChannelEnabled(LogChannel.AI))
  {
      AppLog.Log(LogChannel.AI, "Evaluating path node: {0}", node.Id, this);
  }
  ```

