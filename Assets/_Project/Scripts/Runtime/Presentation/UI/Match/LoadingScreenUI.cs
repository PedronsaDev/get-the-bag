using System.Collections.Generic;
using Project.Match;
using Project.Definitions.Levels;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.UI.Match
{
public class LoadingScreenUI : MonoBehaviour
{
    [Header("Levels")]
    [Tooltip("Name, tagline and art per scene. Scenes not listed fall back to a name made from the scene name.")]
    [SerializeField] private List<LevelDefinition> _levels = new();

    [Header("References")]
    [SerializeField] private TMP_Text _levelName;
    [SerializeField] private TMP_Text _tagline;
    [SerializeField] private TMP_Text _status;
    [SerializeField] private Image _background;
    [Tooltip("Stretched horizontally from 0 to 1 by changing anchorMax.x.")]
    [SerializeField] private RectTransform _progressFill;

    [Header("Settings")]
    [Tooltip("The loading screen stays up at least this long, and levels do not start sooner. The bar fills over this time.")]
    [SerializeField, Min(0f)] private float _minimumSeconds = LevelLoadingStatus.DefaultMinimumSeconds;
    [Tooltip("Share of the bar used by this peer's own scene load; the rest tracks other players.")]
    [SerializeField, Range(0.1f, 1f)] private float _sceneShare = 0.7f;
    [SerializeField, Min(0.1f)] private float _barSmoothing = 6f;

    private string _shownScene;
    private float _shownProgress;
    private string _lastStatus;

    private void Awake() => LevelLoadingStatus.MinimumSeconds = _minimumSeconds;

    private void OnEnable()
    {
        LevelLoadingStatus.MinimumSeconds = _minimumSeconds;
        _shownScene = null;
        _shownProgress = 0f;
        _lastStatus = null;
        Refresh(true);
    }

    private void Update() => Refresh(false);

    private void Refresh(bool snap)
    {
        string scene = LevelLoadingStatus.TargetScene;
        if (scene != _shownScene)
        {
            _shownScene = scene;
            ApplyLevel(scene);
            _shownProgress = 0f;
        }

        float target = ComputeProgress(out string status);
        _shownProgress = snap ? target : Mathf.MoveTowards(_shownProgress, target, _barSmoothing * Time.unscaledDeltaTime);
        if (_progressFill)
        {
            var max = _progressFill.anchorMax;
            max.x = _shownProgress;
            _progressFill.anchorMax = max;
        }

        if (_status && status != _lastStatus)
        {
            _lastStatus = status;
            _status.text = status;
        }
    }

    private float ComputeProgress(out string status)
    {
        float sceneProgress = LevelLoadingStatus.SceneProgress;

        float sceneDone = Mathf.Clamp01(sceneProgress / 0.9f);

        var gate = LevelReadyGate.Instance;
        bool gateForThisScene = gate && gate.gameObject.scene.name == _shownScene;

        if (!gateForThisScene)
        {
            status = sceneDone < 1f ? $"Loading {Mathf.RoundToInt(sceneDone * 100f)}%" : "Almost there...";
            bool arrivedWithoutGate = sceneDone >= 1f && SceneManager.GetActiveScene().name == _shownScene;
            return CapByMinimumTime(arrivedWithoutGate ? 1f : sceneDone * _sceneShare);
        }

        if (gate.CurrentPhase == LevelPhase.Failed)
        {
            string reason = gate.FailureReason.value;
            status = string.IsNullOrEmpty(reason)
                ? "Could not start the level. Returning to the hub..."
                : $"Could not start the level. {reason}\nReturning to the hub...";
            return 1f;
        }

        int expected = Mathf.Max(1, gate.Expected);
        int ready = Mathf.Clamp(gate.ReadyCount, 0, expected);
        status = ready < expected ? $"Waiting for players ({ready}/{expected})" : "Everyone is in";
        return CapByMinimumTime(_sceneShare + (1f - _sceneShare) * ready / expected);
    }

    // Keeps the bar from racing to full on fast loads: it can't get ahead of the minimum loading time.
    private static float CapByMinimumTime(float progress) => Mathf.Min(progress, LevelLoadingStatus.MinimumProgress);

    private void ApplyLevel(string scene)
    {
        var def = Find(scene);

        if (_levelName)
            _levelName.text = def ? $"<sketchy maxclr=(0,0,0) minclr=(0,0,0)>{def.DisplayName}</sketchy>" : LevelLaunchController.DisplayName(scene);

        if (_tagline)
        {
            string tagline = def ? def.Tagline : string.Empty;
            _tagline.text = tagline;
            _tagline.gameObject.SetActive(!string.IsNullOrEmpty(tagline));
        }

        if (_background)
        {
            var sprite = def ? def.LoadingImage : null;
            _background.sprite = sprite;
            _background.enabled = sprite;
        }
    }

    private LevelDefinition Find(string scene)
    {
        if (string.IsNullOrEmpty(scene)) return null;
        for (int i = 0; i < _levels.Count; i++)
        {
            var def = _levels[i];
            if (def && string.Equals(def.SceneName, scene, System.StringComparison.OrdinalIgnoreCase))
                return def;
        }
        return null;
    }
}
}
