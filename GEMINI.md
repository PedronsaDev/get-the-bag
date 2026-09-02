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
