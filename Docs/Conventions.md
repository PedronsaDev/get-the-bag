# Naming and folder conventions

Use this when adding an asset, a script or a folder. The goal is that you can guess where something
lives and what a file is from its name alone. When an existing asset breaks a rule, rename it with its
`.meta` file (or in the Unity Project window) so its GUID and every reference survive.

## Folders

- No spaces or `&` in folder names under `Assets/_Project`. Use PascalCase (`EventChannels`, `NetworkData`).
- Group by asset type first (`Prefabs`, `Data`, `Animations`, `Art`), then by feature (`Loot`, `Weapons`, `AI`).
- Do not create empty placeholder folders. Unity writes a `.meta` for each one and Git keeps it, so the
  folder reappears forever. Create the folder with its first asset.
- `Resources/` is only for assets loaded by string with `Resources.Load`. Everything in it ships in every
  build. Prefer a serialized reference or an Addressable address.
- A folder named `Editor` inside an asmdef folder is **not** editor-only. Editor code goes under
  `Scripts/Editor/<Feature>` in an asmdef whose platforms are `Editor`.

## Scripts and namespaces

- One public type per file; the file name equals the type name.
- The namespace starts with the assembly's `rootNamespace` (`Project.Match`, `Project.Definitions.Loot`).
  A feature subfolder may add one segment (`Project.Match.Zones`, `Project.AI.Cover`).
- Editor assemblies may mirror the runtime feature with an `.Editor` suffix (`Project.AI.Cover.Editor`).
- Avoid a namespace segment that shadows a Unity type used in that code (`Camera`, `Physics`).
  That is why the player camera uses `Project.Player.FPSCamera`. See GEMINI.md on `Project.Core.Physics`.
- Development-only components use the `DEV_` class prefix and live in `Scripts/Development`.

## Asset prefixes

Format: `PREFIX_Name_Variant`, with `Name` in PascalCase and `Variant` as `A`, `B`, or a descriptive word.

### Data (ScriptableObjects, under `Data/`)

| Prefix | Type | Example |
| --- | --- | --- |
| `WEAP_` | WeaponDefinition | `WEAP_Pistol_A` |
| `WEAPPRES_` | WeaponPresentationDefinition | `WEAPPRES_Pistol_A` |
| `CHARVIS_` | CharacterVisualDefinition | `CHARVIS_Player_A` |
| `MOD_` | WeaponAttachmentDefinition | `MOD_Muzzle_Basic` |
| `LOOT_` | LootDefinition | `LOOT_GoldBar` |
| `POOL_` | LootPool | `POOL_Vault` |
| `ARCH_` | AIArchetypeConfig | `ARCH_Cop_A` |
| `WAVES_` | AIWaveConfig | `WAVES_Default` |
| `LVL_` | LevelDefinition | `LVL_Bank` |
| *(none)* | Registries, channels, settings singletons | `WeaponRegistry`, `RaidEventChannel` |

### Prefabs (`<CATEGORY>_PF_<Name>`, under `Prefabs/<Category>/`)

| Prefix | Folder | Example |
| --- | --- | --- |
| `CHAR_PF_` | `Player` | `CHAR_PF_Player` |
| `WEAP_PF_` / `WEAP_FP_` | `Weapons/<Class>` | `WEAP_PF_Pistol_A` (world), `WEAP_FP_Pistol_A` (first person) |
| `LOOT_PF_` | `Loot/<Category>` | `LOOT_PF_GoldBar` |
| `AI_PF_` | `AI` | `AI_PF_Civilian_A` |
| `COMBAT_PF_` | `Combat` | `COMBAT_PF_Projectile` |
| `MATCH_PF_` | `Match` | `MATCH_PF_ExtractionZone` |
| `ENV_PF_` | `Environment` | `ENV_PF_DirectionalLight` |
| `CAM_PF_` | `Camera` | `CAM_PF_CameraRig` |
| `UI_PF_` | `UI/<Screen>` | `UI_PF_InventorySlot` |
| `DEV_PF_` | `Development` | `DEV_PF_DamagePopup` |
| `FX_PF_` | `Art/VFX` (with its mesh, material and texture) | `FX_PF_MuzzleFlash` |

Prefabs loaded by `Resources.Load` keep the name their code path expects (`UIRoot`, `[CORE_SERVICES]`).

### Art

| Prefix | Type |
| --- | --- |
| `M_` | Material |
| `T_` | Texture (`_D` diffuse, `_N` normal, `_M` mask) |
| `SM_` | Static mesh |
| `ICON_` | Editor icon sprite |

### Animation

Animation clips use the **rig** as their prefix, not the asset category. These names are shared with
the Blender export presets in `Tools/Blender`, so keep them as they are. See `Docs/Animation/AUTHORING.md`.

| Prefix | Rig |
| --- | --- |
| `Body_` | Full-body character (`Body_Human_Walk_Forward`, `Body_Pistol_A_Reload_Normal`) |
| `FP_` | First-person arms (`FP_Pistol_A_Fire`) |
| `WEP_` | Weapon mechanism (`WEP_Pistol_A_Reload_Empty`). Not the same as the `WEAP_` data prefix |
| `REF_` | Retained reference clips |

Controllers: `<Subject>_<View>.controller` (`Pistol_A_FP`, `Human_Body`).
Clips for one action variant go in that variant's folder (`Reload/Empty/`, `Reload/Normal/`).

### Scenes

`Scene_<Name>`. Playable levels live in `Scenes/`, the startup scene in `Scenes/Core/`, and test gyms in
`Scenes/Dev/Scene_Gym_<Topic>`.

## Exceptions

Third-party packs keep their vendor names (`Art/Sprites/Input/*`, generated `Art/Generated/Outlines`).
Do not rename inside them; replace or wrap them instead.
