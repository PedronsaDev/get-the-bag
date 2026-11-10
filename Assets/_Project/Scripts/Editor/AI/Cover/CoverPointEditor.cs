#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.AI.Cover.Editor
{
[CustomEditor(typeof(CoverPoint))]
[CanEditMultipleObjects]
public class CoverPointEditor : UnityEditor.Editor
{
    private const float ProtectionAngle = 130f;

    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        InspectorElement.FillDefaultInspector(root, serializedObject, this);

        var summary = new HelpBox(string.Empty, HelpBoxMessageType.Info);
        root.Add(summary);
        root.schedule.Execute(() => summary.text = Describe((CoverPoint)target)).Every(250);
        return root;
    }

    private static string Describe(CoverPoint point)
    {
        if (!point)
            return string.Empty;
        return $"{point.Height} cover, quality {point.Quality:0.00}. Usable peeks: {point.PeekDirections}.";
    }

    private void OnSceneGUI()
    {
        var point = (CoverPoint)target;
        if (!point)
            return;

        Vector3 pos = point.Position;
        Vector3 fwd = point.Forward;
        float height = CoverDimensions.BodyHeight(point.Height);
        Color baseColor = GetCoverStateColor(point);

        DrawStanceVolume(pos, height, baseColor);
        DrawProtectionArc(pos, fwd, baseColor);
        DrawPeekMarkers(point, pos, fwd, height);
        DrawDirectionHandle(point, pos, fwd);
    }

    private static Color GetCoverStateColor(CoverPoint point)
    {
        if (Application.isPlaying && point.IsReserved)
            return new Color(1f, 0.3f, 0.2f, 0.9f);

        return point.Height == CoverHeight.High
            ? new Color(0.2f, 0.8f, 1f, 0.9f)
            : new Color(0.3f, 1f, 0.5f, 0.9f);
    }

    private static void DrawStanceVolume(Vector3 pos, float height, Color color)
    {
        float radius = CoverDimensions.AgentRadius;
        Handles.color = color;
        Handles.DrawWireDisc(pos, Vector3.up, radius);
        Vector3 topPos = pos + Vector3.up * height;
        Handles.DrawWireDisc(topPos, Vector3.up, radius);
        Handles.DrawLine(pos + Vector3.forward * radius, topPos + Vector3.forward * radius);
        Handles.DrawLine(pos - Vector3.forward * radius, topPos - Vector3.forward * radius);
        Handles.DrawLine(pos + Vector3.right * radius, topPos + Vector3.right * radius);
        Handles.DrawLine(pos - Vector3.right * radius, topPos - Vector3.right * radius);
    }

    private static void DrawProtectionArc(Vector3 pos, Vector3 fwd, Color color)
    {
        Vector3 arcStart = Quaternion.Euler(0, -ProtectionAngle * 0.5f, 0) * fwd;
        Handles.color = new Color(color.r, color.g, color.b, 0.12f);
        Handles.DrawSolidArc(pos, Vector3.up, arcStart, ProtectionAngle, 1.2f);

        Handles.color = color;
        Handles.DrawWireArc(pos, Vector3.up, arcStart, ProtectionAngle, 1.2f);
    }

    private static void DrawPeekMarkers(CoverPoint point, Vector3 pos, Vector3 fwd, float height)
    {
        var peeks = point.PeekDirections;

        if (point.TryGetPeekDestination(CoverPeekDirection.Left, out Vector3 leftPeekPos))
            DrawPeekNode(pos, leftPeekPos, fwd, "PEEK L");

        if (point.TryGetPeekDestination(CoverPeekDirection.Right, out Vector3 rightPeekPos))
            DrawPeekNode(pos, rightPeekPos, fwd, "PEEK R");

        if ((peeks & CoverPeekDirection.Over) != 0)
        {
            Vector3 overPos = pos + Vector3.up * (height + 0.35f);
            DrawPeekNode(pos + Vector3.up * height, overPos, fwd, "OVER (POSTURE)");
        }
    }

    private static void DrawPeekNode(Vector3 origin, Vector3 peekPos, Vector3 fwd, string label)
    {
        Handles.color = Color.yellow;
        Handles.DrawDottedLine(origin, peekPos, 4f);
        Handles.DrawSolidDisc(peekPos, Vector3.up, 0.12f);
        Handles.DrawLine(peekPos, peekPos + fwd * 0.6f);
        Handles.Label(peekPos + Vector3.up * 0.15f, label, EditorStyles.boldLabel);
    }

    private static void DrawDirectionHandle(CoverPoint point, Vector3 pos, Vector3 fwd)
    {
        EditorGUI.BeginChangeCheck();
        Vector3 newTarget = Handles.Slider(pos + fwd * 1.5f, fwd, 0.2f, Handles.ConeHandleCap, 0.1f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(point.transform, "Adjust Cover Direction");
            point.transform.forward = (newTarget - pos).normalized;
        }
    }
}
}
#endif
