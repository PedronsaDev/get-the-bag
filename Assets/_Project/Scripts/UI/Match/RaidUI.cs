using System;
using Project.Core.Events;
using Project.Core.Match;
using TMPro;
using UnityEngine;

namespace Project.UI.Match
{
public class RaidUI : MonoBehaviour
{
    private static readonly Color SoftRed = new(1f, 0.35f, 0.35f, 1f);

    [Header("References")] 
    [SerializeField] private RaidEventChannel _raidEventChannel;
    [SerializeField] private GameObject _timePanelRoot;
    [SerializeField] private TMP_Text _timeText;
    
    private RaidManager _raidManager;

    private void Awake()
    {
        if (!_raidEventChannel)
        {
            _raidEventChannel = Resources.Load<RaidEventChannel>("Event Channels/RaidEventChannel");
        }
    }

    private void OnEnable()
    {
        if (_raidEventChannel)
        {
            _raidEventChannel.OnRaidManagerUpdated += OnRaidManagerUpdated;
            _raidEventChannel.OnStateChanged += OnStateChanged;
            _raidEventChannel.OnRaidFailed += OnRaidFailed;
            _raidEventChannel.OnRaidCompleted += OnRaidCompleted;
        }

        if (!_raidManager && RaidManager.Instance)
        {
            _raidManager = RaidManager.Instance;
        }

        UpdatePanelVisibility(_raidManager ? _raidManager.CurrentState.value : RaidState.WaitingAtPlatform);
    }

    private void OnDisable()
    {
        if (_raidEventChannel)
        {
            _raidEventChannel.OnRaidManagerUpdated -= OnRaidManagerUpdated;
            _raidEventChannel.OnStateChanged -= OnStateChanged;
            _raidEventChannel.OnRaidFailed -= OnRaidFailed;
            _raidEventChannel.OnRaidCompleted -= OnRaidCompleted;
        }
    }

    private void Update()
    {
        if (_raidManager && _raidManager.CurrentState.value is RaidState.InRaid or RaidState.ExtractionAvailable or RaidState.ExtractionHoldout)
        {
            float seconds = Mathf.Max(0f, _raidManager.TimeRemaining.value);
            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
            if (_timeText)
            {
                _timeText.text = timeSpan.ToString(@"mm\:ss");
                _timeText.color = seconds < 300f ? SoftRed : Color.white;
            }
        }
    }

    private void OnRaidManagerUpdated(RaidManager raidManager)
    {
        _raidManager = raidManager;
        UpdatePanelVisibility(_raidManager ? _raidManager.CurrentState.value : RaidState.WaitingAtPlatform);
    }

    private void OnStateChanged(RaidState newState)
    {
        UpdatePanelVisibility(newState);
    }

    private void UpdatePanelVisibility(RaidState state)
    {
        if (_timePanelRoot)
        {
            _timePanelRoot.SetActive(state is RaidState.InRaid or RaidState.ExtractionAvailable or RaidState.ExtractionHoldout);
        }
    }

    private void OnRaidFailed()
    {
        
    }

    private void OnRaidCompleted()
    {
        
    }
}
}