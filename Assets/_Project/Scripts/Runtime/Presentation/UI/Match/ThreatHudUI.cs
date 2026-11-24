using Project.Core.Raid;
using Project.Match;
using TMPro;
using UnityEngine;

namespace Project.UI.Match
{
/// <summary>
/// Raid HUD line for the police threat ("POLICE ARRIVING 0:45", "ASSAULT 2", "BREAK 0:20", "FINAL PUSH").
/// Reads <see cref="ThreatStatus.Current"/> and only shows inside a live raid. An empty text hides that phase.
/// </summary>
public class ThreatHudUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _label;

    [Header("Text per phase. Tokens: {time} (m:ss), {assault}, {enemies}")]
    [SerializeField] private string _stealthText = "";
    [SerializeField] private string _alarmedText = "POLICE ARRIVING  {time}";
    [SerializeField] private string _assaultText = "ASSAULT {assault}  {time}";
    [SerializeField] private string _breakText = "BREAK  {time}";
    [SerializeField] private string _finalPushText = "FINAL PUSH";

    private bool _visible = true;
    private ThreatPhase _shownPhase;
    private int _shownSeconds = -1;
    private int _shownAssault = -1;
    private int _shownEnemies = -1;

    private void Awake()
    {
        if (!_group && !TryGetComponent(out _group))
            _group = gameObject.AddComponent<CanvasGroup>();

        _group.blocksRaycasts = false;
        _group.interactable = false;
        SetVisible(false);
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

        if (!_visible || threat.Phase != _shownPhase || ValuesChanged(threat))
            Refresh(threat, template);

        SetVisible(true);
    }

    // The threat directors live in persistent core services, so a status also exists in the hub.
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

    // Only runs when a shown value changes (about once a second), so building the string here is fine.
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

    private static string FormatTime(int seconds) =>
        seconds <= 0 ? string.Empty : $"{seconds / 60}:{seconds % 60:00}";

    private void SetVisible(bool visible)
    {
        if (_visible == visible) return;

        _visible = visible;
        _group.alpha = visible ? 1f : 0f;
    }
}
}
