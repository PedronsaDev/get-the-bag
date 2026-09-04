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

        private GameObject _spawnedBodyInstance;
        private Transform _muzzleSocket;
        private Transform _ejectorSocket;
        private readonly Dictionary<string, Transform> _discoveredSockets = new(StringComparer.OrdinalIgnoreCase);

        public Transform MuzzleSocket => _muzzleSocket != null ? _muzzleSocket : transform;
        public Transform EjectorSocket => _ejectorSocket != null ? _ejectorSocket : transform;

        private void Awake()
        {
            if (!_visualContainer)
                _visualContainer = transform;
        }

        /// <summary>
        /// Tears down existing visual parts and builds the modular weapon mesh.
        /// </summary>
        public void AssembleVisuals(WeaponInstance weapon)
        {
            ClearVisuals();

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

            // 2. Mount parts to discovered sockets
            var parts = weapon.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (!part || !part.MeshPrefab) continue;

                string socketName = part.TargetSocketName;

                if (_discoveredSockets.TryGetValue(socketName, out var socketTransform))
                {
                    var spawnedPart = Instantiate(part.MeshPrefab, socketTransform);
                    spawnedPart.transform.localPosition = Vector3.zero;
                    spawnedPart.transform.localRotation = Quaternion.identity;
                    spawnedPart.transform.localScale = Vector3.one;

                    // Parts (especially Barrels) may contain child sockets (like socket_muzzle)
                    DiscoverSockets(spawnedPart.transform);
                }
            }

            // 3. Resolve Muzzle & Ejector Sockets
            _discoveredSockets.TryGetValue(WeaponSocketNames.Muzzle, out _muzzleSocket);
            _discoveredSockets.TryGetValue(WeaponSocketNames.Ejector, out _ejectorSocket);
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
        }

        private void DiscoverSockets(Transform root)
        {
            if (root.name.StartsWith("socket_", StringComparison.OrdinalIgnoreCase))
            {
                _discoveredSockets[root.name] = root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                DiscoverSockets(root.GetChild(i));
            }
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
