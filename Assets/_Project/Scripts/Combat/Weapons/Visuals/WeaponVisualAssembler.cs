using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Instantiates and mounts modular 3D weapon meshes into their designated Transform sockets.
    /// Exposes dynamic muzzle and ejector points for VFX and projectile dispatching.
    /// </summary>
    public class WeaponVisualAssembler : MonoBehaviour
    {
        [Header("Mount Target")]
        [SerializeField] private Transform _visualContainer;

        private WeaponInstance _currentWeapon;
        private GameObject _spawnedBodyInstance;
        private Transform _muzzleSocket;
        private Transform _ejectorSocket;
        private readonly Dictionary<string, Transform> _discoveredSockets = new(StringComparer.OrdinalIgnoreCase);

        public Transform MuzzleSocket => _muzzleSocket ? _muzzleSocket : (_visualContainer ? _visualContainer : transform);
        public Transform FirePoint => MuzzleSocket;
        public Transform EjectorSocket => _ejectorSocket ? _ejectorSocket : transform;

        private void Awake()
        {
            if (!_visualContainer)
                _visualContainer = transform;
        }

        /// <summary>
        /// Tears down existing visual parts and builds the modular weapon mesh.
        /// Primary parts (Barrels, Grips, etc.) are mounted first, allowing accessories
        /// (like Silencers or Underbarrel grips) to mount to sockets exposed on the Barrel.
        /// If a muzzle accessory is mounted, the fire point automatically routes to its tip.
        /// </summary>
        public void AssembleVisuals(WeaponInstance weapon)
        {
            ClearVisuals();
            _currentWeapon = weapon;

            if (weapon == null || !weapon.Core) return;

            Transform parentContainer = _visualContainer ? _visualContainer : transform;

            // 1. Instantiate receiver/body core
            if (weapon.Core.BodyPrefab)
            {
                _spawnedBodyInstance = Instantiate(weapon.Core.BodyPrefab, parentContainer);
                _spawnedBodyInstance.transform.localPosition = Vector3.zero;
                _spawnedBodyInstance.transform.localRotation = Quaternion.identity;
                _spawnedBodyInstance.transform.localScale = Vector3.one;

                DiscoverSockets(_spawnedBodyInstance.transform);
            }
            
            var parts = weapon.Parts;
            List<WeaponPartDefinition> accessories = null;

            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (!part || !part.MeshPrefab) continue;

                if (part.PartType == WeaponPartType.Accessory)
                {
                    accessories ??= new List<WeaponPartDefinition>();
                    accessories.Add(part);
                    continue;
                }

                MountPart(part);
            }

            // 3. Mount accessories second (can now attach to Barrel or Core sockets)
            if (accessories != null)
            {
                for (int i = 0; i < accessories.Count; i++)
                {
                    MountPart(accessories[i]);
                }
            }

            // 4. Resolve Muzzle & Ejector Sockets (prioritizing muzzle accessories)
            ResolveMuzzleAndEjectorSockets(_currentWeapon);
        }

        private GameObject MountPart(WeaponPartDefinition part)
        {
            if (!part || !part.MeshPrefab) return null;

            string socketName = part.TargetSocketName;

            if (_discoveredSockets.TryGetValue(socketName, out var socketTransform) && socketTransform)
            {
                var spawnedPart = Instantiate(part.MeshPrefab, socketTransform);
                spawnedPart.transform.localPosition = Vector3.zero;
                spawnedPart.transform.localRotation = Quaternion.identity;
                spawnedPart.transform.localScale = Vector3.one;

                // Scan newly spawned part hierarchy for child sockets (like socket_muzzle on barrels or fire_point on silencers)
                DiscoverSockets(spawnedPart.transform);
                return spawnedPart;
            }

            return null;
        }

        /// <summary>
        /// Hot-swaps the visual mesh for a specific part slot without rebuilding the entire weapon hierarchy.
        /// </summary>
        public void ReassembleSlot(WeaponPartType slot, WeaponPartDefinition newPart)
        {
            if (!_spawnedBodyInstance) return;

            string targetSocketName = newPart ? newPart.TargetSocketName : GetDefaultSocketForPartType(slot);

            if (_discoveredSockets.TryGetValue(targetSocketName, out var socketTransform) && socketTransform)
            {
                // Remove existing visual mesh under this socket
                for (int i = socketTransform.childCount - 1; i >= 0; i--)
                {
                    var child = socketTransform.GetChild(i).gameObject;
                    if (Application.isPlaying) Destroy(child);
                    else DestroyImmediate(child);
                }

                // Mount new part mesh if assigned
                if (newPart && newPart.MeshPrefab)
                {
                    var spawnedPart = Instantiate(newPart.MeshPrefab, socketTransform);
                    spawnedPart.transform.localPosition = Vector3.zero;
                    spawnedPart.transform.localRotation = Quaternion.identity;
                    spawnedPart.transform.localScale = Vector3.one;

                    DiscoverSockets(spawnedPart.transform);
                }

                ResolveMuzzleAndEjectorSockets(_currentWeapon);
            }
        }

        public void ClearVisuals()
        {
            if (_spawnedBodyInstance)
            {
                if (Application.isPlaying)
                    Destroy(_spawnedBodyInstance);
                else
                    DestroyImmediate(_spawnedBodyInstance);

                _spawnedBodyInstance = null;
            }

            _discoveredSockets.Clear();
            _muzzleSocket = null;
            _ejectorSocket = null;
            _currentWeapon = null;
        }

        private void ResolveMuzzleAndEjectorSockets(WeaponInstance weapon = null)
        {
            _muzzleSocket = null;

            // 1. First priority: Check if any mounted accessory is a muzzle attachment overriding the fire point
            if (weapon != null && weapon.Parts != null)
            {
                for (int i = 0; i < weapon.Parts.Count; i++)
                {
                    var part = weapon.Parts[i];
                    if (!part || part.PartType != WeaponPartType.Accessory) continue;

                    if (part.OverridesFirePoint || part.IsMuzzleAttachment)
                    {
                        string targetSocket = part.TargetSocketName;
                        if (_discoveredSockets.TryGetValue(targetSocket, out var mountSocket) && mountSocket)
                        {
                            // Search the accessory attached under this socket for a fire point
                            Transform accFirePoint = FindFirePointInChildren(mountSocket);
                            if (accFirePoint != null)
                            {
                                _muzzleSocket = accFirePoint;
                                break;
                            }

                            // If no explicit fire_point child exists, use the accessory transform itself
                            if (mountSocket.childCount > 0)
                            {
                                _muzzleSocket = mountSocket.GetChild(mountSocket.childCount - 1);
                                break;
                            }
                        }
                    }
                }
            }

            // 2. Second priority: Search mounted barrel hierarchy for its own fire point
            if (!_muzzleSocket && _discoveredSockets.TryGetValue(WeaponSocketNames.Barrel, out var barrelSocket) && barrelSocket)
            {
                _muzzleSocket = FindFirePointInChildren(barrelSocket);

                if (!_muzzleSocket)
                {
                    _muzzleSocket = barrelSocket;
                }
            }

            // 3. Third priority: Check discovered sockets for recognized muzzle aliases
            if (!_muzzleSocket)
            {
                for (int i = 0; i < WeaponSocketNames.MuzzleAliases.Length; i++)
                {
                    if (_discoveredSockets.TryGetValue(WeaponSocketNames.MuzzleAliases[i], out var sock) && sock)
                    {
                        _muzzleSocket = sock;
                        break;
                    }
                }
            }

            // 4. Fallback to visual container or weapon transform
            if (!_muzzleSocket)
            {
                _muzzleSocket = _visualContainer ? _visualContainer : transform;
            }

            // Resolve ejector
            _discoveredSockets.TryGetValue(WeaponSocketNames.Ejector, out _ejectorSocket);
        }

        private static Transform FindFirePointInChildren(Transform root)
        {
            return FindChildRecursive(root, t =>
            {
                string n = t.name;
                for (int i = 0; i < WeaponSocketNames.MuzzleAliases.Length; i++)
                {
                    if (n.Equals(WeaponSocketNames.MuzzleAliases[i], StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return n.IndexOf("muzzle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       n.IndexOf("fire_point", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       n.IndexOf("firepoint", StringComparison.OrdinalIgnoreCase) >= 0;
            });
        }

        private static Transform FindChildRecursive(Transform parent, Func<Transform, bool> predicate)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (predicate(child)) return child;
                var found = FindChildRecursive(child, predicate);
                if (found) return found;
            }
            return null;
        }

        private void DiscoverSockets(Transform root)
        {
            if (IsRecognizedSocket(root.name))
            {
                _discoveredSockets[root.name] = root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                DiscoverSockets(root.GetChild(i));
            }
        }

        private static bool IsRecognizedSocket(string name)
        {
            if (name.StartsWith("socket_", StringComparison.OrdinalIgnoreCase))
                return true;

            for (int i = 0; i < WeaponSocketNames.MuzzleAliases.Length; i++)
            {
                if (name.Equals(WeaponSocketNames.MuzzleAliases[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string GetDefaultSocketForPartType(WeaponPartType type) => type switch
        {
            WeaponPartType.Barrel => WeaponSocketNames.Barrel,
            WeaponPartType.Grip => WeaponSocketNames.Grip,
            WeaponPartType.Stock => WeaponSocketNames.Stock,
            WeaponPartType.Sight => WeaponSocketNames.Sight,
            WeaponPartType.Magazine => WeaponSocketNames.Magazine,
            WeaponPartType.Accessory => WeaponSocketNames.Accessory,
            _ => WeaponSocketNames.Barrel
        };
    }
}
