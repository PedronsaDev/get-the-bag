# ADR 002: Assembly Definition Setup for KinematicCharacterController

## Status
Accepted

## Context
`Assets/Plugins/KinematicCharacterController` was imported into the project without `.asmdef` definitions. 
1. If our game scripts use Assembly Definitions, they cannot reference code in `Assembly-CSharp.dll` (which is where KCC would compile by default).
2. KCC includes an `Editor/` subfolder containing `KinematicCharacterMotorEditor.cs` and `ReadOnlyPropertyDrawer.cs`, both of which use `UnityEditor`. If editor scripts share an assembly with runtime scripts, standalone game builds will fail with missing namespace errors.

## Decision
1. **Runtime Assembly**: Created `Assets/Plugins/KinematicCharacterController/KinematicCharacterController.asmdef` with empty `references: []` for all platforms.
2. **Editor Assembly**: Created `Assets/Plugins/KinematicCharacterController/Core/Editor/KinematicCharacterController.Editor.asmdef` with:
   - `includePlatforms: ["Editor"]` (compiles exclusively inside the Unity Editor).
   - `references: ["KinematicCharacterController"]` (references the runtime motor to draw scene gizmos and property drawers).

## Consequences & Lessons
- ✅ Standalone game builds will not suffer `CS0246: The type or namespace name 'UnityEditor' could not be found`.
- ✅ Our upcoming player scripts under `Assets/_Project/Scripts/Player` can reference `KinematicCharacterController` cleanly.
- 💡 **Key Takeaway**: Whenever creating an `.asmdef` for a plugin or feature that contains an `Editor/` subfolder, always isolate the `Editor/` folder into its own Editor-only `.asmdef`.
