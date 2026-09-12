using PrimeTween;
using Project.Core;
using Project.Core.UI;
using UnityEngine;

namespace Project.UI
{
public class TransitionUI : InputBlockerUI
{
    [Header("References")] 
    [SerializeField] private GameObject _rootContainer;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TransitionEventChannel _transitionEvents;

    [Header("Settings")] 
    [SerializeField] private float _fadeDuration;

    protected override void OnEnable()
    {
        base.OnEnable();

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

        Tween.Alpha(_canvasGroup, 1f, 0f, _fadeDuration).OnComplete(() => { _rootContainer.SetActive(false); });
    }

    private void Show()
    {
        _rootContainer.SetActive(true);

        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;

        Tween.Alpha(_canvasGroup, 0f, 1f, _fadeDuration);
    }
}
}