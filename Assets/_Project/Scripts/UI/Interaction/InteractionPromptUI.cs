using System.Collections;
using Project.Core.Interaction;
using Project.Player;
using Project.Player.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Interaction
{
public class InteractionPromptUI : MonoBehaviour
{
    [Header("References")] 
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

    [Header("Colors & Styling")]
    [SerializeField] private Color _enabledPromptColor = Color.white;
    [SerializeField] private Color _disabledPromptColor = new Color(0.65f, 0.65f, 0.65f, 0.75f);
    [SerializeField] private Color _disabledReasonColor = new Color(1f, 0.35f, 0.35f, 1f);

    private PlayerInteractor _interactor;
    private Coroutine _shakeRoutine;

    private void OnEnable()
    {
        HidePrompt();

        // Bind to existing player if already spawned
        if (LocalPlayerContext.HasLocalPlayer && LocalPlayerContext.Player)
            BindInteractor(LocalPlayerContext.Player.Interactor);

        LocalPlayerContext.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
        LocalPlayerContext.OnLocalPlayerDespawned += HandleLocalPlayerDespawned;
    }

    private void OnDisable()
    {
        LocalPlayerContext.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
        LocalPlayerContext.OnLocalPlayerDespawned -= HandleLocalPlayerDespawned;

        UnbindInteractor();
        HidePrompt();
    }

    private void Update()
    {
        if (_interactor && _interactor.CurrentTarget != null)
        {
            RefreshPromptDisplay(_interactor.CurrentTarget);
        }
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
        _interactor.OnInteractionDenied += HandleInteractionDenied;

        // Immediately reflect current state
        HandleTargetChanged(_interactor.CurrentTarget);
    }

    private void UnbindInteractor()
    {
        if (_interactor)
        {
            _interactor.OnTargetChanged -= HandleTargetChanged;
            _interactor.OnHoldProgressChanged -= HandleHoldProgressChanged;
            _interactor.OnInteractionDenied -= HandleInteractionDenied;
            _interactor = null;
        }
    }

    private void HandleHoldProgressChanged(float progress)
    {
        if (_progressBarFill)
            _progressBarFill.fillAmount = Mathf.Clamp01(progress);

        // Dynamically show the progress bar while holding
        if (_progressBarContainer)
            _progressBarContainer.SetActive(progress > 0f);
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
        RefreshPromptDisplay(target);

        if (_progressBarContainer)
            _progressBarContainer.SetActive(false);

        if (_progressBarFill)
            _progressBarFill.fillAmount = 0f;

        SetRootVisibility(true);
    }

    private void RefreshPromptDisplay(IInteractable target)
    {
        if (target == null)
        {
            HidePrompt();
            return;
        }

        bool isDual = target.Mode == InteractionMode.DualInstantAndHold;
        bool isHold = target.Mode == InteractionMode.Hold;
        bool hasInstantPrompt = !isHold && !string.IsNullOrEmpty(target.InteractionPrompt);
        bool hasHoldPrompt = (isDual || isHold) && !string.IsNullOrEmpty(target.HoldPrompt);

        // Target Name & Accent Color
        bool hasTargetName = !string.IsNullOrEmpty(target.TargetName);
        if (_targetNameText)
        {
            _targetNameText.gameObject.SetActive(hasTargetName);
            if (hasTargetName)
            {
                _targetNameText.text = target.TargetName;
                if (target.AccentColor != default && target.AccentColor.a > 0.01f)
                    _targetNameText.color = target.AccentColor;
                else
                    _targetNameText.color = Color.white;
            }
        }

        // Subtext
        bool hasSubtext = !string.IsNullOrEmpty(target.Subtext);
        if (_subtextText)
        {
            _subtextText.gameObject.SetActive(hasSubtext);
            if (hasSubtext)
            {
                _subtextText.text = target.Subtext;
            }
        }

        // Tap / Instant Prompt
        if (_promptTextContainer)
        {
            _promptTextContainer.SetActive(hasInstantPrompt);
            if (hasInstantPrompt && _promptText)
            {
                bool canTap = target.CanInteract(_interactor, isHold: false);
                string disabledReason = target.GetDisabledReason(_interactor, isHold: false);

                if (canTap)
                {
                    _promptText.text = target.InteractionPrompt;
                    _promptText.color = _enabledPromptColor;
                }
                else
                {
                    string reasonTag = !string.IsNullOrEmpty(disabledReason)
                        ? $" <color=#{ColorUtility.ToHtmlStringRGB(_disabledReasonColor)}>({disabledReason})</color>"
                        : string.Empty;
                    _promptText.text = $"{target.InteractionPrompt}{reasonTag}";
                    _promptText.color = _disabledPromptColor;
                }
            }
        }

        // Hold Prompt
        if (_holdPromptTextContainer)
        {
            _holdPromptTextContainer.SetActive(hasHoldPrompt);
            if (hasHoldPrompt && _holdPromptText)
            {
                bool canHold = target.CanInteract(_interactor, isHold: true);
                string disabledReason = target.GetDisabledReason(_interactor, isHold: true);

                if (canHold)
                {
                    _holdPromptText.text = target.HoldPrompt;
                    _holdPromptText.color = _enabledPromptColor;
                }
                else
                {
                    string reasonTag = !string.IsNullOrEmpty(disabledReason)
                        ? $" <color=#{ColorUtility.ToHtmlStringRGB(_disabledReasonColor)}>({disabledReason})</color>"
                        : string.Empty;
                    _holdPromptText.text = $"{target.HoldPrompt}{reasonTag}";
                    _holdPromptText.color = _disabledPromptColor;
                }
            }
        }
    }

    private void HandleInteractionDenied(IInteractable target, string reason, bool isHold)
    {
        if (!gameObject.activeInHierarchy) return;

        if (_shakeRoutine != null)
            StopCoroutine(_shakeRoutine);

        Transform shakeTarget = (isHold && _holdPromptTextContainer)
            ? _holdPromptTextContainer.transform
            : (_promptTextContainer ? _promptTextContainer.transform : (_rootContainer ? _rootContainer.transform : null));

        if (shakeTarget)
            _shakeRoutine = StartCoroutine(DoShake(shakeTarget));
    }

    private IEnumerator DoShake(Transform tr)
    {
        Vector3 origPos = tr.localPosition;
        float elapsed = 0f;
        float duration = 0.18f;
        float magnitude = 6f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float xOffset = Mathf.Sin(elapsed * 50f) * magnitude * (1f - (elapsed / duration));
            tr.localPosition = new Vector3(origPos.x + xOffset, origPos.y, origPos.z);
            yield return null;
        }

        tr.localPosition = origPos;
        _shakeRoutine = null;
    }

    private void HidePrompt()
    {
        if (_promptTextContainer) _promptTextContainer.SetActive(false);
        if (_holdPromptTextContainer) _holdPromptTextContainer.SetActive(false);
        if (_progressBarContainer) _progressBarContainer.SetActive(false);
        if (_progressBarFill) _progressBarFill.fillAmount = 0f;
        if (_targetNameText) _targetNameText.gameObject.SetActive(false);
        if (_subtextText) _subtextText.gameObject.SetActive(false);

        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }

        SetRootVisibility(false);
    }

    private void SetRootVisibility(bool visible)
    {
        if (_canvasGroup)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }

        if (_rootContainer && _rootContainer != gameObject && _rootContainer.activeSelf != visible)
        {
            _rootContainer.SetActive(visible);
        }
    }
}
}