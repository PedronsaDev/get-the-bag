# ADR 004: Data Persistence Architecture & Decoupling

## Context
A profile-based save system was integrated into the project featuring file serialization, XOR encryption, profile management, and backup rollback (`.bak`). However:
1. The scripts resided outside assembly definitions in `Assembly-CSharp.dll`, making them inaccessible to `Project.Player` and future modules.
2. The manager had hardcoded, commented-out couplings to specific subsystem managers (e.g. `_characterManager`).
3. Recurring 300s auto-saving during gameplay can cause frame-drops or corrupted state during co-op combat encounters.

## Decisions

### 1. Inversion of Control via `IDataPersistence` Contract
- Defined [`IDataPersistence.cs`](file:///c:/Projects/unity-projects/proc-weapon-game/Assets/_Project/Scripts/Core/Persistence/IDataPersistence.cs) in `Project.Core.Persistence`.
- Any component in any assembly can implement `IDataPersistence` and register with `DataPersistenceManager.Instance.Register(this)`.
- `DataPersistenceManager` never needs to know about specific concrete managers.

### 2. Assembly Separation
- High-level data contracts (`IDataPersistence`, `GameData`, `SerializableDictionary`) reside in `Project.Core`.
- Infrastructure file operations (`FileDataHandler`, `DataPersistenceManager`) reside in `Project.Persistence` (`Project.Persistence.asmdef`).
- Gameplay assemblies depend strictly on `Project.Core`, maintaining strict unidirectional flow.

### 3. Hybrid Data Schema
- Core systems have typed sub-containers on `GameData` (`Player`, `Settings`) for type safety and IDE autocomplete.
- Extensible systems (procedural weapons stash, mission states) use dynamic JSON buckets:
  ```csharp
  data.SetBucket("Weapons", myWeaponStash);
  data.TryGetBucket("Weapons", out myWeaponStash);
  ```

### 4. Removal of Recurring Timer Auto-Saving
- Removed the 300s auto-save coroutine per multiplayer co-op design requirements.
- Saves are triggered on definitive milestones: mission completion, extraction, hub upgrades, and manual console command (`save_game`).

### 5. Lazy Auto-Instantiation Fallback
- `DataPersistenceManager.Instance` automatically spawns a `[DataPersistenceManager]` `DontDestroyOnLoad` GameObject using modern Unity 6 `FindAnyObjectByType<DataPersistenceManager>()` if none exists in the scene hierarchy.
- This ensures test sandbox scenes and isolated prototype levels never fail with `NullReferenceException`.

## Consequences
- **Positive**: Zero coupling between save manager and gameplay components. No circular references across assemblies. Procedural loot systems can store arbitrary data without modifying `GameData.cs`. Seamless sandbox development with auto-instantiated singleton.
- **Considerations**: Components that need persistence must invoke `DataPersistenceManager.Instance.Register(this)` in `OnEnable`/`Start` and `Unregister(this)` in `OnDisable`.
