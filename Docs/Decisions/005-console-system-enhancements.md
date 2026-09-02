# ADR 005: PedronsaDev Console Modernization & Advanced Features

## Context
The custom developer console (`PedronsaDev.Console`) provides command execution and logging during gameplay. A comprehensive audit revealed several areas needing modernization:
1. The custom inspector used legacy IMGUI, conflicting with Rule 3 in `GEMINI.md`.
2. New commands could be omitted in standalone builds if developers forgot to manually click "Load" in the inspector before building.
3. String parameter splitting (`input.Split(' ')`) broke commands with spaces inside quotes (e.g. `host_scene "Mission 01 - Mine"`).
4. Commands only supported basic primitives without support for `Vector3`, `Vector2`, `Color`, or async `UniTask` / `Task` methods.
5. Every incoming log executed `Instantiate()` and `Clear()` executed `Destroy()`, generating severe GC pressure and Canvas layout recalculations during multiplayer stress tests.
6. Lack of multiplayer authority validation (`HostOnly`), command aliases, and viewport log filtering.

## Decisions

### 1. UI Toolkit Inspector (Phase 1)
Rewrote [`ConsoleCacheEditor.cs`](file:///c:/Projects/unity-projects/proc-weapon-game/Assets/Plugins/PedronsaDev/Console/Scripts/Editor/ConsoleCacheEditor.cs) using **UI Toolkit** (`VisualElement`, `Button`, `PropertyField`), completely eliminating legacy IMGUI.

### 2. Automatic Build Pre-Baking (Phase 1)
Added [`ConsoleBuildPreprocessor.cs`](file:///c:/Projects/unity-projects/proc-weapon-game/Assets/Plugins/PedronsaDev/Console/Scripts/Editor/ConsoleBuildPreprocessor.cs) implementing `IPreprocessBuildWithReport`. Whenever a player build starts, the preprocessor automatically discovers and bakes all `[Command]` methods into `ConsoleCache.asset`.

### 3. Tokenizer & Quoted String Support (Phase 1)
Added `StringUtil.TokenizeInput` which preserves quoted multi-word strings as single arguments. For example:
`host_scene "Mission 01 - Mine" 10` is parsed into `["host_scene", "Mission 01 - Mine", "10"]`.

### 4. Expanded Type System & Native UniTask Execution (Phase 2)
- Added `Vector3`, `Vector2`, `Color` (hex & named colors), and arrays (`int[]`, `string[]`) to `TypeUtil.cs`.
- Integrated `UniTask` via `Console.asmdef` references.
- `ConsoleSystem.ExecuteCommandOnTargets` detects and properly awaits `UniTask`, `Task`, and `IEnumerator` coroutines with error logging.
- Automatically prints return values of non-void command methods (`=> <result>`).

### 5. Command Aliases & Multiplayer Host Authority (Phase 2)
- Added `Aliases` array to `[Command]` attribute: aliases are registered automatically into `ConsoleCache`.
- Added `HostOnly` boolean to `[Command]` attribute and decoupled `Console.IsHostChecker` delegate. In `NetworkCommands.cs`, the host checker delegates to `NetworkManager.main.isServer`, protecting host administrative commands (`host_scene`, `host_kick`) from remote clients.

### 6. Viewport Log Filtering, Search, & Clipboard Copy (Phase 2)
- Added `Console.LogFilterMode` (`All`, `Log`, `Warning`, `Error`, `Custom`) and `OnFilterChanged` event.
- Added `filter <mode>` (alias: `f`) to toggle viewport visibility instantly without clearing logs.
- Added `search <keyword>` (alias: `find`) to query recent log history.
- Added `copy` (alias: `copy_logs`) to copy all console output to the system clipboard.

### 7. UI Object Pooling & Circular Buffer (Phase 1/2)
Overhauled [`UIGenerator.cs`](file:///c:/Projects/unity-projects/proc-weapon-game/Assets/Plugins/PedronsaDev/Console/Scripts/Components/UIGenerator.cs) to introduce:
- UI Object Pool for `LogMessageSetup` instances.
- Configurable maximum log cap (default: 200 items). When the cap is reached, the oldest log instance is recycled to the bottom with updated content without allocating or destroying GameObjects.
- Non-destructive `Clear()`: pooled items are deactivated and queued for reuse.

### 8. Visual Theming Engine & UI Toolkit Preferences Editor (Phase 3)
- Overhauled [`ConsolePreferences.cs`](file:///c:/Projects/unity-projects/proc-weapon-game/Assets/Plugins/PedronsaDev/Console/Scripts/Core/ConsolePreferences.cs) with 5 built-in theme presets:
  - **Default Dark**: Obsidian with cyan commands and emerald highlights.
  - **Matrix**: Deep hacker phosphor green on black.
  - **Cyberpunk**: Neon violet/pink frame with electric cyan commands.
  - **Dracula**: Classic IDE palette with pastel purple, yellow, and red.
  - **Amber**: Vintage CRT terminal amber glow on dark brown.
- Created [`ConsolePreferencesEditor.cs`](file:///c:/Projects/unity-projects/proc-weapon-game/Assets/Plugins/PedronsaDev/Console/Scripts/Editor/ConsolePreferencesEditor.cs) using **UI Toolkit** with 1-click theme switcher buttons, styled section boxes, and live Play Mode updates.
- Added runtime console commands:
  - `set_theme <default|matrix|cyberpunk|dracula|amber>` (alias: `theme`)
  - `set_font_size <size>` (alias: `fontsize`)
  - `set_console_opacity <0.1-1.0>` (alias: `opacity`)
- Linked `OnPreferencesChanged` event to [`UIBackgroundSetter.cs`](file:///c:/Projects/unity-projects/proc-weapon-game/Assets/Plugins/PedronsaDev/Console/Scripts/Components/UIBackgroundSetter.cs) and [`UIFontSetter.cs`](file:///c:/Projects/unity-projects/proc-weapon-game/Assets/Plugins/PedronsaDev/Console/Scripts/Components/UIFontSetter.cs) for instant live theme switching.

### 9. Console Open State Synchronization & Input Gating
- Added `Console.IsOpen` and `Console.OnToggle(bool)` in [`Console.cs`](file:///c:/Projects/unity-projects/proc-weapon-game/Assets/Plugins/PedronsaDev/Console/Scripts/Core/Console.cs).
- Connected `LocalPlayerInputProvider` to `Console.IsOpen`: all gameplay input (WASD movement, mouse look, jump, fire) is automatically suppressed while typing in the console, preventing characters from moving while entering commands.

## Consequences
- **Positive**: Zero GC allocations under steady-state logging, instantaneous `clear`, multi-word string support, modern UI Toolkit tooling, automatic build safety, host authority enforcement, command aliases, viewport filtering, instant visual theming with 5 presets, and automatic gameplay input suppression while typing commands.
