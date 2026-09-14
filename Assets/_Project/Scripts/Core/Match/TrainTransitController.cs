using System;
using Cysharp.Threading.Tasks;
using Project.Networking.Scenes;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.Core.Match
{
public enum TransitState
{
    DockedAtHub,
    Departing,
    InTransit,
    Arriving,
    Arrived
}

[RequireComponent(typeof(NetworkIdentity))]
[DisallowMultipleComponent]
public class TrainTransitController : NetworkBehaviour
{
    public static TrainTransitController Instance { get; private set; }
    
    [Header("References")] 
    [SerializeField] private TransitionEventChannel _transitionEvents;
    
    [Header("State")] public SyncVar<TransitState> CurrentState = new(TransitState.DockedAtHub);

    [Header("Timing Settings")]
    [SerializeField] private float _departureDuration = 4.0f;
    [SerializeField] private float _arrivalDuration = 4.0f;

    [Header("VFX & Environment")] 
    [SerializeField] private GameObject _inTransitVFX;

    private string _targetSceneName;
    private bool _isTransitInProgress;

    public event Action<TransitState> OnTransitStateChanged;
    public event Action OnDoorsOpened;
    public event Action OnDoorsClosed;

    private void Awake()
    {
        if (!Instance)
        {
            Instance = this;
            if (!NetworkManager.main || NetworkManager.main.isOffline)
            {
                DontDestroyOnLoad(transform.root.gameObject);
            }
        }
        else if (Instance != this)
        {
            Destroy(transform.root.gameObject);
            return;
        }

        CurrentState.onChanged += HandleStateChanged;
    }

    protected override void OnSpawned()
    {
        base.OnSpawned();
        DontDestroyOnLoad(transform.root.gameObject);
        HandleStateChanged(CurrentState.value);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        CurrentState.onChanged -= HandleStateChanged;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void TriggerTransit(string sceneName)
    {
        if (_isTransitInProgress)
        {
            AppLog.LogWarning(LogChannel.Match, "Transit already in progress!", this);
            return;
        }

        bool isOffline = !NetworkManager.main || NetworkManager.main.isOffline;
        bool isCurrentHost = isServer || (NetworkManager.main && NetworkManager.main.isServer);
        if (!isCurrentHost && !isOffline)
        {
            AppLog.LogWarning(LogChannel.Match, "Only the server/host can trigger transit!", this);
            return;
        }

        _targetSceneName = sceneName;
        _isTransitInProgress = true;
        StartTransitRoutine().Forget();
    }

    private async UniTaskVoid StartTransitRoutine()
    {
        AppLog.Log(LogChannel.Match, $"Beginning departure sequence to '{_targetSceneName}'...", this);

        ChangeState(TransitState.Departing);
        await UniTask.Delay(TimeSpan.FromSeconds(_departureDuration));

        ChangeState(TransitState.InTransit);

        if (NetworkSceneCoordinator.Instance)
        {
            AppLog.Log(LogChannel.Match, $"Transitioning scene via NetworkSceneCoordinator: {_targetSceneName}", this);
            await NetworkSceneCoordinator.Instance.TransitionToSceneAsync(_targetSceneName);
        }
        else
        {
            AppLog.LogWarning(LogChannel.Match, "NetworkSceneCoordinator not found. Falling back to SceneManager.LoadSceneAsync.", this);
            await UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(_targetSceneName);
        }

        await UniTask.Delay(5000);

        ChangeState(TransitState.Arriving);
        await UniTask.Delay(TimeSpan.FromSeconds(_arrivalDuration));

        ChangeState(TransitState.Arrived);
        _isTransitInProgress = false;

        AppLog.Log(LogChannel.Match, "Train has arrived at station. Infil ready!", this);
    }

    private void ChangeState(TransitState newState)
    {
        bool isCurrentHost = isServer || (NetworkManager.main && NetworkManager.main.isServer) ||
                             !NetworkManager.main || NetworkManager.main.isOffline;
        if (isCurrentHost)
        {
            if (isSpawned)
            {
                CurrentState.value = newState;
            }
            else
            {
                HandleStateChanged(newState);
            }
        }
    }

    private void HandleStateChanged(TransitState newState)
    {
        AppLog.Log(LogChannel.Match, $"Transit state changed to: {newState}", this);
        OnTransitStateChanged?.Invoke(newState);

        switch (newState)
        {
            case TransitState.DockedAtHub:
                SetDoorsOpen(true);
                SetTransitVfxActive(false);
                break;

            case TransitState.Departing:
                SetDoorsOpen(false);
                _transitionEvents.RaiseOnTransitionStarted();
                // Play departure horn and initial rumble audio
                break;

            case TransitState.InTransit:
                SetDoorsOpen(false);
                SetTransitVfxActive(true);
                // Play continuous rumble audio loop
                break;

            case TransitState.Arriving:
                SetTransitVfxActive(false);
                _transitionEvents.RaiseOnTransitionCompleted();
                // Play screeching brakes audio
                break;

            case TransitState.Arrived:
                SetDoorsOpen(true);
                break;
        }
    }

    private void SetDoorsOpen(bool open)
    {
        if (open)
        {
            OnDoorsOpened?.Invoke();
        }
        else
        {
            OnDoorsClosed?.Invoke();
        }
    }

    private void SetTransitVfxActive(bool active)
    {
        if (_inTransitVFX)
        {
            _inTransitVFX.SetActive(active);
        }
    }
}
}