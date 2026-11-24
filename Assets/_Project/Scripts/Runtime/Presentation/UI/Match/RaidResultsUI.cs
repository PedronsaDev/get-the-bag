using System.Text;
using Project.Content;
using Project.Logging;
using Project.Match;
using PurrNet;
using TMPro;
using UnityEngine;

namespace Project.UI.Match
{
/// <summary>
/// Post-raid results panel. Fills every text at once when <see cref="RaidEventChannel.OnResultsReady"/> fires,
/// updates the return countdown, and hides when the loading screen appears.
/// </summary>
public class RaidResultsUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RaidEventChannel _raidEventChannel;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Texts")]
    [SerializeField] private TMP_Text _outcomeText;
    [SerializeField] private TMP_Text _itemsText;
    [SerializeField] private TMP_Text _runValueText;
    [SerializeField] private TMP_Text _summaryText;
    [SerializeField] private TMP_Text _bonusText;
    [SerializeField] private TMP_Text _bankedText;
    [SerializeField] private TMP_Text _returnText;

    [Header("Outcome headers")]
    [SerializeField] private string _extractedHeader = "JOB COMPLETE";
    [SerializeField] private string _leftBehindHeader = "LEFT BEHIND";
    [SerializeField] private string _nobodyExtractedHeader = "NOBODY GOT OUT";
    [SerializeField] private string _wipedHeader = "CREW DOWN";
    [SerializeField] private string _timedOutHeader = "OUT OF TIME";
    [SerializeField] private string _noDataHeader = "RAID OVER";

    [Header("Alarm reasons, indexed by the AI's AlarmReason: GuardAlerted, BodyFound, CivilianCall, CivilianKilled")]
    [SerializeField] private string[] _alarmReasonTexts =
    {
        "A guard raised the alarm",
        "A body was found",
        "A civilian called the police",
        "A civilian was killed"
    };
    [SerializeField] private string _unknownAlarmReasonText = "The alarm was raised";
    [SerializeField] private string _noAlarmText = "No alarm raised";

    private bool _visible;
    private int _shownRunId;
    private int _shownReturnSeconds = -1;

    private void Awake()
    {
        if (!_raidEventChannel)
            _raidEventChannel = Resources.Load<RaidEventChannel>(RaidEventChannel.ResourcePath);

        if (!_canvasGroup && !TryGetComponent(out _canvasGroup))
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        SetVisible(false);
    }

    private void OnEnable()
    {
        if (_raidEventChannel)
            _raidEventChannel.OnResultsReady += HandleResultsReady;

        var raid = RaidManager.Instance;
        if (raid && raid.Results.value.IsValid)
            HandleResultsReady(raid.Results.value);
    }

    private void OnDisable()
    {
        if (_raidEventChannel)
            _raidEventChannel.OnResultsReady -= HandleResultsReady;
    }

    private void Update()
    {
        if (!_visible) return;

        if (LevelLoadingStatus.IsShowing)
        {
            Hide();
            return;
        }

        UpdateReturnText();
    }

    public void Hide() => SetVisible(false);

    private void HandleResultsReady(RaidResults results)
    {
        if (!results.IsValid)
        {
            AppLog.LogError(LogChannel.Match, $"[RaidResultsUI] Invalid raid results (outcome {results.Outcome}).", this);
            return;
        }

        if (results.RunId == _shownRunId) return;
        _shownRunId = results.RunId;

        Show(results);
    }

    private void Show(RaidResults results)
    {
        ClearTexts();

        PlayerID local = NetworkManager.main ? NetworkManager.main.localPlayer : default;
        bool hasPlayer = results.TryGetPlayer(local, out var me);

        SetText(_outcomeText, hasPlayer ? HeaderFor(results.Outcome, me.Extracted) : _noDataHeader);
        if (hasPlayer)
        {
            SetText(_itemsText, BuildItemsText(me));
            SetText(_runValueText, me.Extracted ? $"TAKE  ${me.RunValue:N0}" : $"TAKE  ${me.RunValue:N0}  LOST");
            SetText(_summaryText, BuildSummaryText(results));
            SetText(_bonusText, BuildBonusText(me));
            SetText(_bankedText, $"BANKED  ${me.BankedTotal:N0}");
        }

        _shownReturnSeconds = -1;
        SetVisible(true);
        UpdateReturnText();
    }

