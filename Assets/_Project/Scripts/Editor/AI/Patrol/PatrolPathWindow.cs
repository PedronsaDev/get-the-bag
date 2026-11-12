using System.Collections.Generic;
using Project.AI.Blackboard;
using Project.AI.Combat;
using Project.AI.Configuration;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Project.AI.Locomotion;

namespace Project.AI.Patrol.Editor
{
public class PatrolPathWindow : EditorWindow
{
    private const string UxmlPath = "Assets/_Project/Scripts/Editor/AI/Patrol/PatrolPathWindow.uxml";
    private static readonly string[] RouteSettingNames =
        { "_loop", "_waitSeconds", "_lookSweepDegrees", "_lookSweepPeriod", "_turnSpeedDegrees" };

    [SerializeField] private AIPatrolRoute _route;

    private readonly PatrolPathSceneTool _sceneTool = new();
    private readonly List<Transform> _points = new();

    private ObjectField _routeField;
    private ObjectField _archetypeField;
    private VisualElement _routeSettings;
    private Button _addModeButton;
    private ListView _pointList;
    private Label _statsLabel;
    private Label _assignResult;
    private VisualElement _issueList;
    private bool _toolsWereHidden;

    [MenuItem("Tools/AI/Patrol Path Editor")]
    public static void Open()
    {
        var window = GetWindow<PatrolPathWindow>();
        window.titleContent = new GUIContent("Patrol Paths");
        window.minSize = new Vector2(320f, 480f);
    }

    [MenuItem("GameObject/AI/Patrol Route", false, 10)]
    private static void CreateFromMenu()
    {
        Open();
        GetWindow<PatrolPathWindow>().CreateRouteAtSceneView();
    }

    private void CreateGUI()
    {
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        if (!tree)
        {
            rootVisualElement.Add(new Label($"Missing layout file: {UxmlPath}"));
            return;
        }
        tree.CloneTree(rootVisualElement);

        FindElements();
        SetUpFields();
        SetUpButtons();
        SetUpList();
        _sceneTool.Route = _route;
        BuildRouteSettings();
        SelectRouteFromSelection();
        Refresh();
    }

    private void OnEnable()
    {
        Tools.hidden = false;
        SceneView.duringSceneGui += OnSceneGui;
        Selection.selectionChanged += OnSelectionChanged;
        Undo.undoRedoPerformed += Refresh;
        _sceneTool.PathChanged += Refresh;
        _sceneTool.PointSelected += OnScenePointSelected;
        _sceneTool.AddModeEnded += UpdateAddModeButton;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGui;
        Selection.selectionChanged -= OnSelectionChanged;
        Undo.undoRedoPerformed -= Refresh;
        _sceneTool.PathChanged -= Refresh;
        _sceneTool.PointSelected -= OnScenePointSelected;
        _sceneTool.AddModeEnded -= UpdateAddModeButton;
        _sceneTool.AddMode = false;
        SetNativeToolsHidden(false);
        SceneView.RepaintAll();
    }

    private void FindElements()
    {
        var root = rootVisualElement;
        _routeField = root.Q<ObjectField>("routeField");
        _archetypeField = root.Q<ObjectField>("archetypeField");
        _routeSettings = root.Q<VisualElement>("routeSettings");
        _addModeButton = root.Q<Button>("addModeButton");
        _pointList = root.Q<ListView>("pointList");
        _statsLabel = root.Q<Label>("statsLabel");
        _assignResult = root.Q<Label>("assignResult");
        _issueList = root.Q<VisualElement>("issueList");
    }

