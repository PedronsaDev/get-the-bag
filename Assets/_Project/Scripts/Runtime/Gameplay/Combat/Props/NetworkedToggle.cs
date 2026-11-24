using Project.Core.Interaction;
using PurrNet;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Combat.Props
{
[RequireComponent(typeof(Collider))]
public class NetworkedToggle : NetworkBehaviour, IInteractable
{
    [Header("Prompts")]
    [SerializeField] private string _targetName = "Switch";
    [SerializeField] private string _onPrompt = "[F] Turn Off";
    [SerializeField] private string _offPrompt = "[F] Turn On";
    [SerializeField] private Color _accentColor = Color.white;

    [Header("Targets (applied on every peer)")]
    [SerializeField] private bool _startOn = true;
    [SerializeField] private GameObject[] _enableWhenOn;
    [SerializeField] private AudioSource[] _audioWhenOn;
    [SerializeField] private UnityEvent<bool> _onChanged;

    public SyncVar<bool> IsOn = new(true);
    private bool _localOn;

    public bool On => isSpawned ? IsOn.value : _localOn;

    public string InteractionPrompt => On ? _onPrompt : _offPrompt;
    public string HoldPrompt => string.Empty;
    public string TargetName => _targetName;
    public string Subtext => On ? "On" : "Off";
    public Color AccentColor => _accentColor;
    public InteractionMode Mode => InteractionMode.Instant;
    public float HoldDuration => 0f;

    private void Awake()
    {
        _localOn = _startOn;
        int layer = LayerMask.NameToLayer("Interactable");
        if (layer >= 0) gameObject.layer = layer;
        IsOn.onChanged += Apply;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        IsOn.onChanged -= Apply;
    }

    protected override void OnSpawned()
    {
        if (isServer) IsOn.value = _startOn;
        Apply(IsOn.value);
    }

    private void Start()
    {
        if (!isSpawned) Apply(_localOn);
    }

    private void Apply(bool on)
    {
        foreach (var go in _enableWhenOn)
            if (go) go.SetActive(on);

        foreach (var src in _audioWhenOn)
        {
            if (!src) continue;
            if (on && !src.isPlaying) src.Play();
            else if (!on) src.Stop();
        }

        _onChanged?.Invoke(on);
    }

    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false) => interactor != null;
    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false) => string.Empty;

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        if (!isSpawned)
        {
            _localOn = !_localOn;
            Apply(_localOn);
        }
        else if (isServer)
            IsOn.value = !IsOn.value;
        else
            RpcToggle();
    }

    [ServerRpc(requireOwnership: false)]
    private void RpcToggle()
    {
        IsOn.value = !IsOn.value;
    }

    public void OnHoverEnter(IPlayerInteractor interactor) { }
    public void OnHoverExit(IPlayerInteractor interactor) { }
}
}
