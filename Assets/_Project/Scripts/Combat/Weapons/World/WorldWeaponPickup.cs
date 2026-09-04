using System;
using PurrNet;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Networked 3D world pickup representing a dropped or spawned weapon in the scene.
    /// Reconstructs modular visuals across all clients via ItemNetSnapshot,
    /// renders a Borderlands-style rarity beam/glow, and supports player interaction & pickup.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WorldWeaponPickup : NetworkBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private WeaponVisualAssembler _visualAssembler;
        [SerializeField] private Transform _visualPivot;
        [SerializeField] private Light _rarityLight;
        [SerializeField] private LineRenderer _rarityBeam;

        [Header("Physics & Floating Animation")]
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private bool _enableFloating = true;
        [SerializeField] private float _rotationSpeed = 45.0f; // Deg/sec
        [SerializeField] private float _bobFrequency = 2.5f;
        [SerializeField] private float _bobAmplitude = 0.12f;

        private ItemNetSnapshot _snapshot;
        private WeaponInstance _cachedWeapon;
        private Vector3 _initialLocalPivotPos;

        public ItemNetSnapshot Snapshot => _snapshot;
        public WeaponInstance Weapon => _cachedWeapon;

        private void Awake()
        {
            if (!_visualPivot)
                _visualPivot = transform;

            _initialLocalPivotPos = _visualPivot.localPosition;

            if (!_visualAssembler)
                _visualAssembler = GetComponentInChildren<WeaponVisualAssembler>();

            if (!_rigidbody)
                _rigidbody = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (!_enableFloating) return;

            // Smooth rotation around Y axis
            _visualPivot.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime, Space.World);

            // Gentle vertical bobbing
            float newY = _initialLocalPivotPos.y + (Mathf.Sin(Time.time * _bobFrequency) * _bobAmplitude);
            _visualPivot.localPosition = new Vector3(_initialLocalPivotPos.x, newY, _initialLocalPivotPos.z);
        }

        /// <summary>
        /// Server-side initialization to broadcast snapshot to all observers and late-joiners.
        /// </summary>
        public void Initialize(ItemNetSnapshot snapshot, Vector3 throwVelocity = default)
        {
            _snapshot = snapshot;
            if (_rigidbody != null && throwVelocity.sqrMagnitude > 0.01f)
            {
                _rigidbody.linearVelocity = throwVelocity;
            }

            RpcInitializePickup(snapshot);
        }

        [ObserversRpc(bufferLast: true)]
        public void RpcInitializePickup(ItemNetSnapshot snapshot)
        {
            _snapshot = snapshot;
            _cachedWeapon = ItemRegistry.Instance ? ItemRegistry.Instance.ReconstructWeapon(snapshot) : null;

            if (_visualAssembler && _cachedWeapon != null)
            {
                _visualAssembler.AssembleVisuals(_cachedWeapon);
            }

            ApplyRarityVisuals();
        }

        private void ApplyRarityVisuals()
        {
            if (_cachedWeapon == null) return;

            Color color = RarityUtility.GetColor(_cachedWeapon.Rarity);

            // 1. Point light glow
            if (_rarityLight)
            {
                _rarityLight.color = color;
                _rarityLight.enabled = true;
            }
            else
            {
                var lightObj = new GameObject("Rarity_Glow_Light");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = Vector3.up * 0.4f;
                _rarityLight = lightObj.AddComponent<Light>();
                _rarityLight.type = LightType.Point;
                _rarityLight.range = 3.5f;
                _rarityLight.intensity = 2.0f;
                _rarityLight.color = color;
            }

            // 2. Vertical Borderlands-style loot beam
            if (!_rarityBeam)
            {
                var beamObj = new GameObject("Rarity_Beam");
                beamObj.transform.SetParent(transform, false);
                beamObj.transform.localPosition = Vector3.zero;
                _rarityBeam = beamObj.AddComponent<LineRenderer>();
                _rarityBeam.positionCount = 2;
                _rarityBeam.useWorldSpace = false;
                _rarityBeam.SetPosition(0, Vector3.zero);
                _rarityBeam.SetPosition(1, Vector3.up * 3.5f);
                _rarityBeam.startWidth = 0.07f;
                _rarityBeam.endWidth = 0.02f;
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (shader) _rarityBeam.material = new Material(shader);
            }

            if (_rarityBeam)
            {
                _rarityBeam.startColor = color;
                _rarityBeam.endColor = new Color(color.r, color.g, color.b, 0f);
            }
        }

        /// <summary>
        /// Called by a local player to request picking up this weapon.
        /// </summary>
        public void RequestPickup()
        {
            RpcRequestPickup();
        }

        [ServerRpc(requireOwnership: false)]
        private void RpcRequestPickup(RPCInfo info = default)
        {
            PlayerID sender = info.sender;

            // Locate sender's weapon controller
            var behaviours = UnityEngine.Object.FindObjectsByType<NetworkBehaviour>();
            Component targetControllerComp = null;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i].owner == sender && behaviours[i].GetType().Name == "PlayerWeaponController")
                {
                    targetControllerComp = behaviours[i];
                    break;
                }
            }

            if (targetControllerComp == null)
            {
                Debug.LogWarning($"[WorldWeaponPickup] Sender {sender} has no PlayerWeaponController.");
                return;
            }

            // Proximity check (prevent remote pickup exploits)
            float dist = Vector3.Distance(transform.position, targetControllerComp.transform.position);
            if (dist > 6.0f)
            {
                Debug.LogWarning($"[WorldWeaponPickup] Sender {sender} is too far ({dist:F1}m).");
                return;
            }

            // Inform the controller to equip or swap
            var equipMethod = targetControllerComp.GetType().GetMethod("EquipOrSwapWeapon");
            if (equipMethod != null)
            {
                equipMethod.Invoke(targetControllerComp, new object[] { _cachedWeapon, _snapshot });
            }

            // Destroy world pickup across network
            if (isServer)
            {
                Destroy(gameObject);
            }
        }
    }
}
