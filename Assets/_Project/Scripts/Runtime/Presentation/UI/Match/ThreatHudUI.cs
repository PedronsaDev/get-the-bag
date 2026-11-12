using PrimeTween;
using Project.Core.Raid;
using Project.Match;
using TMPro;
using UnityEngine;

namespace Project.UI.Match
{
/// <summary>
/// Raid HUD banner for the police threat: "POLICE ARRIVING 0:45", "ASSAULT 2", "BREAK 0:20", "FINAL PUSH".
/// Reads <see cref="ThreatStatus.Current"/> (synced by the AI) and only shows inside a live raid.
/// An empty template hides the banner for that phase.
/// </summary>
public class ThreatHudUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _label;

    [Header("Text per phase. Tokens: {time} (m:ss), {assault}, {enemies}. TMPEffects looping tags work.")]
    [SerializeField] private string _stealthText = "";
    [SerializeField] private string _alarmedText = "<color=#E0A040>POLICE ARRIVING</color>  {time}";
    [SerializeField] private string _assaultText = "<color=#E04040><shake a=0.6>ASSAULT {assault}</shake></color>  <size=70%>{time}</size>";
    [SerializeField] private string _breakText = "<color=#AAAAAA>BREAK</color>  {time}";
    [SerializeField] private string _finalPushText = "<color=#E04040><wave>FINAL PUSH</wave></color>";

    [Header("Animation (unscaled seconds)")]
    [SerializeField, Min(0f)] private float _fadeSeconds = 0.25f;
    [SerializeField] private float _phasePunch = 0.15f;
    [SerializeField, Min(0.01f)] private float _phasePunchSeconds = 0.35f;

    private bool _visible;
    private ThreatPhase _shownPhase;
    private int _shownSeconds = -1;
    private int _shownAssault = -1;
    private int _shownEnemies = -1;
    private Tween _fade;

    private void Awake()
    {
        if (!_group && !TryGetComponent(out _group))
            _group = gameObject.AddComponent<CanvasGroup>();

        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
    }

    private void Update()
    {
        var threat = ThreatStatus.Current;
        string template = threat != null && IsInsideLiveRaid() ? TemplateFor(threat.Phase) : null;

        if (string.IsNullOrEmpty(template))
        {
            SetVisible(false);
            return;
        }

        bool phaseChanged = threat.Phase != _shownPhase || !_visible;
        if (phaseChanged || ValuesChanged(threat))
            Refresh(threat, template);

        if (phaseChanged && _visible && _phasePunch != 0f)
            Tween.PunchScale(transform, Vector3.one * _phasePunch, _phasePunchSeconds, useUnscaledTime: true);

        SetVisible(true);
    }

    // The threat directors live in persistent core services, so a status exists in the hub too.
    private static bool IsInsideLiveRaid() => RaidManager.Instance && RaidLifecycle.AllowsAI;

    private string TemplateFor(ThreatPhase phase) => phase switch
    {
        ThreatPhase.Stealth => _stealthText,
        ThreatPhase.Alarmed => _alarmedText,
        ThreatPhase.Assault => _assaultText,
        ThreatPhase.Break => _breakText,
        ThreatPhase.FinalPush => _finalPushText,
        _ => null
    };

    private bool ValuesChanged(IThreatStatus threat) =>
        Mathf.CeilToInt(threat.SecondsInPhase) != _shownSeconds ||
        threat.AssaultNumber != _shownAssault ||
        threat.EnemiesAlive != _shownEnemies;

    // Runs only when a shown value changes (about once a second), so the string work here is not per frame.
    private void Refresh(IThreatStatus threat, string template)
    {
        _shownPhase = threat.Phase;
        _shownSeconds = Mathf.CeilToInt(threat.SecondsInPhase);
        _shownAssault = threat.AssaultNumber;
        _shownEnemies = threat.EnemiesAlive;

        if (!_label) return;

        _label.text = template
            .Replace("{time}", FormatTime(_shownSeconds))
            .Replace("{assault}", _shownAssault.ToString())
            .Replace("{enemies}", _shownEnemies.ToString());
    }

    private static string FormatTime(int seconds)
    {
        if (seconds <= 0) return string.Empty;
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    private void SetVisible(bool visible)
    {
        if (_visible == visible) return;

        _visible = visible;
        _fade.Stop();
        _fade = Tween.Alpha(_group, visible ? 1f : 0f, _fadeSeconds, useUnscaledTime: true);
    }
}
}
