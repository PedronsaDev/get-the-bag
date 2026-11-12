using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using Project.Content;
using Project.Match;
using Project.Logging;
using Project.UI.Match.Reveal;
using PurrNet;
using UnityEngine;

namespace Project.UI.Match
{
/// <summary>
/// Shows the post-raid results. Turns <see cref="RaidResults"/> into a <see cref="RevealContext"/>
/// and hands it to the <see cref="RevealSequence"/>. The look and timing live in the sequence's step children.
/// </summary>
public class RaidResultsUI : MonoBehaviour
{
    [SerializeField] private RaidEventChannel _raidEventChannel;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RevealSequence _sequence;

    [Header("Threat summary")]
    [Tooltip("Text for {alarmReason}, indexed by the AI's AlarmReason value: GuardAlerted, BodyFound, CivilianCall, CivilianKilled.")]
    [SerializeField] private string[] _alarmReasonTexts =
    {
        "A guard raised the alarm",
        "A body was found",
        "A civilian called the police",
        "A civilian was killed"
    };
    [SerializeField] private string _unknownAlarmReasonText = "The alarm was raised";
    [SerializeField] private string _noAlarmText = "No alarm raised";

    [Header("Panel fade (unscaled seconds)")]
    [SerializeField, Min(0f)] private float _fadeInSeconds = 0.35f;
    [SerializeField, Min(0f)] private float _fadeOutSeconds = 0.25f;

    private CancellationTokenSource _cts;
    private RevealContext _context;
    private Tween _fadeTween;
    private bool _visible;
    private int _shownRunId;

    private void Awake()
    {
        if (!_raidEventChannel)
            _raidEventChannel = Resources.Load<RaidEventChannel>(RaidEventChannel.ResourcePath);

        if (!_canvasGroup && !TryGetComponent(out _canvasGroup))
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (!_sequence)
            _sequence = GetComponentInChildren<RevealSequence>(true);

        SetVisible(false, 0f);
        if (_sequence) _sequence.ResetVisuals();
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

        CancelSequence();
    }

    private void Update()
    {
        if (_visible && LevelLoadingStatus.IsShowing)
            Hide();
    }

    public void Skip() => _context?.RequestSkip();

    public void Hide()
    {
        if (!_visible) return;

        CancelSequence();
        SetVisible(false, _fadeOutSeconds);
    }

    private void HandleResultsReady(RaidResults results)
    {
        if (!results.IsValid)
        {
            AppLog.LogError(LogChannel.Match, $"[RaidResultsUI] Invalid raid results (outcome {results.Outcome}).", this);
            return;
        }

        if (results.RunId == _shownRunId) return;
        _shownRunId = results.RunId;

        if (!_sequence)
        {
            AppLog.LogError(LogChannel.Match, "[RaidResultsUI] No RevealSequence assigned.", this);
            return;
        }

        CancelSequence();
        _cts = new CancellationTokenSource();
        _context = BuildContext(results);
        PlayAsync(_context, _cts.Token).Forget();
    }

    private async UniTaskVoid PlayAsync(RevealContext ctx, CancellationToken ct)
    {
        _sequence.ResetVisuals();
        SetVisible(true, _fadeInSeconds);

        try
        {
            await _sequence.Play(ctx, ct);
        }
        catch (System.OperationCanceledException)
        {
        }
    }

    private RevealContext BuildContext(RaidResults results)
    {
        PlayerID local = NetworkManager.main ? NetworkManager.main.localPlayer : default;
        if (!results.TryGetPlayer(local, out var me))
            return new RevealContext(RevealOutcome.NoData, null, GetReturnSeconds);

        var ctx = new RevealContext(ToRevealOutcome(results.Outcome, me.Extracted), BuildItems(me), GetReturnSeconds);
        ctx.SetThreat(results.Stealth, results.CivilianKills)
           .SetNumber("take", me.RunValue)
           .SetNumber("payout", me.Payout)
           .SetNumber("stealthBonus", me.StealthBonus)
           .SetNumber("civilianPenalty", me.CivilianPenalty)
           .SetNumber("banked", me.BankedTotal)
           .SetNumber("bankedBefore", me.BankedTotal - me.Payout)
           .SetNumber("civilianKills", results.CivilianKills)
           .SetNumber("assaults", results.AssaultsReached)
           .SetText("alarmReason", AlarmReasonText(results));
        return ctx;
    }

    private string AlarmReasonText(RaidResults results)
    {
        if (results.Stealth) return _noAlarmText;

        int index = results.AlarmReason;
        bool known = _alarmReasonTexts != null && index < _alarmReasonTexts.Length && !string.IsNullOrEmpty(_alarmReasonTexts[index]);
        return known ? _alarmReasonTexts[index] : _unknownAlarmReasonText;
    }

    private static RevealOutcome ToRevealOutcome(RaidOutcome outcome, bool extracted) => outcome switch
    {
        RaidOutcome.Extracted => extracted ? RevealOutcome.Extracted : RevealOutcome.LeftBehind,
        RaidOutcome.NobodyExtracted => RevealOutcome.NobodyExtracted,
        RaidOutcome.Wiped => RevealOutcome.Wiped,
        RaidOutcome.TimedOut => RevealOutcome.TimedOut,
        _ => RevealOutcome.NoData
    };

    private static List<RevealItem> BuildItems(RaidPlayerResult me)
    {
        var items = new List<RevealItem>();
        int count = me.ItemIds?.Length ?? 0;
        var registry = GameContent.LootRegistry;

        for (int i = 0; i < count; i++)
        {
            string id = me.ItemIds[i];
            int quantity = me.ItemQuantities != null && i < me.ItemQuantities.Length ? me.ItemQuantities[i] : 1;

            string name = id;
            int value = 0;
            if (registry && registry.TryGetById(id, out var def))
            {
                name = def.Name;
                value = def.BaseValue * quantity;
            }

            items.Add(new RevealItem(name, quantity, value));
        }

        return items;
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

    private void SetVisible(bool visible, float fadeSeconds)
    {
        _visible = visible;
        _canvasGroup.blocksRaycasts = visible;
        _canvasGroup.interactable = visible;

        _fadeTween.Stop();
        if (fadeSeconds <= 0f) _canvasGroup.alpha = visible ? 1f : 0f;
        else _fadeTween = Tween.Alpha(_canvasGroup, visible ? 1f : 0f, fadeSeconds, useUnscaledTime: true);
    }

    private void CancelSequence()
    {
        if (_cts == null) return;
        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
    }

#if UNITY_EDITOR
    private float _previewReturnEnd = -1f;

    public void StartPreviewReturnCountdown(float seconds) => _previewReturnEnd = Time.unscaledTime + seconds;
#endif
}
}
