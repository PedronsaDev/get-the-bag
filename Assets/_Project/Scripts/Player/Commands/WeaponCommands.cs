using System;
using System.Text;
using PedronsaDev.Console;
using Project.Combat;
using Project.Player.Weapons;
using UnityEngine;

namespace Project.Player.Commands
{
    [CommandGroup("Weapons")]
    public static class WeaponCommands
    {
        [Command("weap_list", "Lists all registered weapon cores and parts by their string ID and GUID.")]
        public static void ListWeapons()
        {
            var registry = ItemRegistry.Instance;
            if (!registry)
            {
                PedronsaDev.Console.Console.LogError("[Weapons] ItemRegistry instance not found!");
                return;
            }

            StringBuilder sb = new();
            sb.AppendLine("=== REGISTERED WEAPON CORES ===");
            var cores = registry.WeaponCores;
            if (cores.Count == 0)
            {
                sb.AppendLine("  (No cores registered in ItemRegistry. Run 'Scan Project' in ItemRegistry asset.)");
            }
            else
            {
                for (int i = 0; i < cores.Count; i++)
                {
                    var core = cores[i];
                    if (!core) continue;
                    sb.AppendLine($"  [{core.CoreId}] \"{core.BaseWeaponName}\" ({core.WeaponType}) | GUID: {core.Guid}");
                }
            }

            sb.AppendLine("\n=== REGISTERED WEAPON PARTS ===");
            var parts = registry.WeaponParts;
            if (parts.Count == 0)
            {
                sb.AppendLine("  (No parts registered in ItemRegistry. Run 'Scan Project' in ItemRegistry asset.)");
            }
            else
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    var part = parts[i];
                    if (!part) continue;
                    sb.AppendLine($"  [{part.PartId}] \"{part.DisplayName}\" ({part.PartType} - {part.WeaponType}) | GUID: {part.Guid}");
                }
            }

            sb.AppendLine("\n=== REGISTERED WEAPON PRESETS ===");
            var presets = registry.WeaponPresets;
            if (presets.Count == 0)
            {
                sb.AppendLine("  (No presets registered in ItemRegistry. Run 'Scan Project' in ItemRegistry asset.)");
            }
            else
            {
                for (int i = 0; i < presets.Count; i++)
                {
                    var preset = presets[i];
                    if (!preset) continue;
                    sb.AppendLine($"  [{preset.PresetId}] \"{preset.PresetDisplayName}\" ({preset.Rarity} - {preset.Core?.WeaponType}) | GUID: {preset.Guid}");
                }
            }

