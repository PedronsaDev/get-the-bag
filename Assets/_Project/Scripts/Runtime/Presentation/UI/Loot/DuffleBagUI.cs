using PrimeTween;
using Project.Player;
using Project.Player.Loot;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Loot
{
public class DuffleBagUI : MonoBehaviour
{
    [Header("Containers")] 
    [SerializeField] private GameObject _bagRootContainer;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Text Displays")]
    [SerializeField] private TMP_Text _valueText;
    [SerializeField] private TMP_Text _weightText;

    [Header("Weight Progress Bar")] 
    [SerializeField] private Image _weightFillBar;
    
    [Header("Throw Progress Bar")]
    [SerializeField] private Image _throwFillBar;
    
    [Header("Visual Settings")]
    [SerializeField] private Gradient _weightBarGradient;
    [SerializeField] private float _fadeDuration;
    
    private PlayerDuffleBagController _controller;
    private Tween _fadeTween;

    private void OnEnable()
    {
        if (LocalPlayerContext.HasLocalPlayer && LocalPlayerContext.Player)
            Bind(LocalPlayerContext.Player.DuffleBagController);
        
        LocalPlayerContext.OnLocalPlayerSpawned += HandlePlayerSpawned;
        LocalPlayerContext.OnLocalPlayerDespawned += HandlePlayerDespawned;
    }

    private void OnDisable()
    {
        LocalPlayerContext.OnLocalPlayerSpawned -= HandlePlayerSpawned;
        LocalPlayerContext.OnLocalPlayerDespawned -= HandlePlayerDespawned;
        Unbind();
    }

    private void HandlePlayerSpawned(PlayerFacade player)
    {
        if (player) 
            Bind(player.DuffleBagController);
    }

    private void HandlePlayerDespawned(PlayerFacade player)
    {
        Unbind();
    }

    private void Bind(PlayerDuffleBagController controller)
    {
        Unbind();
        _controller = controller;
        if (!_controller) 
            return;
        
        _controller.OnBagStateChanged += RefreshDisplay;
        _controller.OnWeightChanged += HandleWeightChanged;
        _controller.OnValueChanged += HandleValueChanged;
        RefreshDisplay();
    }

    private void Unbind()
    {
        if (!_controller) 
            return;
        
        _controller.OnBagStateChanged -= RefreshDisplay;
        _controller.OnWeightChanged -= HandleWeightChanged;
        _controller.OnValueChanged -= HandleValueChanged;
        _controller = null;
    }

    private void RefreshDisplay()
    {
        if (!_controller || !_controller.HasBag)
        {
            HideIndicator();
            return;
        }

        ShowIndicator();
        HandleWeightChanged(_controller.CurrentWeight, _controller.MaxCapacityKg);
        HandleValueChanged(_controller.CurrentValue);
    }

    private void HandleWeightChanged(float currentWeight, float maxWeight)
    {
        if (_weightText)
            _weightText.text = $"{currentWeight:F1} / {maxWeight:F1} kg";
        float ratio = maxWeight > 0.01f ? Mathf.Clamp01(currentWeight / maxWeight) : 0f;
        if (_weightFillBar)
        {
            _weightFillBar.fillAmount = ratio;
            if (_weightBarGradient != null)
            {
                _weightFillBar.color = _weightBarGradient.Evaluate(ratio);
            }
        }
    }

    private void HandleValueChanged(int totalValue)
    {
        if (_valueText)
            _valueText.text = $"${totalValue:N0}";
    }

    private void ShowIndicator()
    {
        if (!_bagRootContainer)
            return;
        
        StopAnimations();
        
        _bagRootContainer.SetActive(true);
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = true;
        _fadeTween = Tween.Alpha(_canvasGroup, 1f, _fadeDuration);
    }

    private void HideIndicator()
    {
        if (!_bagRootContainer)
            return;
        
        StopAnimations();
        
        _fadeTween = Tween.Alpha(_canvasGroup, 0f, _fadeDuration).OnComplete(() =>
        {
            _bagRootContainer.SetActive(false);
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;    
        });
    }

    private void StopAnimations()
    {
        _fadeTween.Stop();
    }
}
}