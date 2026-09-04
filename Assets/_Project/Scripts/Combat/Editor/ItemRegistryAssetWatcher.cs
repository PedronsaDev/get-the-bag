using System;
using UnityEditor;
using UnityEngine;

namespace Project.Combat.Editor
{
    /// <summary>
    /// Automatic watcher that listens to Unity's asset pipeline.
    /// Whenever a WeaponCoreDefinition, WeaponPartDefinition, WeaponPresetDefinition,
    /// or ManufacturerDefinition is created, modified, moved, or deleted,
    /// ItemRegistry is automatically updated and saved without requiring manual clicks.
    /// </summary>
    public class ItemRegistryAssetWatcher : AssetPostprocessor
    {
        private static bool _scanQueued;

        [InitializeOnLoadMethod]
        private static void InitializeWatcher()
        {
            // Initial scan when Unity starts up, loads project, or finishes compilation
            EditorApplication.delayCall += () =>
            {
                RequestScan(silent: true);
            };

            // Pre-playmode synchronization to guarantee runtime has all assets
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                {
                    var registry = ItemRegistry.Instance;
                    if (registry != null)
                    {
                        registry.ScanProjectAssets(silent: true);
                    }
                }
            };
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool hasRelevantChanges = false;

            for (int i = 0; i < importedAssets.Length; i++)
            {
                if (IsRelevantAsset(importedAssets[i]))
                {
                    hasRelevantChanges = true;
                    break;
                }
            }

            if (!hasRelevantChanges)
            {
                for (int i = 0; i < deletedAssets.Length; i++)
                {
                    if (IsRelevantDeletedPath(deletedAssets[i]))
                    {
                        hasRelevantChanges = true;
                        break;
                    }
                }
            }

            if (!hasRelevantChanges)
            {
                for (int i = 0; i < movedAssets.Length; i++)
                {
                    if (IsRelevantAsset(movedAssets[i]) || IsRelevantDeletedPath(movedFromAssetPaths[i]))
                    {
                        hasRelevantChanges = true;
                        break;
                    }
                }
            }

            if (hasRelevantChanges)
            {
                RequestScan(silent: true);
            }
        }

        /// <summary>
        /// Schedules a debounced registry scan on the next editor tick.
        /// </summary>
        public static void RequestScan(bool silent = true)
        {
            if (_scanQueued) return;
            _scanQueued = true;

            EditorApplication.delayCall += () =>
            {
                _scanQueued = false;

                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    // Delay again if editor is in the middle of compilation
                    RequestScan(silent);
                    return;
                }

                var registry = ItemRegistry.Instance;
                if (registry != null)
                {
                    registry.ScanProjectAssets(silent: silent);
                }
            };
        }

        private static bool IsRelevantAsset(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return false;
            if (path.EndsWith("ItemRegistry.asset", StringComparison.OrdinalIgnoreCase)) return false;

            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (type != null)
            {
                return typeof(WeaponCoreDefinition).IsAssignableFrom(type) ||
                       typeof(WeaponPartDefinition).IsAssignableFrom(type) ||
                       typeof(WeaponPresetDefinition).IsAssignableFrom(type) ||
                       typeof(ManufacturerDefinition).IsAssignableFrom(type);
            }

            return IsRelevantDeletedPath(path);
        }

        private static bool IsRelevantDeletedPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return false;
            if (path.EndsWith("ItemRegistry.asset", StringComparison.OrdinalIgnoreCase)) return false;

            return path.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Manufacturer", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
