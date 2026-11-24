using System;
using UnityEditor;
using UnityEngine;

namespace Project.AI.Patrol.Editor
{
public sealed class PatrolPathSceneTool
{
    private const float PointSize = 0.12f;
    private const float PickSize = 0.18f;
    private const float RayDistance = 2000f;

    private static readonly Color OnNavMeshColor = new(0.3f, 0.9f, 0.4f);
    private static readonly Color OffNavMeshColor = new(1f, 0.35f, 0.3f);
    private static readonly Color SelectedColor = new(1f, 0.85f, 0.2f);

    public AIPatrolRoute Route;
    public int SelectedIndex = -1;
    public bool AddMode;
    /// <summary>Handles and clicks only work while true. The route is still drawn when it is false.</summary>
    public bool Editable;
    public bool SnapToNavMesh = true;
    public float SnapRadius = 2f;
    public bool FaceAlongPath = true;

    public event Action PathChanged;
    public event Action<int> PointSelected;
    public event Action AddModeEnded;

    public void OnSceneGui(SceneView view)
    {
        if (!Route)
            return;

        if (Event.current.type == EventType.Repaint)
            DrawRoute();

        if (AddMode)
            HandleAddMode(view);
        else if (Editable)
            HandlePointEditing();
    }

    private void DrawRoute()
    {
        int count = Route.PointCount;
        if (count == 0)
            return;

        DrawLines(count);
        for (int i = 0; i < count; i++)
        {
            var point = Route.GetPoint(i);
            float size = HandleUtility.GetHandleSize(point.position);
            bool onNavMesh = PatrolPathUtility.TrySnap(point.position, 0.5f, out _);

            Handles.color = i == SelectedIndex ? SelectedColor : onNavMesh ? OnNavMeshColor : OffNavMeshColor;
            Handles.SphereHandleCap(0, point.position, Quaternion.identity, size * PointSize * 2f, EventType.Repaint);
            Handles.Label(point.position + Vector3.up * size * 0.3f, (i + 1).ToString());

            Handles.color = Color.cyan;
            Handles.ArrowHandleCap(0, point.position, Quaternion.LookRotation(point.forward), size * 0.7f, EventType.Repaint);
        }
    }

    private void DrawLines(int count)
    {
        Handles.color = Color.yellow;
        int segments = Route.Loop && count > 1 ? count : count - 1;
        for (int i = 0; i < segments; i++)
        {
            Vector3 a = Route.GetPoint(i).position;
            Vector3 b = Route.GetPoint((i + 1) % count).position;
            Handles.DrawAAPolyLine(3f, a, b);

            Vector3 direction = b - a;
            if (direction.sqrMagnitude < 0.01f)
                continue;
            Vector3 middle = (a + b) * 0.5f;
            Handles.ConeHandleCap(0, middle, Quaternion.LookRotation(direction),
                HandleUtility.GetHandleSize(middle) * 0.15f, EventType.Repaint);
        }
    }

    private void HandlePointEditing()
    {
        for (int i = 0; i < Route.PointCount; i++)
        {
            var point = Route.GetPoint(i);
            float size = HandleUtility.GetHandleSize(point.position);

            if (i != SelectedIndex)
            {
                Handles.color = Color.clear;
                if (Handles.Button(point.position, Quaternion.identity, size * PointSize, size * PickSize,
                        Handles.SphereHandleCap))
                    SelectPoint(i);
                continue;
            }
            EditSelectedPoint(point, size);
        }
    }

    private void EditSelectedPoint(Transform point, float size)
    {
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.PositionHandle(point.position, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(point, "Move Patrol Point");
            point.position = moved;
            PathChanged?.Invoke();
        }

        Handles.color = Color.cyan;
        EditorGUI.BeginChangeCheck();
        Quaternion turned = Handles.Disc(point.rotation, point.position, Vector3.up, size * 0.9f, false, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(point, "Rotate Patrol Point");
            point.rotation = turned;
            PathChanged?.Invoke();
        }
    }

    private void HandleAddMode(SceneView view)
    {
        // Claim left clicks so the scene view does not change the selection while placing points.
        if (Event.current.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        bool hasPoint = TryGetPlacement(out Vector3 placement);
        bool insert = Event.current.control || Event.current.command;

        if (hasPoint && Event.current.type == EventType.Repaint)
            DrawPlacementPreview(placement, insert);

        switch (Event.current.type)
        {
            case EventType.MouseMove:
                view.Repaint();
                break;
            case EventType.MouseDown when Event.current.button == 0 && !Event.current.alt && hasPoint:
                PlacePoint(placement, insert);
                Event.current.Use();
                break;
            case EventType.KeyDown when Event.current.keyCode == KeyCode.Escape:
                AddMode = false;
                AddModeEnded?.Invoke();
                Event.current.Use();
                break;
        }
    }

    private void DrawPlacementPreview(Vector3 placement, bool insert)
    {
        float size = HandleUtility.GetHandleSize(placement);
        Handles.color = insert ? SelectedColor : Color.white;
        Handles.DrawWireDisc(placement, Vector3.up, size * 0.2f);

        if (Route.PointCount == 0)
            return;

        Handles.color = new Color(1f, 1f, 1f, 0.5f);
        if (insert && Route.PointCount > 1)
        {
            int start = PatrolPathUtility.NearestSegmentStart(Route, placement);
            Handles.DrawDottedLine(Route.GetPoint(start).position, placement, 4f);
            Handles.DrawDottedLine(placement, Route.GetPoint((start + 1) % Route.PointCount).position, 4f);
        }
        else
        {
            Handles.DrawDottedLine(Route.GetPoint(Route.PointCount - 1).position, placement, 4f);
        }
    }

    private void PlacePoint(Vector3 position, bool insert)
    {
        int index = insert && Route.PointCount > 1
            ? PatrolPathUtility.NearestSegmentStart(Route, position) + 1
            : -1;
        int added = PatrolPathUtility.AddPoint(Route, position, index);
        if (FaceAlongPath)
            PatrolPathUtility.FaceAlongPath(Route);

        SelectedIndex = added;
        PathChanged?.Invoke();
        PointSelected?.Invoke(added);
    }

    private void SelectPoint(int index)
    {
        SelectedIndex = index;
        PointSelected?.Invoke(index);
    }

    private bool TryGetPlacement(out Vector3 position)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        if (UnityEngine.Physics.Raycast(ray, out RaycastHit hit, RayDistance, ~0, QueryTriggerInteraction.Ignore))
            position = hit.point;
        else if (!TryIntersectPlane(ray, out position))
            return false;

        if (SnapToNavMesh && PatrolPathUtility.TrySnap(position, SnapRadius, out Vector3 snapped))
            position = snapped;
        return true;
    }

    private bool TryIntersectPlane(Ray ray, out Vector3 position)
    {
        float height = Route.PointCount > 0 ? Route.GetPoint(Route.PointCount - 1).position.y : Route.transform.position.y;
        var plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
        if (plane.Raycast(ray, out float distance))
        {
            position = ray.GetPoint(distance);
            return true;
        }
        position = default;
        return false;
    }
}
}