    private void SetUpFields()
    {
        var root = rootVisualElement;

        _routeField.objectType = typeof(AIPatrolRoute);
        _routeField.allowSceneObjects = true;
        _routeField.SetValueWithoutNotify(_route);
        _routeField.RegisterValueChangedCallback(evt => SetRoute(evt.newValue as AIPatrolRoute));

        _archetypeField.objectType = typeof(AIArchetypeConfig);
        _archetypeField.allowSceneObjects = false;
        _archetypeField.RegisterValueChangedCallback(_ => UpdateStats());

        var snapToggle = root.Q<Toggle>("snapToggle");
        snapToggle.RegisterValueChangedCallback(evt => _sceneTool.SnapToNavMesh = evt.newValue);
        _sceneTool.SnapToNavMesh = snapToggle.value;

        var radiusField = root.Q<FloatField>("snapRadiusField");
        radiusField.RegisterValueChangedCallback(evt => _sceneTool.SnapRadius = Mathf.Max(0.1f, evt.newValue));
        _sceneTool.SnapRadius = Mathf.Max(0.1f, radiusField.value);

        var faceToggle = root.Q<Toggle>("faceToggle");
        faceToggle.RegisterValueChangedCallback(evt => _sceneTool.FaceAlongPath = evt.newValue);
        _sceneTool.FaceAlongPath = faceToggle.value;
    }

    private void SetUpButtons()
    {
        var root = rootVisualElement;
        root.Q<Button>("newRouteButton").clicked += CreateRouteAtSceneView;
        _addModeButton.clicked += ToggleAddMode;
        root.Q<Button>("frameButton").clicked += FrameSelectedPoint;
        root.Q<Button>("deleteButton").clicked += DeleteSelectedPoint;
        root.Q<Button>("snapAllButton").clicked += SnapAll;
        root.Q<Button>("faceButton").clicked += FaceAlongPath;
        root.Q<Button>("reverseButton").clicked += ReverseRoute;
        root.Q<Button>("assignButton").clicked += AssignToSelectedGuards;
    }

    private void SetUpList()
    {
        _pointList.itemsSource = _points;
        _pointList.fixedItemHeight = 22f;
        _pointList.selectionType = SelectionType.Single;
        _pointList.reorderable = true;
        _pointList.makeItem = () => new Label();
        _pointList.bindItem = BindPointRow;
        _pointList.selectedIndicesChanged += indices =>
        {
            foreach (int index in indices)
                SelectPoint(index);
        };
        _pointList.itemIndexChanged += OnPointReordered;
    }

    private void BindPointRow(VisualElement element, int index)
    {
        var label = (Label)element;
        var point = _points[index];
        bool onNavMesh = PatrolPathUtility.TrySnap(point.position, 0.5f, out _);

        label.text = $"{index + 1:00}   {point.name}   ({point.position.x:0.0}, {point.position.y:0.0}, {point.position.z:0.0})";
        label.EnableInClassList("point-row", true);
        label.EnableInClassList("point-ok", onNavMesh);
        label.EnableInClassList("point-bad", !onNavMesh);
    }

    private void SetRoute(AIPatrolRoute route)
    {
        _route = route;
        _sceneTool.Route = route;
        _sceneTool.SelectedIndex = -1;
        _sceneTool.AddMode = false;
        _routeField.SetValueWithoutNotify(route);
        BuildRouteSettings();
        Refresh();
        SceneView.RepaintAll();
    }

    private void OnSelectionChanged()
    {
        SelectRouteFromSelection();
        if (_route && !SelectionTouchesRoute() && !SelectionHasGuards())
        {
            SetRoute(null);
            return;
        }

        Refresh();
    }

    // Guards stay selectable without losing the route, so Assign to Selected Guards still has one.
    private static bool SelectionHasGuards()
    {
        foreach (var selected in Selection.gameObjects)
        {
            if (selected.GetComponentInChildren<AIBlackboard>())
                return true;
        }
        return false;
    }

    private bool SelectionTouchesRoute()
    {
        var selected = Selection.activeGameObject;
        return _route && selected && selected.transform.IsChildOf(_route.transform);
    }

    private void SelectRouteFromSelection()
    {
        var selected = Selection.activeGameObject;
        if (!selected)
            return;

        var route = selected.GetComponentInParent<AIPatrolRoute>();
        if (!route)
            return;

        if (route != _route)
            SetRoute(route);

        _sceneTool.SelectedIndex = selected.transform.parent == route.transform ? selected.transform.GetSiblingIndex() : -1;
    }

