using System.Collections.Generic;
using PrimeTween;
using Project.Core.Events;
using Project.Core.Match;
using Project.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Match
{
public class RouteSelectionUI : InputBlockerUI
{
    [Header("References")] 
    [SerializeField] private RouteSelectionEventChannel _routeSelectionEventChannel;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GameObject _rootContainer;
    [SerializeField] private List<RouteSelectionButtonUI> _routeButtons = new();
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TMP_Text _selectedRouteLabel;

    [Header("Visual Settings")] [SerializeField]
    private float _fadeDuration = 0.3f;

    private Tween _fadeTween;
    private TrainRouteConsole _console;
    private RouteSelectionButtonUI _selectedRouteButton;
    private bool _hasRouteSelected => _selectedRouteButton;

    private void Awake()
    {
        _registerOnEnable = false;

        if (!_canvasGroup)
            _canvasGroup = GetComponent<CanvasGroup>();

        if (!_routeSelectionEventChannel)
        {
            _routeSelectionEventChannel =
                Resources.Load<RouteSelectionEventChannel>("Event Channels/RouteSelectionChannel");
        }

        if (_routeButtons == null || _routeButtons.Count == 0)
        {
            _routeButtons = new List<RouteSelectionButtonUI>(GetComponentsInChildren<RouteSelectionButtonUI>(true));
        }

        foreach (var button in _routeButtons)
        {
            if (button)
                button.OnButtonClicked += HandleRouteButtonClicked;
        }

        if (_confirmButton)
            _confirmButton.onClick.AddListener(ConfirmRouteSelection);

        if (_closeButton)
            _closeButton.onClick.AddListener(Close);

        UpdateSelectionState();

        if (_canvasGroup)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        if (_rootContainer)
        {
            _rootContainer.SetActive(false);
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        foreach (var button in _routeButtons)
        {
            if (button)
                button.OnButtonClicked -= HandleRouteButtonClicked;
        }

        if (_confirmButton)
            _confirmButton.onClick.RemoveListener(ConfirmRouteSelection);

        if (_closeButton)
            _closeButton.onClick.RemoveListener(Close);
    }

    protected override void OnEnable()
    {
        if (_routeSelectionEventChannel)
            _routeSelectionEventChannel.OnConsoleInteracted += HandleConsoleInteracted;
        
        base.OnEnable();
    }

    protected override void OnDisable()
    {
        if (_routeSelectionEventChannel)
            _routeSelectionEventChannel.OnConsoleInteracted -= HandleConsoleInteracted;
        
        base.OnDisable();
    }

    private void HandleConsoleInteracted(TrainRouteConsole console)
    {
        BindConsole(console);
        Open();
    }

    private void HandleRouteButtonClicked(RouteSelectionButtonUI button)
    {
        if (_selectedRouteButton)
        {
            _selectedRouteButton.MakeDeselected();
        }

        _selectedRouteButton = button;
        if (_selectedRouteButton)
        {
            _selectedRouteButton.MakeSelected();
        }

        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        if (_confirmButton)
        {
            _confirmButton.interactable = _hasRouteSelected;
        }

        if (_selectedRouteLabel)
        {
            _selectedRouteLabel.text = _hasRouteSelected
                ? $"Selected: {_selectedRouteButton.DisplayName}"
                : "Select a Destination";
        }
    }

    public void ConfirmRouteSelection()
    {
        if (!_hasRouteSelected || !_console)
            return;

        _console.LaunchRoute(_selectedRouteButton.SceneName);
        Close();
    }

    public override void Open()
    {
        base.Open();
        Show();
    }

    public override void Close()
    {
        base.Close();
        Hide();
        UnbindConsole();
    }
    
    private void Show()
    {
        if (!_canvasGroup) return;

        _fadeTween.Stop();
        if (_rootContainer) _rootContainer.SetActive(true);

        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = true;

        _fadeTween = Tween.Alpha(_canvasGroup, 0f, 1f, _fadeDuration);
    }

    private void Hide()
    {
        if (!_canvasGroup) return;

        _fadeTween.Stop();

        if (!gameObject.activeInHierarchy)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            if (_rootContainer) _rootContainer.SetActive(false);
            return;
        }

        _fadeTween = Tween.Alpha(_canvasGroup, _canvasGroup.alpha, 0f, _fadeDuration).OnComplete(() =>
        {
            if (_rootContainer) _rootContainer.SetActive(false);
        });

        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }
    
    private void BindConsole(TrainRouteConsole console)
    {
        _console = console;
    }

    private void UnbindConsole()
    {
        _console = null;
    }
}
}