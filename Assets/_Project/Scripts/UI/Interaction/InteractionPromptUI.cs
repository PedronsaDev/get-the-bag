using Project.Core.Interaction;
using Project.Player;
using Project.Player.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
public class InteractionPromptUI : MonoBehaviour
{
    [Header("Primary References")] 
    [SerializeField] private GameObject _rootContainer;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GameObject _promptTextContainer;
    [SerializeField] private TMP_Text _promptText;
    [SerializeField] private GameObject _holdPromptTextContainer;
    [SerializeField] private TMP_Text _holdPromptText;
    [SerializeField] private GameObject _progressBarContainer;
    [SerializeField] private Image _progressBarFill;

    [Header("Optional Secondary Text")] 
    [SerializeField] private TMP_Text _targetNameText;
    [SerializeField] private TMP_Text _subtextText;

    private PlayerInteractor _interactor;

    private void Awake()
    {
        HidePrompt();

        if (LocalPlayerContext.HasLocalPlayer && LocalPlayerContext.Player)
            BindInteractor(LocalPlayerContext.Player.Interactor);

        LocalPlayerContext.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
        LocalPlayerContext.OnLocalPlayerDespawned += HandleLocalPlayerDespawned;
    }

    private void OnDestroy()
    {
        LocalPlayerContext.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
        LocalPlayerContext.OnLocalPlayerDespawned -= HandleLocalPlayerDespawned;

        UnbindInteractor();
    }
    
    // Player binding
    private void HandleLocalPlayerSpawned(PlayerFacade player)
    {
        if (player)
            BindInteractor(player.Interactor);
    }

    private void HandleLocalPlayerDespawned(PlayerFacade player)
    {
        UnbindInteractor();
        HidePrompt();
    }

    private void BindInteractor(PlayerInteractor playerInteractor)
    {
        if (_interactor == playerInteractor)
            return;

        UnbindInteractor();

        if (!playerInteractor)
            return;

        _interactor = playerInteractor;
        _interactor.OnTargetChanged += HandleTargetChanged;
        _interactor.OnHoldProgressChanged += HandleHoldProgressChanged;

        HandleTargetChanged(_interactor.CurrentTarget);
    }

    private void UnbindInteractor()
    {
        if (_interactor)
        {
            _interactor.OnTargetChanged -= HandleTargetChanged;
            _interactor.OnHoldProgressChanged -= HandleHoldProgressChanged;
            _interactor = null;
        }
    }
    
    // Interaction handling
    private void HandleHoldProgressChanged(float progress)
    {
        _progressBarFill.fillAmount = Mathf.Clamp01(progress);
    }

    private void HandleTargetChanged(IInteractable target)
    {
        if (target != null)
            ShowPrompt(target);
        else
            HidePrompt();
    }

    private void ShowPrompt(IInteractable target)
    {
        bool isDual = target.Mode == InteractionMode.DualInstantAndHold;
        bool isHold = target.Mode == InteractionMode.Hold;
        bool hasHoldPrompt = isDual || target.Mode == InteractionMode.Hold;

        if (!string.IsNullOrEmpty(target.InteractionPrompt) && !isHold)
        {
            _promptTextContainer.SetActive(true);
            _promptText.text = target.InteractionPrompt;
        }

        if (hasHoldPrompt && !string.IsNullOrEmpty(target.HoldPrompt))
        {
            _holdPromptTextContainer.SetActive(true);
            _holdPromptText.text = target.HoldPrompt;
        }

        SetRootVisibility(true);
    }

    private void HidePrompt()
    {
        _promptTextContainer.SetActive(false);
        _holdPromptText.gameObject.SetActive(false);
        _holdPromptTextContainer.SetActive(false);
        _targetNameText.gameObject.SetActive(false);
        _subtextText.gameObject.SetActive(false);
        _progressBarContainer.SetActive(false);
        
        _progressBarFill.fillAmount = 0f;
        
        SetRootVisibility(false);
    }

    private void SetRootVisibility(bool visible)
    {
        _canvasGroup.alpha = visible ? 1f : 0f;
        _rootContainer.SetActive(visible);
    }
}
}