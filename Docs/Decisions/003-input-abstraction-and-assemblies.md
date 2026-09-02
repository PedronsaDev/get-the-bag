# ADR 003: Assembly Definitions & Input Abstraction Architecture

## Status
Accepted

## Context
1. To prevent monolithic compilation bottlenecks and circular dependencies, the project requires domain-based assembly definitions (`.asmdef`).
2. In multiplayer co-op games, having player movement or combat poll hardware input devices directly creates race conditions and prevents proper replication on remote peers. Local and remote players must share the same physical motor and combat logic, but receive different input streams.

## Decision
1. **Domain Asmdefs**:
   - `Project.Core.asmdef`: Pure contracts (`IDamageable`, `IPlayerInputProvider`), `PlayerInputData`, and utilities.
   - `Project.Networking.asmdef`: PurrNet NetworkBehaviours, transport management, scene synchronization, and console commands.
   - `Project.Player.asmdef`: Motor execution, KCC integration, camera controls, local/remote input providers.
2. **Decoupled Input Pipeline**:
   - Created `PlayerInputData` unmanaged struct holding all directional vectors and button states.
   - Created `IPlayerInputProvider` contract.
   - Created `LocalPlayerInputProvider`: Reads from Unity's new Input System (`InputSystem_Actions.inputactions`).
   - Created `RemotePlayerInputProvider`: Returns neutral inputs (remote player movement is driven via PurrNet's `NetworkTransform` synchronization).
3. **Action Map Expanded**:
   - Added FPS combat inputs to `InputSystem_Actions.inputactions`: `Aim`, `Reload`, `Grenade`, `Skill`, `Melee`, `Ping`, `WeaponSlot1` through `WeaponSlot4`.

## Consequences
- ✅ The character motor and combat systems will have zero coupling to `UnityEngine.InputSystem` or hardware devices.
- ✅ Remote player instances on your screen can use the exact same motor code as local players without input pollution.
- ✅ Future AI bots can easily implement `IPlayerInputProvider` to control humanoid player rigs.
