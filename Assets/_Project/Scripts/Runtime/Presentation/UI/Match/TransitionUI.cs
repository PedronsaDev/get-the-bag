using PrimeTween;
using Project.Core;
using Project.Core.UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace Project.UI.Match
{
public class TransitionUI : InputBlockerUI
{
    [Header("References")]
    [SerializeField] private GameObject _rootContainer;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TransitionEventChannel _transitionEvents;

    [Header("Settings")]
    [Tooltip("Seconds to fade in. Scene loading waits for this, so keep it short.")]
    [SerializeField, Min(0f)] private float _fadeInDuration = 0.3f;
    [Tooltip("Seconds to fade out and reveal the new scene.")]
    [FormerlySerializedAs("_fadeDuration")]
    [SerializeField, Min(0f)] private float _fadeOutDuration = 0.5f;

    private Tween _fade;

    protected override void OnEnable()
    {
        base.OnEnable();

        _transitionEvents.FadeInSeconds = _fadeInDuration;
        _transitionEvents.OnTransitionStarted += HandleTransitionStarted;
        _transitionEvents.OnTransitionCompleted += HandleTransitionCompleted;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        _transitionEvents.OnTransitionStarted -= HandleTransitionStarted;
        _transitionEvents.OnTransitionCompleted -= HandleTransitionCompleted;
    }

    private void HandleTransitionStarted() => Open();

    private void HandleTransitionCompleted() => Close();

    public override void Open()
    {
        base.Open();
        Show();
    }

    public override void Close()
    {
        base.Close();
        Hide();
    }

    private void Hide()
    {
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        _fade.Stop();
        _fade = Tween.Alpha(_canvasGroup, _canvasGroup.alpha, 0f, _fadeOutDuration, useUnscaledTime: true)
            .OnComplete(() => _rootContainer.SetActive(false));
    }

    private void Show()
    {
        bool wasHidden = !_rootContainer.activeSelf;
        _rootContainer.SetActive(true);

        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;

        _fade.Stop();
        float from = wasHidden ? 0f : _canvasGroup.alpha;
        if (_fadeInDuration <= 0f) _canvasGroup.alpha = 1f;
        else _fade = Tween.Alpha(_canvasGroup, from, 1f, _fadeInDuration, useUnscaledTime: true);
    }
}
}
