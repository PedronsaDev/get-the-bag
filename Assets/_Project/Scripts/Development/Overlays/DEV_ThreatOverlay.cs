using System.Text;
using Project.AI.Alarm;
using Project.AI.Configuration;
using Project.AI.Decision;
using Project.AI.Decision.States;
using Project.AI.Perception;
using Project.Core.Raid;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Development.Overlays
{
/// <summary>
/// Top-left debug text with the raid and threat status and a short list of recent events. It creates itself in the
/// editor and in development builds, and F3 hides or shows it. AI counts only exist on the host, where agents run.
/// </summary>
public class DEV_ThreatOverlay : MonoBehaviour
{
    private const float RefreshSeconds = 0.25f;
    private const float PanelWidth = 380f;
    private const float Margin = 10f;
    private const float Padding = 6f;

    private readonly ThreatEventLog _log = new();
    private readonly StringBuilder _builder = new(512);
    private readonly GUIContent _content = new();

    private bool _visible = true;
    private float _nextRefresh;
    private GUIStyle _style;
    private Texture2D _background;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (!Debug.isDebugBuild || Application.isBatchMode || FindAnyObjectByType<DEV_ThreatOverlay>())
            return;

        var host = new GameObject("[DEV] Threat Overlay");
        DontDestroyOnLoad(host);
        host.AddComponent<DEV_ThreatOverlay>();
    }

    private void OnEnable() => AICorpse.BodyDiscovered += HandleBodyFound;

    private void OnDisable() => AICorpse.BodyDiscovered -= HandleBodyFound;

    private void OnDestroy()
    {
        if (_background)
            Destroy(_background);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            _visible = !_visible;

        // The log keeps watching while hidden, so showing the overlay again still has the history.
        _log.Observe(Time.timeSinceLevelLoad, RaidLifecycle.Phase, ThreatStatus.Current);

        if (_visible && Time.unscaledTime >= _nextRefresh)
            Refresh();
    }

    private void OnGUI()
    {
        if (!_visible)
            return;

        EnsureStyle();
        float height = _style.CalcHeight(_content, PanelWidth - Padding * 2f) + Padding * 2f;
        var panel = new Rect(Margin, Margin, PanelWidth, height);
        GUI.DrawTexture(panel, _background);
        GUI.Label(new Rect(panel.x + Padding, panel.y + Padding, PanelWidth - Padding * 2f, height), _content, _style);
    }

    private void HandleBodyFound(Vector3 position) =>
        _log.Add(Time.timeSinceLevelLoad, $"Body found near {position.x:0}, {position.z:0}");

    private void EnsureStyle()
    {
        if (_style != null)
            return;

        _background = new Texture2D(1, 1);
        _background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
        _background.Apply();
        _style = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        _style.normal.textColor = Color.white;
    }

    private void Refresh()
    {
        _nextRefresh = Time.unscaledTime + RefreshSeconds;
        _builder.Clear();
        _builder.AppendLine("THREAT DEBUG   (F3 hides)");
        AppendRaid();
        AppendThreat();
        AppendAi();
        AppendEvents();
        _content.text = _builder.ToString();
    }

    private void AppendRaid() => _builder.Append("Raid: ").Append(RaidLifecycle.Phase).AppendLine();

    private void AppendThreat()
    {
        var status = ThreatStatus.Current;
        if (status == null)
        {
            _builder.AppendLine("Threat: no AssaultDirector in this level");
            return;
        }

        _builder.Append("Threat: ").AppendLine(Headline(status));
        _builder.Append("Alarm: ").Append(AlarmText(status));
        _builder.Append("   Civilians killed: ").Append(status.CivilianKills).AppendLine();
        _builder.Append("Enemies alive: ").Append(status.EnemiesAlive).AppendLine();
    }

    private static string AlarmText(IThreatStatus status) =>
        status.AlarmReason == ThreatStatus.NoAlarmReason ? "quiet" : ((AlarmReason)status.AlarmReason).ToString();

    private static string Headline(IThreatStatus status)
    {
        switch (status.Phase)
        {
            case ThreatPhase.Alarmed: return $"Police arriving in {ThreatEventLog.FormatClock(status.SecondsInPhase)}";
            case ThreatPhase.Assault: return $"Assault {status.AssaultNumber}, {ThreatEventLog.FormatClock(status.SecondsInPhase)} left";
            case ThreatPhase.Break: return $"Break, next assault in {ThreatEventLog.FormatClock(status.SecondsInPhase)}";
            case ThreatPhase.FinalPush: return $"FINAL PUSH (assault {status.AssaultNumber})";
            case ThreatPhase.Ended: return $"Ended after {status.AssaultNumber} assaults";
            default: return "Stealth";
        }
    }

    private void AppendAi()
    {
        var brains = AIBrain.All;
        if (brains.Count == 0)
        {
            _builder.AppendLine("AI: none on this peer (agents run on the host)");
            return;
        }

        int fighters = 0, patrol = 0, investigate = 0, combat = 0, idle = 0, inactive = 0, civilians = 0, fleeing = 0;
        for (int i = 0; i < brains.Count; i++)
        {
            var brain = brains[i];
            var blackboard = brain.Blackboard;
            if (!blackboard || !blackboard.IsArchetypeReady)
                continue;

            if (brain.IsLivingFighter)
            {
                fighters++;
                switch (brain.CurrentState)
                {
                    case AIPatrolState: patrol++; break;
                    case AIInvestigateState: investigate++; break;
                    case AICombatState: combat++; break;
                    case AIIdleState: idle++; break;
                    default: inactive++; break;
                }
            }
            else if (brain.CurrentState != null && !blackboard.ArchetypeConfig.Role.Fights())
            {
                civilians++;
                if (brain.CurrentState is AIFleeState)
                    fleeing++;
            }
        }

        _builder.Append("Fighters: ").Append(fighters)
            .Append("  patrol ").Append(patrol)
            .Append(", investigating ").Append(investigate)
            .Append(", combat ").Append(combat)
            .Append(", idle ").Append(idle)
            .Append(", not active ").Append(inactive).AppendLine();
        _builder.Append("Civilians: ").Append(civilians).Append("  scared ").Append(fleeing).AppendLine();
        AppendBodies();
    }

    private void AppendBodies()
    {
        var bodies = AICorpse.All;
        if (bodies.Count == 0)
            return;

        int found = 0;
        for (int i = 0; i < bodies.Count; i++)
            found += bodies[i].Discovered ? 1 : 0;
        _builder.Append("Bodies: ").Append(bodies.Count).Append("  found ").Append(found).AppendLine();
    }

    private void AppendEvents()
    {
        var lines = _log.Lines;
        if (lines.Count == 0)
            return;

        _builder.AppendLine("Events:");
        for (int i = 0; i < lines.Count; i++)
            _builder.Append("  ").AppendLine(lines[i]);
    }
}
}
