using System;
using Project.Core.Interaction;
using PurrNet;
using UnityEngine;

namespace Project.Combat
{
[RequireComponent(typeof(Collider))]
public class WorldWeaponPickup : NetworkBehaviour, IInteractable
{
    [Header("Visual Components")] 
    [SerializeField] private WeaponVisualAssembler _visualAssembler;
    [SerializeField] private Transform _visualPivot;
    [SerializeField] private Light _rarityLight;
    [SerializeField] private LineRenderer _rarityBeam;
    [SerializeField] private Rigidbody _rigidbody;

    [Header("Interaction Settings")] 
    [SerializeField] private string _interactionPrompt = "[F] Pick up";
    [SerializeField] private string _holdPrompt = "[Hold F] Equip";
    [SerializeField] private string _targetName = "Weapon";
    [SerializeField] private string _subtext = "A dropped weapon";
    [SerializeField] private Color _accentColor = Color.cyan;
    [SerializeField] private InteractionMode _mode = InteractionMode.DualInstantAndHold;
    [SerializeField] private float _holdDuration = 0.75f;

    private ItemNetSnapshot _snapshot;
    private WeaponInstance _cachedWeapon;

    private bool _isClaimed;

    public ItemNetSnapshot Snapshot => _snapshot;
    public WeaponInstance Weapon => _cachedWeapon;

    private void Awake()
    {
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
        {
            gameObject.layer = interactableLayer;
        }

        if (!_visualPivot)
            _visualPivot = transform;

        if (!_visualAssembler)
            _visualAssembler = GetComponentInChildren<WeaponVisualAssembler>();
    }

    public void Initialize(ItemNetSnapshot snapshot, Vector3 throwVelocity = default)
    {
        _snapshot = snapshot;
        if (_rigidbody && throwVelocity.sqrMagnitude > 0.01f)
        {
            _rigidbody.linearVelocity = throwVelocity;
        }

        if (NetworkManager.main)
        {
            RpcInitializePickup(snapshot);
        }
        else
        {
            ApplySnapshot(snapshot);
        }
    }

    [ObserversRpc(runLocally: true, bufferLast: true)]
    public void RpcInitializePickup(ItemNetSnapshot snapshot)
    {
        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(ItemNetSnapshot snapshot)
    {
        _snapshot = snapshot;
        _cachedWeapon = ItemRegistry.Instance ? ItemRegistry.Instance.ReconstructWeapon(snapshot) : null;

        if (_visualAssembler && _cachedWeapon != null)
        {
            _visualAssembler.AssembleVisuals(_cachedWeapon);
        }

        if (_cachedWeapon != null)
        {
            _targetName = _cachedWeapon.DisplayName;
            _subtext =
                $"{_cachedWeapon.Core?.Manufacturer?.DisplayName ?? "Custom"} • Level {_cachedWeapon.Level} {_cachedWeapon.Rarity} {_cachedWeapon.Core?.WeaponType}";
            _accentColor = RarityUtility.GetColor(_cachedWeapon.Rarity);
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

    // Interaction stuff
    public string InteractionPrompt => _interactionPrompt;
    public string HoldPrompt => _holdPrompt;
    public string TargetName => _targetName;
    public string Subtext => _subtext;
    public Color AccentColor => _accentColor;
    public InteractionMode Mode => _mode;
    public float HoldDuration => _holdDuration;

    private IWeaponPickupReceiver GetReceiver(IPlayerInteractor interactor)
    {
        if (interactor == null || !interactor.InteractorGameObject) return null;
        var targetObj = interactor.InteractorGameObject;
        if (targetObj.TryGetComponent<IWeaponPickupReceiver>(out var receiver) ||
            (receiver = targetObj.GetComponentInParent<IWeaponPickupReceiver>()) != null)
        {
            return receiver;
        }
        return null;
    }

    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false)
    {
        if (_isClaimed || interactor == null) return false;

        var receiver = GetReceiver(interactor);
        if (receiver != null)
        {
            return receiver.CanReceiveWeaponPickup(_cachedWeapon, isHold, out _);
        }

        return true;
    }

    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false)
    {
        if (_isClaimed) return "Already Taken";
        if (interactor == null) return null;

        var receiver = GetReceiver(interactor);
        if (receiver != null)
        {
            receiver.CanReceiveWeaponPickup(_cachedWeapon, isHold, out string reason);
            return reason;
        }

        return null;
    }

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        if (_isClaimed) return;

        var receiver = GetReceiver(interactor);
        if (receiver != null)
        {
            if (!receiver.CanReceiveWeaponPickup(_cachedWeapon, isHold, out _))
                return;

            receiver.ReceiveWeaponPickup(_cachedWeapon, _snapshot, isHold);
        }

        if (NetworkManager.main && NetworkManager.main.isClient && !isServer)
        {
            _isClaimed = true;
            RpcRequestPickup();
        }
        else
        {
            _isClaimed = true;
            SafeDespawn();
        }
    }

    [ServerRpc(requireOwnership: false)]
    private void RpcRequestPickup(RPCInfo info = default)
    {
        if (_isClaimed) return;
        _isClaimed = true;
        SafeDespawn();
    }

    private void SafeDespawn()
    {
        if (isSpawned && NetworkManager.main)
        {
            Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void OnHoverEnter(IPlayerInteractor interactor)
    {
    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {
    }
}
}