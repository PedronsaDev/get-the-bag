# ADR 001: Folder Structure & Domain Organization

## Status
Accepted

## Context
As the project expands to include procedural weapons, co-op networking via PurrNet, extraction game loops, and modular dungeon generation, keeping scripts and assets in a flat or disorganized hierarchy causes severe merge conflicts, circular dependencies, and asset clutter.

## Decision
1. Isolate all project-specific assets, code, and configurations inside `Assets/_Project/`.
2. Third-party plugins (`Plugins/`, `PurrLobby/`, `TextMesh Pro/`) remain isolated at the root of `Assets/` to enable painless package updates.
3. Group content by domain (`Art/`, `Audio/`, `Data/`, `Prefabs/`, `Scenes/`, `Scripts/`, `Settings/`).
4. Keep all leaf directories tracked in Git using `.gitkeep` files.
5. Plan assembly definitions (`.asmdef`) for each functional domain in `Scripts/`.

## Consequences
- **Positive**: Clean separation of code from content; modular team development; painless Git tracking; clear mental model.
- **Rules to Follow**:
  - Never place project scripts in root `Assets/Scripts/`.
  - All new ScriptableObject assets belong in `Assets/_Project/Data/<Domain>/`.
  - Prefabs must be sorted by functional type under `Assets/_Project/Prefabs/<Domain>/`.
