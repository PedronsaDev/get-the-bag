# Weapon visual loading checkpoint

**Historical single-view extraction checkpoint.** Resource ownership described here still applies; the presenter now uses two bindings for the owner. Read [the current dual-view setup](DUAL-VIEW.md) for current selection, visibility and failure behavior.

This is the single-view extraction within Milestone 1. It prepares the loader for two independent visual bindings. It does not yet create the owner's world-shadow weapon alongside the FP weapon.

`WeaponVisualBinding` is a plain C# resource owner, not a component to add in the Inspector. Create one object per load. Its input is an `AssetReferenceT<GameObject>`, the WeaponDefinition, the destination socket and a cancellation token. Its read-only `Instance` is the runtime WeaponInstance; `IsLoaded` reports whether the instance and owned handle are still usable.

The definition's Addressable reference identifies a prefab on disk/in the catalog. Loading returns a handle that keeps that prefab and its dependencies available. Instantiating creates a separate runtime GameObject. Destroying that GameObject and releasing the load handle are distinct operations; the binding owns both.

```text
Presenter: choose weapon/prefab/socket and begin a versioned request
Binding: acquire prefab handle, await load, validate root WeaponInstance, instantiate, initialize
Presenter: verify the request is still current
Presenter: reset/dispose old binding and adopt the completed binding
Later clear/despawn: presenter disposes its active binding
```

During LoadAsync, the acquired handle and any temporary clone remain local variables. Only a successful load transfers them to the binding fields. A finally block destroys/releases anything that was not transferred. This includes cancellation and validation failure after the handle was already acquired.

Dispose clears the binding's fields before destroying its clone and releasing its owned handle. Calling it twice is safe. If disposed while awaiting a load, the binding refuses to publish the result and releases its temporary resources when the await completes. The presenter's cancellation token lets a superseded request stop waiting promptly.

The presenter owns the request version and cancellation source because it decides which equipment is current. A successfully loaded binding can still be superseded before adoption; the presenter's finally block disposes that unadopted binding. Once adopted, the presenter becomes responsible for its later disposal.

Only one active binding is used at this checkpoint. Owner FP/world selection, rifle fallback, fire/reload notifications and procedural motion retain their existing behavior. Earlier replacement-failure policy is also unchanged; the dual-view milestone must finish the policy that prevents stale equipment visuals.

The next change introduces separate world and FP bindings using CharacterRigBinding's sockets, followed by their visibility/effect policy. Keep gameplay authority and Addressables resource ownership separate throughout that migration.