    private void CreateRouteAtSceneView()
    {
        var view = SceneView.lastActiveSceneView;
        Vector3 position = view ? view.pivot : Vector3.zero;
        var route = PatrolPathUtility.CreateRoute(position);
        Selection.activeGameObject = route.gameObject;
        SetRoute(route);
        SetAddMode(true);
    }

    private void ToggleAddMode() => SetAddMode(!_sceneTool.AddMode);

    private void SetAddMode(bool on)
    {
        if (on && !_route)
        {
            _assignResult.text = "Create or pick a route first.";
            return;
        }
        _sceneTool.AddMode = on;
        UpdateAddModeButton();
        UpdateEditing();
        SceneView.RepaintAll();
    }

    private void UpdateAddModeButton()
    {
        if (_addModeButton == null)
            return;
        _addModeButton.EnableInClassList("active", _sceneTool.AddMode);
        _addModeButton.text = _sceneTool.AddMode ? "Adding Points... (Esc to stop)" : "Add Points in Scene View";
    }

    private void SelectPoint(int index)
    {
        _sceneTool.SelectedIndex = index;
        SelectPointObject(index);
        SceneView.RepaintAll();
    }

    // The Hierarchy selection is what says the route is being edited, so selecting a point selects its object.
    private void SelectPointObject(int index)
    {
        if (_route && index >= 0 && index < _route.PointCount)
            Selection.activeGameObject = _route.GetPoint(index).gameObject;
    }

    private void OnScenePointSelected(int index)
    {
        if (_pointList == null)
            return;
        SelectPointObject(index);
        _pointList.SetSelectionWithoutNotify(index >= 0 ? new[] { index } : new int[0]);
        if (index >= 0)
            _pointList.ScrollToItem(index);
    }

    private void OnPointReordered(int from, int to)
    {
        if (!_route)
            return;
        PatrolPathUtility.MovePoint(_route, from, to);
        _sceneTool.SelectedIndex = to;
        // Rebuild after the list finishes its drag, not in the middle of it.
        rootVisualElement.schedule.Execute(Refresh);
    }

    private void DeleteSelectedPoint()
    {
        if (!_route || _sceneTool.SelectedIndex < 0)
            return;
        PatrolPathUtility.RemovePoint(_route, _sceneTool.SelectedIndex);
        _sceneTool.SelectedIndex = Mathf.Min(_sceneTool.SelectedIndex, _route.PointCount - 1);
        Refresh();
        SceneView.RepaintAll();
    }

    private void FrameSelectedPoint()
    {
        var view = SceneView.lastActiveSceneView;
        if (!_route || !view)
            return;

        int index = _sceneTool.SelectedIndex;
        if (index >= 0 && index < _route.PointCount)
            view.Frame(new Bounds(_route.GetPoint(index).position, Vector3.one * 4f), false);
        else if (_route.PointCount > 0)
            view.Frame(RouteBounds(), false);
    }

    private Bounds RouteBounds()
    {
        var bounds = new Bounds(_route.GetPoint(0).position, Vector3.one);
        for (int i = 1; i < _route.PointCount; i++)
            bounds.Encapsulate(_route.GetPoint(i).position);
        bounds.Expand(2f);
        return bounds;
    }

    private void SnapAll()
    {
        if (!_route)
            return;
        int failed = PatrolPathUtility.SnapToNavMesh(_route, _sceneTool.SnapRadius);
        _assignResult.text = failed == 0 ? "All points are on the NavMesh." : $"{failed} point(s) have no NavMesh nearby.";
        Refresh();
        SceneView.RepaintAll();
    }

    private void FaceAlongPath()
    {
        if (!_route)
            return;
        PatrolPathUtility.FaceAlongPath(_route);
        SceneView.RepaintAll();
    }

    private void ReverseRoute()
    {
        if (!_route)
            return;
        PatrolPathUtility.Reverse(_route);
        _sceneTool.SelectedIndex = -1;
        Refresh();
        SceneView.RepaintAll();
    }

