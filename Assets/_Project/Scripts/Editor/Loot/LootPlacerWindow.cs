using System.Collections.Generic;
using System.Linq;
using Project.Definitions.Loot;
using Project.Loot.Spawning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.Loot.Editor
{
/// <summary>
/// Click in the Scene view to place loot spawn points on any surface.
/// Shift + click removes a point. Esc stops placing.
/// </summary>
internal class LootPlacerWindow : EditorWindow
{
    private const string DefaultRootName = "Loot Spawns";
    private const float PickRadiusPixels = 18f;

    [SerializeField] private LootPool _pool;
    [SerializeField] private float _spawnChance = 1f;
    [SerializeField] private Transform _parent;
    [SerializeField] private bool _alignToSurface;
    [SerializeField] private bool _randomYaw = true;
    [SerializeField] private float _heightOffset;
    [SerializeField] private bool _paint;
    [SerializeField] private float _paintSpacing = 1f;

    private bool _placing;
    private Vector3 _lastPaintPosition;
    private Vector2 _scroll;
    private readonly List<LootTable> _previewTables = new(1);

    [MenuItem("Tools/Loot/Loot Placer")]
    private static void Open() => GetWindow<LootPlacerWindow>("Loot Placer");

    private void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        _placing = false;
    }

    private void OnSelectionChange() => Repaint();

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawPlacementSettings();
        EditorGUILayout.Space(8);
        DrawPlaceButton();
        EditorGUILayout.Space(12);
        DrawSelectionTools();
        EditorGUILayout.Space(12);
        DrawSceneSummary();

        EditorGUILayout.EndScrollView();
    }

    private void DrawPlacementSettings()
    {
        EditorGUILayout.LabelField("New points", EditorStyles.boldLabel);
        _pool = (LootPool)EditorGUILayout.ObjectField("Pool", _pool, typeof(LootPool), false);
        _spawnChance = EditorGUILayout.Slider("Spawn Chance", _spawnChance, 0f, 1f);
        _parent = (Transform)EditorGUILayout.ObjectField(
            new GUIContent("Parent", $"Where new points go. Empty = a '{DefaultRootName}' object. Drop a Loot Spawn Group here to fill it."),
            _parent, typeof(Transform), true);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        _alignToSurface = EditorGUILayout.Toggle(new GUIContent("Align To Surface", "Tilt points to match walls and slopes."), _alignToSurface);
        _randomYaw = EditorGUILayout.Toggle(new GUIContent("Random Rotation", "Random spin around the up axis so loot doesn't all face the same way."), _randomYaw);
        _heightOffset = EditorGUILayout.FloatField(new GUIContent("Height Offset", "Lift points off the surface (meters)."), _heightOffset);
        _paint = EditorGUILayout.Toggle(new GUIContent("Paint (drag)", "Drag to place a trail of points."), _paint);
        using (new EditorGUI.DisabledScope(!_paint))
            _paintSpacing = Mathf.Max(0.1f, EditorGUILayout.FloatField("Paint Spacing", _paintSpacing));

        if (_pool)
        {
            EditorGUILayout.Space(4);
            _previewTables.Clear();
            _previewTables.Add(_pool.Table);
            LootOddsGUI.DrawOdds(_previewTables, LootRollModifiers.Neutral);
        }
    }

    private void DrawPlaceButton()
    {
        var old = GUI.backgroundColor;
        GUI.backgroundColor = _placing ? new Color(1f, 0.55f, 0.4f) : new Color(0.5f, 0.9f, 0.5f);
        if (GUILayout.Button(_placing ? "Stop Placing (Esc)" : "Start Placing", GUILayout.Height(32)))
        {
            _placing = !_placing;
            if (_placing && SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.Focus();
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = old;

        if (_placing)
            EditorGUILayout.HelpBox("Click: place a point.  Shift + click: remove a point.  Esc: stop.", MessageType.Info);
        else if (!_pool)
            EditorGUILayout.HelpBox("No pool set: points will be placed empty (shown red). You can assign a pool later.", MessageType.None);
    }

    private void DrawSelectionTools()
    {
        var selected = SelectedPoints();
        EditorGUILayout.LabelField($"Selected points ({selected.Count})", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(selected.Count == 0))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Pool")) ApplyToSelected(selected, applyPool: true, applyChance: false);
                if (GUILayout.Button("Apply Chance")) ApplyToSelected(selected, applyPool: false, applyChance: true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Group Selected", "Put them under a new Loot Spawn Group that picks 1 of them."))) GroupSelected(selected);
                if (GUILayout.Button(new GUIContent("Drop To Ground", "Move each point down onto the collider below it."))) DropToGround(selected);
            }
        }

        if (_pool && GUILayout.Button($"Select All Points Using '{_pool.name}'"))
            Selection.objects = AllPoints().Where(p => p.Pool == _pool).Select(p => (Object)p.gameObject).ToArray();
    }

    private void DrawSceneSummary()
    {
        var points = AllPoints();
        EditorGUILayout.LabelField($"Scene: {points.Count} points", EditorStyles.boldLabel);

        foreach (var group in points.GroupBy(p => p.Pool).OrderByDescending(g => g.Count()))
        {
            string label = group.Key ? group.Key.name : "No pool";
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, group.Count().ToString());
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                    Selection.objects = group.Select(p => (Object)p.gameObject).ToArray();
            }
        }
    }

    private void OnSceneGUI(SceneView view)
    {
        if (!_placing) return;

        var e = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(controlId);

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            _placing = false;
            e.Use();
            Repaint();
            return;
        }

        if (e.alt) return;

        if (e.shift) HandleRemove(e);
        else HandlePlace(e);

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) view.Repaint();
    }

    private void HandlePlace(Event e)
    {
        if (!HandleUtility.PlaceObject(e.mousePosition, out var position, out var normal)) return;

        var rotation = SurfaceRotation(normal, 0f);
        position += normal * _heightOffset;

        Handles.color = _pool ? new Color(0.5f, 1f, 0.5f) : Color.red;
        using (new Handles.DrawingScope(Handles.color, Matrix4x4.TRS(position, rotation, Vector3.one)))
        {
            Handles.DrawWireCube(Vector3.up * 0.15f, Vector3.one * 0.3f);
            Handles.DrawLine(Vector3.zero, Vector3.up * 0.6f);
        }
        Handles.Label(position + Vector3.up * 0.7f, _pool ? $"{_pool.name}  {_spawnChance:P0}" : "No pool");

        bool click = e.type == EventType.MouseDown && e.button == 0;
        bool paint = _paint && e.type == EventType.MouseDrag && e.button == 0 &&
                     Vector3.Distance(position, _lastPaintPosition) >= _paintSpacing;

        if (click || paint)
        {
            Place(position, normal);
            _lastPaintPosition = position;
            e.Use();
        }
    }

    private void HandleRemove(Event e)
    {
        var target = PointUnderMouse(e.mousePosition);
        if (!target) return;

        Handles.color = Color.red;
        Handles.DrawWireDisc(target.transform.position, Vector3.up, 0.35f);
        Handles.Label(target.transform.position + Vector3.up * 0.7f, "Remove");

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Undo.DestroyObjectImmediate(target.gameObject);
            e.Use();
            Repaint();
        }
    }

    private void Place(Vector3 position, Vector3 normal)
    {
        var parent = ResolveParent();
        int index = parent ? parent.GetComponentsInChildren<LootSpawnPoint>(true).Length + 1 : 1;

        var go = new GameObject($"Loot - {(_pool ? _pool.name.Replace("POOL_", "") : "Empty")} {index:00}");
        Undo.RegisterCreatedObjectUndo(go, "Place loot spawn point");
        if (parent) Undo.SetTransformParent(go.transform, parent, "Place loot spawn point");

        go.transform.SetPositionAndRotation(position, SurfaceRotation(normal, _randomYaw ? Random.Range(0f, 360f) : 0f));

        var point = go.AddComponent<LootSpawnPoint>();
        var so = new SerializedObject(point);
        so.FindProperty("_pool").objectReferenceValue = _pool;
        so.FindProperty("_spawnChance").floatValue = _spawnChance;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(go.scene);
        Repaint();
    }

    private Quaternion SurfaceRotation(Vector3 normal, float yaw)
    {
        var up = _alignToSurface ? normal : Vector3.up;
        return Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, yaw, 0f);
    }

    private Transform ResolveParent()
    {
        if (_parent) return _parent;

        var scene = SceneManagerActiveScene();
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == DefaultRootName) return root.transform;

        var created = new GameObject(DefaultRootName);
        Undo.RegisterCreatedObjectUndo(created, "Create loot root");
        return created.transform;
    }

    private static UnityEngine.SceneManagement.Scene SceneManagerActiveScene() =>
        UnityEngine.SceneManagement.SceneManager.GetActiveScene();

    private static LootSpawnPoint PointUnderMouse(Vector2 mouse)
    {
        LootSpawnPoint best = null;
        float bestDistance = PickRadiusPixels;
        foreach (var point in AllPoints())
        {
            float d = Vector2.Distance(HandleUtility.WorldToGUIPoint(point.transform.position + Vector3.up * 0.15f), mouse);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = point;
            }
        }
        return best;
    }

    private void ApplyToSelected(List<LootSpawnPoint> points, bool applyPool, bool applyChance)
    {
        foreach (var point in points)
        {
            var so = new SerializedObject(point);
            if (applyPool) so.FindProperty("_pool").objectReferenceValue = _pool;
            if (applyChance) so.FindProperty("_spawnChance").floatValue = _spawnChance;
            so.ApplyModifiedProperties();
        }
    }

    private static void GroupSelected(List<LootSpawnPoint> points)
    {
        var center = points.Aggregate(Vector3.zero, (sum, p) => sum + p.transform.position) / points.Count;
        var commonParent = points[0].transform.parent;

        var go = new GameObject("Loot Spawn Group");
        Undo.RegisterCreatedObjectUndo(go, "Group loot spawn points");
        if (commonParent) Undo.SetTransformParent(go.transform, commonParent, "Group loot spawn points");
        go.transform.position = center;
        go.AddComponent<LootSpawnGroup>();

        foreach (var point in points)
            Undo.SetTransformParent(point.transform, go.transform, "Group loot spawn points");

        Selection.activeGameObject = go;
    }

    private static void DropToGround(List<LootSpawnPoint> points)
    {
        int missed = 0;
        foreach (var point in points)
        {
            var origin = point.transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            {
                Undo.RecordObject(point.transform, "Drop loot points to ground");
                point.transform.position = hit.point;
            }
            else missed++;
        }

        if (missed > 0)
            Debug.LogWarning($"[LootPlacer] {missed} point(s) had no collider below them and were not moved.");
    }

    private static List<LootSpawnPoint> SelectedPoints() =>
        Selection.gameObjects.Select(g => g.GetComponent<LootSpawnPoint>()).Where(p => p).ToList();

    private static List<LootSpawnPoint> AllPoints() =>
        FindObjectsByType<LootSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
}
}
