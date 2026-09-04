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
    [Tooltip("Optional root container or CanvasGroup to toggle the entire prompt UI.")]
    [SerializeField] private GameObject _rootContainer;

    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TMP_Text _promptText;
    [SerializeField] private GameObject _progressBarContainer;
    [SerializeField] private Image _progressBarFill;

    [Header("Optional Secondary Text")] 
    [SerializeField] private TMP_Text _targetNameText;
    [SerializeField] private TMP_Text _subtextText;

    [Header("Dual Interaction Prompts")]
    [Tooltip("Optional secondary prompt text for hold actions (e.g. '[Hold F] Equip'). If unassigned, dual prompts format dynamically into _promptText.")]
    [SerializeField] private TMP_Text _holdPromptText;

    [Tooltip("Optional container GameObject for the hold prompt row.")] 
    [SerializeField] private GameObject _holdPromptContainer;

    [Header("Visual Styling")] 
    [SerializeField] private Color _defaultAccentColor = new Color(1f, 0.85f, 0.2f, 1f);

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
        bool hasHoldPrompt =
            !string.IsNullOrEmpty(target.HoldPrompt) && (isDual || target.Mode == InteractionMode.Hold);

        if (_promptText)
        {
            if (!_targetNameText && !string.IsNullOrEmpty(target.TargetName))
            {
                if (hasHoldPrompt && !_holdPromptText)
                {
                    _promptText.SetText(
                        $"{target.InteractionPrompt} <color=white>{target.TargetName}</color>\n<size=85%><color=#D0D0D0>{target.HoldPrompt}</color></size>");
                }
                else
                {
                    _promptText.SetText($"{target.InteractionPrompt} <color=white>{target.TargetName}</color>");
                }
            }
            else
            {
                if (hasHoldPrompt && !_holdPromptText)
                {
                    _promptText.SetText(
                        $"{target.InteractionPrompt}\n<size=85%><color=#D0D0D0>{target.HoldPrompt}</color></size>");
                }
                else
                {
                    _promptText.SetText(target.InteractionPrompt);
                }
            }

            _promptText.gameObject.SetActive(true);
        }

        if (_holdPromptText)
        {
            _holdPromptText.gameObject.SetActive(hasHoldPrompt);
            if (hasHoldPrompt)
            {
                _holdPromptText.SetText(target.HoldPrompt);
            }
        }

        if (_holdPromptContainer)
        {
            _holdPromptContainer.SetActive(hasHoldPrompt);
        }

        if (_targetNameText)
        {
            bool hasName = !string.IsNullOrEmpty(target.TargetName);
            _targetNameText.gameObject.SetActive(hasName);
            if (hasName) _targetNameText.SetText(target.TargetName);
        }

        if (_subtextText)
        {
            bool hasSubtext = !string.IsNullOrEmpty(target.Subtext);
            _subtextText.gameObject.SetActive(hasSubtext);
            if (hasSubtext) _subtextText.SetText(target.Subtext);
        }

        bool isHoldable = target.Mode != InteractionMode.Instant && target.HoldDuration > 0f;
        if (_progressBarContainer)
        {
            _progressBarContainer.SetActive(isHoldable);
        }

        if (_progressBarFill)
        {
            _progressBarFill.fillAmount = 0f;

            Color accent = target.AccentColor;
            _progressBarFill.color = accent.a > 0.01f ? accent : _defaultAccentColor;
        }

        SetRootVisibility(true);
    }

    private void HidePrompt()
    {
        if (_promptText)
            _promptText.gameObject.SetActive(false);

        if (_holdPromptText)
            _holdPromptText.gameObject.SetActive(false);

        if (_holdPromptContainer)
            _holdPromptContainer.SetActive(false);

        if (_targetNameText)
            _targetNameText.gameObject.SetActive(false);

        if (_subtextText)
            _subtextText.gameObject.SetActive(false);

        if (_progressBarContainer)
            _progressBarContainer.SetActive(false);

        if (_progressBarFill)
            _progressBarFill.fillAmount = 0f;

        SetRootVisibility(false);
    }

    private void SetRootVisibility(bool visible)
    {
        if (_canvasGroup)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        if (_rootContainer)
        {
            _rootContainer.SetActive(visible);
        }
    }

    private void HandleHoldProgressChanged(float progress)
    {
        if (_progressBarFill)
        {
            _progressBarFill.fillAmount = Mathf.Clamp01(progress);
        }
    }
}
}