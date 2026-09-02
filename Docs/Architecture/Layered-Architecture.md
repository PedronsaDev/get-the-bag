# Layered Architecture: Systems & Dependency Flow

This document details how different architectural layers communicate in **proc-weapon-game**, specifically defining the boundary between **Zenject (Dependency Injection)** and **PurrNet (Networking)**.

---

## The Dependency Pyramid

```
┌─────────────────────────────────────────────────────────────┐
│                    Layer 4: Presentation                    │
│      (UI / HUD, Audio Emitters, Camera Shakes, VFX)         │
└──────────────────────────────┬──────────────────────────────┘
                               │ Observes via events / signals
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                    Layer 3: Gameplay Logic                  │
│   (Player Motor, Combat, Procedural Generation, Loot, AI)   │
└──────────────────────────────┬──────────────────────────────┘
                               │ Coordinates / Syncs via
                               ▼
┌─────────────────────────────────────────────────────────────┐
│               Layer 2: Network & Core Services              │
│      (PurrNet NetworkRules, Session Flow, EventBus)         │
└──────────────────────────────┬──────────────────────────────┘
                               │ Injected via
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                 Layer 1: Infrastructure & DI                │
│    (Zenject ProjectContext, InputReader, SaveLoad, Configs) │
└─────────────────────────────────────────────────────────────┘
```

---

## 1. Zenject vs. PurrNet: The Golden Rule

> [!IMPORTANT]
> **Zenject handles *Services*; PurrNet handles *Actors*.**

- **Zenject Scope**:
  - Global services that exist across scenes: `IInputReader`, `ISaveLoadService`, `IAudioManager`, `IGameStateManager`.
  - Installed in `ProjectContext` (or `SceneContext` for scene-specific services).
  - These services are injected into entry points, controllers, and managers.

- **PurrNet Scope**:
  - Networked actors dynamically spawned/destroyed: `PlayerFacade`, `NetworkEnemy`, `WeaponPickup`, `ExtractionConsole`.
  - Managed by PurrNet's `NetworkManager` and replicated via `NetworkIdentity` and `NetworkBehaviour`.
  - Networked actors **do not** require Zenject injection on every instantiated component. Instead, if an actor needs a global service (e.g. `IInputReader`), it accesses it via an injected controller or through a typed service accessor.

---

## 2. Communication Between Layers

1. **Top-Down (Calls)**: High-level systems can call low-level services (e.g. `PlayerCombat` queries `IInputReader.IsFiring`).
2. **Bottom-Up (Events)**: Low-level systems notify high-level presentation via **Events** (e.g. `PlayerHealth.OnHealthChanged` triggers UI updates).
3. **Lateral (Interfaces)**: Peers interact solely through interfaces (`IDamageable`, `IInteractable`).