            PedronsaDev.Console.Console.Log(sb.ToString());
        }

        [Command("weap_give_preset", "Gives the local player a dedicated weapon preset by ID. Usage: weap_give_preset <presetId> [level]")]
        public static void GivePreset(string presetId, byte level = 1)
        {
            var registry = ItemRegistry.Instance;
            if (!registry)
            {
                PedronsaDev.Console.Console.LogError("[Weapons] ItemRegistry instance not found!");
                return;
            }

            if (string.IsNullOrWhiteSpace(presetId))
            {
                PedronsaDev.Console.Console.LogWarning("Usage: weap_give_preset <presetId> [level]\nRun 'weapon_list' to view available IDs.");
                return;
            }

            if (!registry.TryGetPresetById(presetId, out var preset))
            {
                PedronsaDev.Console.Console.LogError($"[Weapons] Could not find weapon preset with ID '{presetId}'. Run 'weapon_list' to see valid presets.");
                return;
            }

            uint seed = (uint)UnityEngine.Random.Range(1, 999999);
            var weapon = registry.GenerateWeaponFromPreset(preset, seed, (byte)Mathf.Clamp(level, 1, 100));
            if (weapon == null)
            {
                PedronsaDev.Console.Console.LogError("[Weapons] Failed to generate weapon from preset.");
                return;
            }

            var controller = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponController>();
            if (!controller)
            {
                PedronsaDev.Console.Console.LogError("[Weapons] No local PlayerWeaponController found in the current scene.");
                return;
            }

            controller.EquipWeapon(weapon);
            PedronsaDev.Console.Console.Log($"[Weapons] Successfully equipped Preset: {weapon.DisplayName} (Level {weapon.Level} {weapon.Rarity}) [Damage: {weapon.CompiledStats.Damage:F1}]");
        }

        [Command("weap_give", "Gives the local player a procedural weapon by string ID. Usage: weap_give <coreId> [level] [rarity]")]
        public static void GiveWeapon(string coreId, byte level = 1, string rarity = "Common")
        {
            var registry = ItemRegistry.Instance;
            if (!registry)
            {
                PedronsaDev.Console.Console.LogError("[Weapons] ItemRegistry instance not found!");
                return;
            }

            if (string.IsNullOrWhiteSpace(coreId))
            {
                PedronsaDev.Console.Console.LogWarning("Usage: weap_give <coreId> [level] [rarity]\nRun 'weapon_list' to view available IDs.");
                return;
            }

            // O(1) Fast lookup by human-friendly string ID
            if (!registry.TryGetCoreById(coreId, out var core))
            {
                PedronsaDev.Console.Console.LogError($"[Weapons] Could not find weapon core with ID '{coreId}'. Run 'weapon_list' to see valid IDs.");
                return;
            }

            if (!Enum.TryParse<ItemRarity>(rarity, true, out var itemRarity))
            {
                itemRarity = ItemRarity.Common;
            }

            uint seed = (uint)UnityEngine.Random.Range(1, 999999);
            var weapon = registry.GenerateWeapon(core, seed, (byte)Mathf.Clamp(level, 1, 100), itemRarity);
            if (weapon == null)
            {
                PedronsaDev.Console.Console.LogError("[Weapons] Failed to generate weapon instance.");
                return;
            }

            // Unity 6: Use FindAnyObjectByType per GEMINI.md
            var controller = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponController>();
            if (!controller)
            {
                PedronsaDev.Console.Console.LogError("[Weapons] No local PlayerWeaponController found in the current scene.");
                return;
            }

            controller.EquipWeapon(weapon);
            PedronsaDev.Console.Console.Log($"[Weapons] Successfully equipped: {weapon.DisplayName} (Level {weapon.Level} {weapon.Rarity}) [Damage: {weapon.CompiledStats.Damage:F1}]");
        }

        [Command("weap_inspect", "Prints detailed stats and mounted parts of currently held weapon.")]
        public static void InspectWeapon()
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponController>();
            if (!controller || controller.ActiveWeapon == null)
            {
                PedronsaDev.Console.Console.LogWarning("[Weapons] No active weapon currently equipped.");
                return;
            }

            var w = controller.ActiveWeapon;
            var s = w.CompiledStats;

            StringBuilder sb = new();
            sb.AppendLine($"=== WEAPON: {w.DisplayName.ToUpper()} ===");
            sb.AppendLine($"Archetype: {w.Core.BaseWeaponName} (Core ID: {w.Core.CoreId})");
            sb.AppendLine($"Core GUID: {w.Core.Guid}");
            sb.AppendLine($"Level: {w.Level} | Rarity: {w.Rarity} | Seed: {w.Seed}");
            sb.AppendLine($"Damage: {s.Damage:F1} x {s.PelletsPerShot} | Fire Rate: {s.FireRate:F0} RPM | Mag: {s.MagazineSize}");
            sb.AppendLine($"Reload: {s.ReloadSpeed:F2}s | Mode: {s.FiringMode} | Element: {s.Element}");

            float burstDps = (s.Damage * s.PelletsPerShot) * (s.FireRate / 60f);
            float cycleTime = (s.MagazineSize / (s.FireRate / 60f)) + s.ReloadSpeed;
            float sustainedDps = (s.Damage * s.PelletsPerShot * s.MagazineSize) / cycleTime;
            sb.AppendLine($"Burst DPS: {burstDps:F1} | Sustained DPS: {sustainedDps:F1}");

            sb.AppendLine("\nMounted Parts:");
            for (int i = 0; i < w.Parts.Count; i++)
            {
                var p = w.Parts[i];
                if (!p) continue;
                sb.AppendLine($"  - [{p.PartType}] {p.DisplayName} (ID: {p.PartId}, GUID: {p.Guid})");
            }

            PedronsaDev.Console.Console.Log(sb.ToString());
        }

        [Command("weap_drop", "Drops the currently equipped weapon into the scene as a 3D world pickup")]
        public static void DropWeapon()
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponController>();
            if (!controller || controller.ActiveWeapon == null)
            {
                PedronsaDev.Console.Console.LogWarning("[Weapons] No active weapon equipped to drop.");
                return;
            }

            string name = controller.ActiveWeapon.DisplayName;
            controller.DropSpecificWeapon();
            PedronsaDev.Console.Console.Log($"[Weapons] Dropped '{name}' into the scene!");
        }

        [Command("weap_spawn_pickup", "Spawns a 3D world weapon pickup in front of the player. Usage: weap_spawn_pickup <coreId/presetId> [level] [rarity]")]
        public static void SpawnPickup(string id, byte level = 1, string rarity = "Common")
        {
            var registry = ItemRegistry.Instance;
            if (!registry)
            {
                PedronsaDev.Console.Console.LogError("[Weapons] ItemRegistry instance not found!");
                return;
            }

            var controller = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponController>();
            Vector3 spawnPos = controller ? controller.transform.position + controller.transform.forward * 2.0f + Vector3.up * 0.5f : Vector3.up * 1f;

            ItemNetSnapshot snapshot = default;

            if (registry.TryGetPresetById(id, out var preset))
            {
                uint seed = (uint)UnityEngine.Random.Range(1, 999999);
                var weapon = registry.GenerateWeaponFromPreset(preset, seed, (byte)Mathf.Clamp(level, 1, 100));
                snapshot = registry.SerializeWeapon(weapon);
            }
            else if (registry.TryGetCoreById(id, out var core))
            {
                if (!Enum.TryParse<ItemRarity>(rarity, true, out var itemRarity))
                    itemRarity = ItemRarity.Common;

                uint seed = (uint)UnityEngine.Random.Range(1, 999999);
                var weapon = registry.GenerateWeapon(core, seed, (byte)Mathf.Clamp(level, 1, 100), itemRarity);
                snapshot = registry.SerializeWeapon(weapon);
            }
            else
            {
                PedronsaDev.Console.Console.LogError($"[Weapons] '{id}' is neither a valid Core ID nor a Preset ID. Run 'weapon_list' to view available IDs.");
                return;
            }

            var pickupObj = new GameObject($"WorldPickup_{snapshot.CustomName ?? "Weapon"}");
            pickupObj.transform.position = spawnPos;

            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0)
            {
                pickupObj.layer = interactableLayer;
            }

            var collider = pickupObj.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 1.2f;

            var visualRoot = new GameObject("Visual_Root");
            visualRoot.transform.SetParent(pickupObj.transform, false);
            visualRoot.AddComponent<WeaponVisualAssembler>();

            var pickup = pickupObj.AddComponent<WorldWeaponPickup>();

            if (PurrNet.NetworkManager.main && PurrNet.NetworkManager.main.isServer)
            {
                PurrNet.NetworkManager.main.Spawn(pickupObj);
            }

            pickup.Initialize(snapshot);

            PedronsaDev.Console.Console.Log($"[Weapons] Spawned 3D World Pickup: '{snapshot.CustomName}' at {spawnPos}!");
        }
    }
}