    private string HeaderFor(RaidOutcome outcome, bool extracted) => outcome switch
    {
        RaidOutcome.Extracted => extracted ? _extractedHeader : _leftBehindHeader,
        RaidOutcome.NobodyExtracted => _nobodyExtractedHeader,
        RaidOutcome.Wiped => _wipedHeader,
        RaidOutcome.TimedOut => _timedOutHeader,
        _ => _noDataHeader
    };

    private static string BuildItemsText(RaidPlayerResult me)
    {
        int count = me.ItemIds?.Length ?? 0;
        if (count == 0) return "HAUL\nEmpty bag";

        var sb = new StringBuilder(me.Extracted ? "HAUL\n" : "HAUL (lost)\n");
        var registry = GameContent.LootRegistry;
        for (int i = 0; i < count; i++)
        {
            string id = me.ItemIds[i];
            int quantity = me.ItemQuantities != null && i < me.ItemQuantities.Length ? me.ItemQuantities[i] : 1;

            string name = id;
            int value = 0;
            if (registry && registry.TryGetById(id, out var definition))
            {
                name = definition.Name;
                value = definition.BaseValue * quantity;
            }

            sb.Append(name).Append("  x").Append(quantity).Append("   $").Append(value.ToString("N0")).Append('\n');
        }

        return sb.ToString().TrimEnd();
    }

    private string BuildSummaryText(RaidResults results)
    {
        if (results.Stealth)
            return $"STEALTH - {_noAlarmText}";

        return $"LOUD - {AlarmReasonText(results.AlarmReason)}\n" +
               $"Assaults survived {results.AssaultsReached}   Civilians killed {results.CivilianKills}";
    }

    private static string BuildBonusText(RaidPlayerResult me)
    {
        if (me.StealthBonus > 0) return $"Stealth bonus  +${me.StealthBonus:N0}";
        if (me.CivilianPenalty > 0) return $"Civilian penalty  -${me.CivilianPenalty:N0}";
        return string.Empty;
    }

    private string AlarmReasonText(byte reason)
    {
        bool known = _alarmReasonTexts != null && reason < _alarmReasonTexts.Length &&
                     !string.IsNullOrEmpty(_alarmReasonTexts[reason]);
        return known ? _alarmReasonTexts[reason] : _unknownAlarmReasonText;
    }

    private void UpdateReturnText()
    {
        if (!_returnText) return;

        int seconds = Mathf.CeilToInt(GetReturnSeconds());
        if (seconds == _shownReturnSeconds) return;

        _shownReturnSeconds = seconds;
        _returnText.text = seconds > 0 ? $"Back to the hideout in {seconds}" : string.Empty;
    }

    private float GetReturnSeconds()
    {
        var raid = RaidManager.Instance;
        if (raid) return raid.IsResolved ? raid.ReturnCountdown.value : 0f;
#if UNITY_EDITOR
        if (_previewReturnEnd > 0f) return Mathf.Max(0f, _previewReturnEnd - Time.unscaledTime);
#endif
        return 0f;
    }

    private void ClearTexts()
    {
        SetText(_outcomeText, string.Empty);
        SetText(_itemsText, string.Empty);
        SetText(_runValueText, string.Empty);
        SetText(_summaryText, string.Empty);
        SetText(_bonusText, string.Empty);
        SetText(_bankedText, string.Empty);
        SetText(_returnText, string.Empty);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text) text.text = value;
    }

    private void SetVisible(bool visible)
    {
        _visible = visible;
        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.blocksRaycasts = visible;
        _canvasGroup.interactable = visible;
    }

#if UNITY_EDITOR
    private float _previewReturnEnd = -1f;

    public void StartPreviewReturnCountdown(float seconds) => _previewReturnEnd = Time.unscaledTime + seconds;
#endif
}
}
