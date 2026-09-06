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

---

## 2. Unity 6 Modern APIs & Deprecations
- **Object Finding**: Do NOT use `FindFirstObjectByType<T>()`. Always use `FindAnyObjectByType<T>()`.
- **Development Build Checks**: Do NOT use `#if DEVELOPMENT_BUILD` preprocessor directives. Use `Debug.isDebugBuild` or `!Application.isEditor && !Debug.isDebugBuild`.

---

## 3. Editor Tooling & In-Editor Debugging
- **UI Framework**: For all Unity Editor tooling, custom inspectors, editor windows, and in-editor debugging panels, ALWAYS use **UI Toolkit** (`UnityEngine.UIElements`, `UnityEditor.UIElements`, `.uxml`, `.uss`, `VisualElement`).
- Do NOT use legacy IMGUI (`OnGUI`, `GUILayout`, `EditorGUILayout`) for editor tools.

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
