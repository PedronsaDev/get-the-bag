using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.AI.Cover.Editor
{
public class CoverBakerWindow : EditorWindow
{
    private const string UxmlPath = "Assets/_Project/Scripts/Editor/AI/Cover/CoverBakerWindow.uxml";

    private CoverBakeSettings _settings;
    private readonly List<CoverCandidate> _preview = new();
    private List<CoverIssue> _issues = new();

    private ObjectField _targetField;
    private Label _statusLabel;
    private Label _validateSummary;
    private VisualElement _issueList;

    [MenuItem("Tools/AI/Cover Baker")]
    public static void Open()
    {
        var window = GetWindow<CoverBakerWindow>();
        window.titleContent = new GUIContent("Cover Baker");
        window.minSize = new Vector2(340f, 520f);
    }

    [MenuItem("Tools/AI/Toggle Draw Cover Points")]
    public static void Toggle() => CoverBakerEngine.SetDrawPoints(!CoverBakerEngine.DrawPoints);

    private void OnEnable() => SceneView.duringSceneGui += OnSceneGui;

    private void OnDisable() => SceneView.duringSceneGui -= OnSceneGui;

    private void CreateGUI()
    {
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        if (!tree)
        {
            rootVisualElement.Add(new Label($"Missing layout file: {UxmlPath}"));
            return;
        }
        tree.CloneTree(rootVisualElement);

        _settings = CoverBakeSettings.Load();
        var root = rootVisualElement;
        _targetField = root.Q<ObjectField>("targetField");
        _targetField.objectType = typeof(GameObject);
        _targetField.allowSceneObjects = true;
        _statusLabel = root.Q<Label>("statusLabel");
        _validateSummary = root.Q<Label>("validateSummary");
        _issueList = root.Q<VisualElement>("issueList");

        BindSettings();
        root.Q<Button>("resetButton").clicked += ResetSettings;
        root.Q<Button>("previewButton").clicked += Preview;
        root.Q<Button>("bakeButton").clicked += Bake;
        root.Q<Button>("clearButton").clicked += Clear;
        root.Q<Button>("validateButton").clicked += Validate;

        var showPoints = root.Q<Toggle>("showPointsToggle");
        showPoints.SetValueWithoutNotify(CoverBakerEngine.DrawPoints);
        showPoints.RegisterValueChangedCallback(evt => CoverBakerEngine.SetDrawPoints(evt.newValue));
    }

    private void BindSettings()
    {
        var root = rootVisualElement;
        Bind(root.Q<LayerMaskField>("obstacleMaskField"), _settings.ObstacleMask.value, value => _settings.ObstacleMask = value);
        Bind(root.Q<FloatField>("stepField"), _settings.StepInterval, value => _settings.StepInterval = Mathf.Max(0.2f, value));
        Bind(root.Q<FloatField>("spacingField"), _settings.MinSpacing, value => _settings.MinSpacing = Mathf.Max(0.1f, value));
        Bind(root.Q<FloatField>("wallDistanceField"), _settings.WallSearchDistance, value => _settings.WallSearchDistance = Mathf.Max(0.5f, value));
        Bind(root.Q<FloatField>("lowHeightField"), _settings.LowCoverHeight, value => _settings.LowCoverHeight = Mathf.Max(0.3f, value));
        Bind(root.Q<FloatField>("highHeightField"), _settings.HighCoverHeight, value => _settings.HighCoverHeight = Mathf.Max(0.3f, value));
        Bind(root.Q<FloatField>("maxPeekField"), _settings.MaxPeekDistance, value => _settings.MaxPeekDistance = Mathf.Max(0.3f, value));
        Bind(root.Q<FloatField>("agentRadiusField"), _settings.AgentRadius, value => _settings.AgentRadius = Mathf.Max(0.1f, value));
    }

    private void Bind(LayerMaskField field, int initial, System.Action<int> apply)
    {
        field.SetValueWithoutNotify(initial);
        field.RegisterValueChangedCallback(evt =>
        {
            apply(evt.newValue);
            _settings.Save();
        });
    }

    private void Bind(FloatField field, float initial, System.Action<float> apply)
    {
        field.SetValueWithoutNotify(initial);
        field.RegisterValueChangedCallback(evt =>
        {
            apply(evt.newValue);
            _settings.Save();
        });
    }

    private void ResetSettings()
    {
        _settings = new CoverBakeSettings();
        _settings.Save();
        rootVisualElement.Clear();
        CreateGUI();
    }

    private GameObject Target()
    {
        var target = _targetField.value as GameObject;
        return target ? target : Selection.activeGameObject;
    }

    private bool TryGetTarget(out GameObject target)
    {
        target = Target();
        if (target)
            return true;

        SetStatus("Select a level object (or fill the Root field) first.", false);
        return false;
    }

    private void Preview()
    {
        if (!TryGetTarget(out var target))
            return;

        CoverBakerEngine.Collect(target, _settings, _preview);
        SceneView.RepaintAll();
        SetStatus(_preview.Count == 0
            ? "Nothing found. Check that the scene has a baked NavMesh and the obstacle layers are right."
            : $"Preview: {_preview.Count} cover points ({CountHigh()} high, {_preview.Count - CountHigh()} low, {CountWithPeeks()} with side peeks).",
            _preview.Count > 0);
    }

    private void Bake()
    {
        if (!TryGetTarget(out var target))
            return;

        int count = CoverBakerEngine.Bake(target, _settings);
        _preview.Clear();
        SetStatus($"Baked {count} cover points. Any earlier baked points were replaced.", count > 0);
        Validate();
    }

    private void Clear()
    {
        if (!TryGetTarget(out var target))
            return;

        CoverBakerEngine.ClearCoverPoints(target);
        _preview.Clear();
        _issues.Clear();
        ShowIssues();
        SetStatus("Baked cover points removed.", true);
    }

    private void Validate()
    {
        if (!TryGetTarget(out var target))
            return;

        _issues = CoverValidator.Validate(target, _settings);
        ShowIssues();
        SceneView.RepaintAll();
    }

    private void ShowIssues()
    {
        _issueList.Clear();
        int errors = 0;
        foreach (var issue in _issues)
        {
            bool isError = issue.Severity == CoverIssueSeverity.Error;
            errors += isError ? 1 : 0;

            var row = new Label($"{issue.Point.name}: {issue.Message}");
            row.AddToClassList("issue");
            row.AddToClassList(isError ? "issue-error" : "issue-warning");
            var point = issue.Point;
            row.RegisterCallback<ClickEvent>(_ => SelectPoint(point));
            _issueList.Add(row);
        }

        _validateSummary.text = _issues.Count == 0
            ? "All cover points passed."
            : $"{errors} errors, {_issues.Count - errors} warnings. Click an entry to select the point.";
        _validateSummary.EnableInClassList("status-ok", _issues.Count == 0);
        _validateSummary.EnableInClassList("status-bad", errors > 0);
    }

    private static void SelectPoint(CoverPoint point)
    {
        if (!point)
            return;

        Selection.activeGameObject = point.gameObject;
        SceneView.lastActiveSceneView?.Frame(new Bounds(point.Position, Vector3.one * 4f), false);
    }

    private void SetStatus(string message, bool good)
    {
        _statusLabel.text = message;
        _statusLabel.EnableInClassList("status-ok", good);
        _statusLabel.EnableInClassList("status-bad", !good);
    }

    private int CountHigh()
    {
        int count = 0;
        foreach (var candidate in _preview)
            count += candidate.Height == CoverHeight.High ? 1 : 0;
        return count;
    }

    private int CountWithPeeks()
    {
        int count = 0;
        foreach (var candidate in _preview)
            count += (candidate.Peeks & (CoverPeekDirection.Left | CoverPeekDirection.Right)) != 0 ? 1 : 0;
        return count;
    }

    private void OnSceneGui(SceneView view)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        foreach (var candidate in _preview)
            DrawCandidate(candidate);

        Handles.color = Color.red;
        foreach (var issue in _issues)
        {
            if (issue.Point && issue.Severity == CoverIssueSeverity.Error)
                Handles.DrawWireDisc(issue.Point.Position + Vector3.up * 0.05f, Vector3.up, 0.45f);
        }
    }

    // Brighter means better cover. Yellow dots are the side-peek positions.
    private static void DrawCandidate(CoverCandidate candidate)
    {
        Handles.color = Color.Lerp(new Color(1f, 0.4f, 0.3f), new Color(0.3f, 1f, 0.5f), candidate.Quality);
        Handles.DrawSolidDisc(candidate.Position + Vector3.up * 0.03f, Vector3.up, 0.22f);
        Handles.DrawLine(candidate.Position, candidate.Position + candidate.Normal * 0.7f);

        Vector3 right = Vector3.Cross(Vector3.up, candidate.Normal);
        Handles.color = Color.yellow;
        if (candidate.RightOffset > 0f)
            Handles.DrawSolidDisc(candidate.Position + right * candidate.RightOffset, Vector3.up, 0.12f);
        if (candidate.LeftOffset > 0f)
            Handles.DrawSolidDisc(candidate.Position - right * candidate.LeftOffset, Vector3.up, 0.12f);
    }
}
}