    private void AssignToSelectedGuards()
    {
        if (!_route)
        {
            _assignResult.text = "Pick a route first.";
            return;
        }

        int assigned = 0;
        foreach (var selected in Selection.gameObjects)
        {
            var blackboard = selected.GetComponentInChildren<AIBlackboard>();
            if (!blackboard)
                continue;

            var serialized = new SerializedObject(blackboard);
            serialized.FindProperty("_patrolRoute").objectReferenceValue = _route;
            serialized.ApplyModifiedProperties();
            assigned++;
        }
        _assignResult.text = assigned == 0
            ? "Select one or more guards in the Hierarchy first."
            : $"Assigned to {assigned} guard(s).";
    }

    private void BuildRouteSettings()
    {
        _routeSettings.Clear();
        if (!_route)
            return;

        var serialized = new SerializedObject(_route);
        foreach (string propertyName in RouteSettingNames)
        {
            var field = new PropertyField(serialized.FindProperty(propertyName));
            field.Bind(serialized);
            field.RegisterValueChangeCallback(_ => UpdateStats());
            _routeSettings.Add(field);
        }
    }

    private void Refresh()
    {
        if (_pointList == null)
            return;

        _points.Clear();
        if (_route)
        {
            for (int i = 0; i < _route.PointCount; i++)
                _points.Add(_route.GetPoint(i));
        }
        _pointList.Rebuild();
        if (_sceneTool.SelectedIndex >= 0 && _sceneTool.SelectedIndex < _points.Count)
            _pointList.SetSelectionWithoutNotify(new[] { _sceneTool.SelectedIndex });

        UpdateAddModeButton();
        UpdateStats();
        UpdateEditing();
    }

    private void UpdateStats()
    {
        if (_statsLabel == null)
            return;

        _issueList.Clear();
        if (!_route)
        {
            _statsLabel.text = "No route selected.";
            return;
        }

        float speed = PatrolSpeed();
        float length = PatrolPathUtility.PathLength(_route);
        float cycle = PatrolPathUtility.EstimateCycleSeconds(_route, speed);
        _statsLabel.text = $"{_route.PointCount} points, {length:0.0} m one way, " +
                           $"about {cycle:0} s per cycle at {speed:0.0} m/s.";

        var issues = PatrolPathUtility.Validate(_route);
        if (issues.Count == 0)
            AddIssueRow("The route is valid.", "issue-ok", -1);
        foreach (var issue in issues)
            AddIssueRow(issue.Message, issue.Severity == PatrolIssueSeverity.Error ? "issue-error" : "issue-warning",
                issue.PointIndex);
    }

    private void AddIssueRow(string message, string styleClass, int pointIndex)
    {
        var row = new Label(message);
        row.AddToClassList("issue");
        row.AddToClassList(styleClass);
        if (pointIndex >= 0)
        {
            row.RegisterCallback<ClickEvent>(_ =>
            {
                _sceneTool.SelectedIndex = pointIndex;
                OnScenePointSelected(pointIndex);
                FrameSelectedPoint();
            });
        }
        _issueList.Add(row);
    }

    private float PatrolSpeed()
    {
        var archetype = _archetypeField.value as AIArchetypeConfig;
        return archetype ? archetype.MovementConfig.PatrolSpeed : new AIMovementConfig().PatrolSpeed;
    }

    // Editing only happens while the route or one of its points is selected, or while points are being added.
    // Unity's own tools are hidden only when our handles replace them: a selected point or add mode.
    private void UpdateEditing()
    {
        _sceneTool.Editable = SelectionTouchesRoute();
        bool ourHandlesActive = _sceneTool.AddMode || (_sceneTool.Editable && _sceneTool.SelectedIndex >= 0);
        SetNativeToolsHidden(_route && ourHandlesActive);
    }

    // Our own handles replace Unity's move tool while a route is being edited.
    private void SetNativeToolsHidden(bool hidden)
    {
        if (hidden == _toolsWereHidden)
            return;
        Tools.hidden = hidden;
        _toolsWereHidden = hidden;
    }

    private void OnSceneGui(SceneView view) => _sceneTool.OnSceneGui(view);
}
}
