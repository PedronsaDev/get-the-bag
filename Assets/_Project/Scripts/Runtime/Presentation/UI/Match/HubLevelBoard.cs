using System.Text;
using Project.Core.Match;
using TMPro;
using UnityEngine;

namespace Project.UI.Match
{
/// <summary>
/// World-space screen in the hub. Every player sees the same launch status because it reads
/// replicated state from <see cref="LevelLaunchController"/> and <see cref="HubReadyState"/>.
/// The text is rebuilt only when a shown value changes.
/// </summary>
public class HubLevelBoard : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;

    private readonly StringBuilder _sb = new(128);
    private LaunchState _lastState = (LaunchState)255;
    private int _lastSeconds = -1;
    private string _lastLevel;
    private int _lastReady = -1;
    private int _lastTotal = -1;
    private int _lastInZone = -1;
    private int _lastZoneTotal = -1;

    private void Awake()
    {
        if (!_text) _text = GetComponentInChildren<TMP_Text>();
    }

    private void Update()
    {
        if (!_text) return;

        var launcher = LevelLaunchController.Instance;
        var ready = HubReadyState.Instance;

        var state = launcher ? launcher.CurrentState : LaunchState.Idle;
        string level = launcher ? launcher.Pending : string.Empty;
        int seconds = launcher ? Mathf.CeilToInt(launcher.Countdown) : 0;
        int readyCount = ready ? ready.ReadyCount : 0;
        int total = ready ? ready.TotalCount : 0;
        var zone = launcher ? launcher.DeployZone : null;
        int inZone = zone ? zone.PlayersInsideCount.value : 0;
        int zoneTotal = zone ? zone.TotalRequiredCount.value : 0;

        if (state == _lastState && seconds == _lastSeconds && level == _lastLevel &&
            readyCount == _lastReady && total == _lastTotal &&
            inZone == _lastInZone && zoneTotal == _lastZoneTotal)
            return;

        _lastState = state;
        _lastSeconds = seconds;
        _lastLevel = level;
        _lastReady = readyCount;
        _lastTotal = total;
        _lastInZone = inZone;
        _lastZoneTotal = zoneTotal;

        _sb.Clear();
        switch (state)
        {
            case LaunchState.Countdown:
                _sb.Append("DEPLOYING\n").Append(PrettyName(level)).Append("\nin ").Append(seconds).Append('s');
                break;
            case LaunchState.Loading:
                _sb.Append("DEPLOYING\n").Append(PrettyName(level)).Append("\nLoading...");
                break;
            default:
                _sb.Append("NO JOB SELECTED\nUse the level selector");
                break;
        }

        _sb.Append('\n');

        if (zone)
            _sb.Append("\nIn deploy zone ").Append(inZone).Append('/').Append(zoneTotal);

        if (ready)
            _sb.Append("\nReady ").Append(readyCount).Append('/').Append(total);

        _text.text = _sb.ToString();
    }

    private static string PrettyName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return "?";
        if (sceneName.StartsWith("Scene_")) sceneName = sceneName.Substring(6);
        return sceneName.Replace('_', ' ').ToUpperInvariant();
    }
}
}
