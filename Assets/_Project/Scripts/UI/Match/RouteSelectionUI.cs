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
    [SerializeField] private GameObject _rootPanel;
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
        base.OnEnable();
        if (_routeSelectionEventChannel)
            _routeSelectionEventChannel.OnConsoleInteracted += HandleConsoleInteracted;

        HideInstantly();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_routeSelectionEventChannel)
            _routeSelectionEventChannel.OnConsoleInteracted -= HandleConsoleInteracted;

        _fadeTween.Stop();
        HideInstantly();
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
        if (_rootPanel) _rootPanel.SetActive(true);

        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = true;

        _fadeTween = Tween.Alpha(_canvasGroup, 0f, 1f, _fadeDuration);
    }

    private void Hide()
    {
        if (!_canvasGroup) return;

        _fadeTween.Stop();

        _fadeTween = Tween.Alpha(_canvasGroup, _canvasGroup.alpha, 0f, _fadeDuration).OnComplete(() =>
        {
            if (_rootPanel) _rootPanel.SetActive(false);
        });

        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    private void HideInstantly()
    {
        base.Close();
        _fadeTween.Stop();
        if (!_canvasGroup) return;

        if (_rootPanel) 
            _rootPanel.SetActive(false);

        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
        _canvasGroup.alpha = 0f;
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