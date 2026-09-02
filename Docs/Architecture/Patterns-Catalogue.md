# Architecture Patterns Catalogue

This living document contains the core software design patterns used across the **proc-weapon-game** project. Refer to this catalogue whenever you design new systems or refactor existing ones.

---

## 1. The Facade Pattern

### The Problem
A player or enemy entity consists of multiple components: `KinematicCharacterMotor`, `PlayerHealth`, `PlayerCombat`, `PlayerInventory`, and `NetworkIdentity`. If external systems (like UI, enemy AI, or interactable items) try to search for and communicate with each internal component directly, code becomes brittle and heavily coupled.

### The Solution
A single root component (the **Facade**) acts as the unified gateway for the entity.

```csharp
// Assets/_Project/Scripts/Player/PlayerFacade.cs
using UnityEngine;
using PurrNet;

namespace Project.Player
{
    [SelectionBase]
    [DisallowMultipleComponent]
    public class PlayerFacade : NetworkBehaviour
    {
        [Header("Subsystems")]
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private PlayerHealth _health;
        [SerializeField] private PlayerCombat _combat;
        [SerializeField] private PlayerInventory _inventory;

        // Public read-only accessors
        public PlayerMotor Motor => _motor;
        public PlayerHealth Health => _health;
        public PlayerCombat Combat => _combat;
        public PlayerInventory Inventory => _inventory;

        public bool IsLocalPlayer => isOwner; // PurrNet ownership check
    }
}
```

### Rules:
- ✅ **DO**: Let external scripts (like a revive prompt or damage trigger) talk to `PlayerFacade`.
- ❌ **DON'T**: Let UI or external scripts search for child objects or deep private components via `GetComponentInChildren`.

---

## 2. The Observer Pattern (Event-Driven Decoupling)

### The Problem
If `PlayerHealth` has direct references to a UI Health Bar, a post-processing blood vignette, an AudioSource for hurt screams, and a camera shake script, testing or changing any of those systems breaks `PlayerHealth`.

### The Solution
`PlayerHealth` maintains state and fires an event when that state changes. It has **zero awareness** of UI, Audio, or VFX.

```csharp
// Assets/_Project/Scripts/Combat/PlayerHealth.cs
using System;
using PurrNet;
using UnityEngine;

namespace Project.Combat
{
    public class PlayerHealth : NetworkBehaviour
    {
        [SyncVar(OnChange = nameof(OnHealthSyncChanged))]
        private float _currentHealth = 100f;
        
        [SerializeField] private float _maxHealth = 100f;

        // C# Event for local listeners (UI, Audio, VFX)
        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action OnDied;

        private void OnHealthSyncChanged(float oldVal, float newVal)
        {
            OnHealthChanged?.Invoke(newVal, _maxHealth);
            if (newVal <= 0f) OnDied?.Invoke();
        }

        // Only Host/Server should invoke this in authoritative setup
        public void ApplyDamage(float amount)
        {
            if (!isServer) return;
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        }
    }
}
```

```csharp
// Assets/_Project/Scripts/UI/HealthBarPresenter.cs
using UnityEngine;
using UnityEngine.UI;
using Project.Combat;

namespace Project.UI
{
    public class HealthBarPresenter : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private PlayerHealth _playerHealth;

        private void OnEnable()
        {
            if (_playerHealth != null)
                _playerHealth.OnHealthChanged += UpdateHealthBar;
        }

        private void OnDisable()
        {
            if (_playerHealth != null)
                _playerHealth.OnHealthChanged -= UpdateHealthBar;
        }

        private void UpdateHealthBar(float current, float max)
        {
            _slider.value = current / max;
        }
    }
}
```

### Rules:
- ✅ **DO**: Subscribe in `OnEnable` and unsubscribe in `OnDisable` to prevent memory leaks.
- ❌ **DON'T**: Pass UI slider references or AudioClips into gameplay logic scripts.

---

## 3. Interface Segregation (`IDamageable`, `IInteractable`)

### The Problem
When a bullet hits a collider, writing `hit.collider.GetComponent<PlayerHealth>()` and then `hit.collider.GetComponent<EnemyHealth>()` and `hit.collider.GetComponent<ExplosiveBarrel>()` creates an unmaintainable chain of `if-else` type checks.

### The Solution
Define small, single-purpose contracts (interfaces). Anything that can receive damage implements `IDamageable`.

```csharp
// Assets/_Project/Scripts/Combat/IDamageable.cs
namespace Project.Combat
{
    public struct DamageData
    {
        public float Amount;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public DamageType Type; // e.g. Kinetic, Fire, Acid
        public ulong AttackerId;
    }

    public interface IDamageable
    {
        void TakeDamage(DamageData damage);
    }
}
```

```csharp
// Firing script only checks for the contract:
if (hitCollider.TryGetComponent<IDamageable>(out var damageable))
{
    damageable.TakeDamage(damageData);
}
```

---

## 4. Assembly Definitions (`.asmdef`)

### The Problem
By default, Unity puts all scripts into a single monolithic assembly (`Assembly-CSharp.dll`). When the project reaches hundreds of files, changing one line of code causes the entire game to recompile, stalling iteration time and allowing any script to reference any other script.

### The Solution
Use Assembly Definitions to split the project into modular packages with explicit boundaries:
- `Project.Core`: Zero dependencies on gameplay. Holds base contracts, utilities, and event buses.
- `Project.Networking`: Depends on `PurrNet` and `Project.Core`.
- `Project.Player`: Depends on `Project.Core`, `KinematicCharacterController`, and `Project.Networking`.
- `Project.UI`: Listens to events, depends on `Project.Core`. Cannot be referenced by `Project.Player` or `Project.Combat`.
