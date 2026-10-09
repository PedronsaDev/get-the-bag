# Local player HUD

Data flow: `LocalPlayerContext → PlayerHUDBinder → widget.Bind(component) → snapshot + change events → TMP labels`.

`UIRoot` ensures a `PlayerHUDBinder` exists on its root. The binder discovers the first ammo and health widget below it, including inactive children. Alternatively add the binder explicitly and assign its widget references in the Inspector. Keep it above containers that are independently hidden. Widgets should exist before the root awakens; dynamically created widgets need explicit binding.

## Setup

- Add `WeaponAmmoUI` to an ammo label or container under UIRoot. Assign **Ammo Text** and optionally **Reload Text**. A TMP label on the same GameObject is detected automatically. Ammo displays magazine / reserve.
- Assign **Health Text** and optionally **Shield Text** on the existing `PlayerHealthUI`. Both are TMP labels. No shield capacity leaves its label blank.
- No player references belong in the prefab: the binder supplies the current local player's weapon controller and health component when the player spawns.

## Lifecycle contract

`Bind(source)` detaches the previous source, subscribes to the new one, and reads its current state immediately. Repeating the same binding does not add duplicate subscriptions. `Unbind()` detaches and clears the labels.

Disabling a widget removes its subscriptions but retains the assigned source. Re-enabling reads a fresh snapshot. Disabling the binder unbinds its widgets; re-enabling resolves the current local player again. Player replacement detaches the old sources; despawn clears the HUD.

`PlayerWeaponController.OnEquipmentChanged` forwards confirmed equipment updates as a plain C# event. The HUD never polls in Update and never writes gameplay state. Health uses its existing health/shield events.

To add another HUD widget, give it typed Bind/Unbind methods, snapshot rendering, and event cleanup; add its reference and binding call to PlayerHUDBinder. No global message bus is needed.
