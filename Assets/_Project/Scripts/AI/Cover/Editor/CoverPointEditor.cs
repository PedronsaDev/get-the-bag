#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Project.AI.Cover.Editor
{
[CustomEditor(typeof(CoverPoint))]
[CanEditMultipleObjects]
public class CoverPointEditor : UnityEditor.Editor
{
    // Tactical dimensions (matching human capsule standards)
    private const float LowCoverHeight = 0.9f;
    private const float HighCoverHeight = 1.8f;
    private const float AgentRadius = 0.35f;
    private const float PeekStepDistance = 0.75f;
    private const float ProtectionAngle = 130f; // 130 degree defensive coverage arc

    private void OnSceneGUI()
    {
        var point = (CoverPoint)target;
        if (!point) return;

        Vector3 pos = point.Position;
        Vector3 fwd = point.Forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
        float height = point.Height == CoverHeight.High ? HighCoverHeight : LowCoverHeight;

        // 1. Color coding based on Reservation State (in Play Mode)
        Color baseColor = GetCoverStateColor(point);

        // 2. Draw Stance Volume (Wire Cylinder/Capsule representing character in cover)
        DrawStanceVolume(pos, fwd, height, baseColor);

        // 3. Draw Protective Front Arc
        DrawProtectionArc(pos, fwd, baseColor);

        // 4. Draw Peek Direction Markers (Step Left, Step Right, Pop Over)
        DrawPeekMarkers(point, pos, fwd, right, height, baseColor);

        // 5. Interactive Rotation Handle (Optional: lets designers drag facing direction)
        DrawDirectionHandle(point, pos, fwd);
    }

    private Color GetCoverStateColor(CoverPoint point)
    {
        if (Application.isPlaying && point.IsReserved)
        {
            return new Color(1f, 0.3f, 0.2f, 0.9f); // Reserved: Red/Orange
        }

        return point.Height == CoverHeight.High
            ? new Color(0.2f, 0.8f, 1f, 0.9f) // High: Cyan
            : new Color(0.3f, 1f, 0.5f, 0.9f); // Low: Green
    }

    private void DrawStanceVolume(Vector3 pos, Vector3 fwd, float height, Color color)
    {
        Handles.color = color;
        // Base ring on ground
        Handles.DrawWireDisc(pos, Vector3.up, AgentRadius);
        // Top ring at cover height
        Vector3 topPos = pos + Vector3.up * height;
        Handles.DrawWireDisc(topPos, Vector3.up, AgentRadius);
        // Vertical connecting struts
        Handles.DrawLine(pos + Vector3.forward * AgentRadius, topPos + Vector3.forward * AgentRadius);
        Handles.DrawLine(pos - Vector3.forward * AgentRadius, topPos - Vector3.forward * AgentRadius);
        Handles.DrawLine(pos + Vector3.right * AgentRadius, topPos + Vector3.right * AgentRadius);
        Handles.DrawLine(pos - Vector3.right * AgentRadius, topPos - Vector3.right * AgentRadius);
    }

    private void DrawProtectionArc(Vector3 pos, Vector3 fwd, Color color)
    {
        // Protective viewing arc (fan on the ground)
        Vector3 arcStart = Quaternion.Euler(0, -ProtectionAngle * 0.5f, 0) * fwd;
        Color fillColor = new Color(color.r, color.g, color.b, 0.12f);
        Handles.color = fillColor;
        Handles.DrawSolidArc(pos, Vector3.up, arcStart, ProtectionAngle, 1.2f);

        Handles.color = color;
        Handles.DrawWireArc(pos, Vector3.up, arcStart, ProtectionAngle, 1.2f);
    }

    private void DrawPeekMarkers(CoverPoint point, Vector3 pos, Vector3 fwd, Vector3 right, float height, Color color)
    {
        var peeks = point.PeekDirections;

        // Step Left Peek
        if ((peeks & CoverPeekDirection.Left) != 0)
        {
            Vector3 leftPeekPos = pos - (right * PeekStepDistance);
            DrawPeekNode(pos, leftPeekPos, fwd, "PEEK L");
        }

        // Step Right Peek
        if ((peeks & CoverPeekDirection.Right) != 0)
        {
            Vector3 rightPeekPos = pos + (right * PeekStepDistance);
            DrawPeekNode(pos, rightPeekPos, fwd, "PEEK R");
        }

        // Over Peek (for Low Cover)
        if ((peeks & CoverPeekDirection.Over) != 0)
        {
            Vector3 overPos = pos + Vector3.up * (height + 0.35f);
            DrawPeekNode(pos + Vector3.up * height, overPos, fwd, "PEEK OVER");
        }
    }

    private void DrawPeekNode(Vector3 origin, Vector3 peekPos, Vector3 fwd, string label)
    {
        Handles.color = Color.yellow;
        Handles.DrawDottedLine(origin, peekPos, 4f);
        Handles.DrawSolidDisc(peekPos, Vector3.up, 0.12f);
        Handles.DrawLine(peekPos, peekPos + fwd * 0.6f);

        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 9,
            normal = { textColor = Color.yellow }
        };
        Handles.Label(peekPos + Vector3.up * 0.15f, label, labelStyle);
    }

    private void DrawDirectionHandle(CoverPoint point, Vector3 pos, Vector3 fwd)
    {
        // Arrow handle to adjust forward direction visually
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