using Project.Core.Match;
using UnityEditor;
using UnityEngine;

namespace Project.Core.Editor
{
internal static class SpawnPointGizmos
{
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void Draw(SpawnPoint point, GizmoType type)
    {
        bool selected = (type & GizmoType.Selected) != 0;

        var info = point.Validate(true);
        Color mainColor;

        switch (info.Status)
        {
            case SpawnPointStatus.Valid:
                mainColor = new Color(0.13f, 0.77f, 0.36f, selected ? 0.95f : 0.75f);
                break;
            case SpawnPointStatus.Obstructed:
                mainColor = new Color(0.94f, 0.27f, 0.27f, selected ? 0.95f : 0.85f);
                break;
            case SpawnPointStatus.Floating:
                mainColor = new Color(0.96f, 0.62f, 0.07f, selected ? 0.95f : 0.85f);
                break;
            case SpawnPointStatus.SteepSlope:
            case SpawnPointStatus.Underground:
                mainColor = new Color(0.98f, 0.45f, 0.09f, selected ? 0.95f : 0.85f);
                break;
            case SpawnPointStatus.Disabled:
            default:
                mainColor = new Color(0.45f, 0.50f, 0.58f, selected ? 0.55f : 0.35f);
                break;
        }

        Vector3 pos = point.transform.position;
        Vector3 fwd = point.transform.forward;
        Vector3 rgt = point.transform.right;
        Vector3 up = Vector3.up;

        UnityEditor.Handles.color = mainColor;
        UnityEditor.Handles.DrawWireDisc(pos, up, point.CapsuleRadius);
        UnityEditor.Handles.color = new Color(mainColor.r, mainColor.g, mainColor.b, selected ? 0.22f : 0.10f);
        UnityEditor.Handles.DrawSolidDisc(pos, up, point.CapsuleRadius);

        float halfHeightMinusRadius = Mathf.Max(0.01f, (point.CapsuleHeight * 0.5f) - point.CapsuleRadius);
        Vector3 worldCenter = point.transform.TransformPoint(point.CapsuleCenter);
        Vector3 bottomSphere = worldCenter - up * halfHeightMinusRadius;
        Vector3 topSphere = worldCenter + up * halfHeightMinusRadius;

        UnityEditor.Handles.color = mainColor;

        UnityEditor.Handles.DrawWireDisc(bottomSphere, up, point.CapsuleRadius);
        UnityEditor.Handles.DrawWireArc(bottomSphere, point.transform.right, point.transform.forward, 180f, point.CapsuleRadius);
        UnityEditor.Handles.DrawWireArc(bottomSphere, -point.transform.forward, point.transform.right, 180f, point.CapsuleRadius);

        UnityEditor.Handles.DrawWireDisc(topSphere, up, point.CapsuleRadius);
        UnityEditor.Handles.DrawWireArc(topSphere, -point.transform.right, point.transform.forward, 180f, point.CapsuleRadius);
        UnityEditor.Handles.DrawWireArc(topSphere, point.transform.forward, point.transform.right, 180f, point.CapsuleRadius);

        UnityEditor.Handles.DrawLine(bottomSphere + fwd * point.CapsuleRadius, topSphere + fwd * point.CapsuleRadius);
        UnityEditor.Handles.DrawLine(bottomSphere - fwd * point.CapsuleRadius, topSphere - fwd * point.CapsuleRadius);
        UnityEditor.Handles.DrawLine(bottomSphere + rgt * point.CapsuleRadius, topSphere + rgt * point.CapsuleRadius);
        UnityEditor.Handles.DrawLine(bottomSphere - rgt * point.CapsuleRadius, topSphere - rgt * point.CapsuleRadius);

        Vector3 eyePos = pos + up * 1.6f;
        Vector3 arrowTip = eyePos + fwd * 0.9f;
        UnityEditor.Handles.color = mainColor;
        UnityEditor.Handles.DrawLine(eyePos, arrowTip);

        Vector3 arrowBack = arrowTip - fwd * 0.25f;
        UnityEditor.Handles.DrawLine(arrowTip, arrowBack + rgt * 0.12f);
        UnityEditor.Handles.DrawLine(arrowTip, arrowBack - rgt * 0.12f);
        UnityEditor.Handles.DrawLine(arrowBack + rgt * 0.12f, arrowBack - rgt * 0.12f);

        if (info.Status == SpawnPointStatus.Floating)
        {
            UnityEditor.Handles.color = new Color(0.96f, 0.62f, 0.07f, 0.9f);
            if (info.HasGroundHit)
            {
                UnityEditor.Handles.DrawDottedLine(pos, info.GroundHit.point, 3f);
                UnityEditor.Handles.DrawWireDisc(info.GroundHit.point, info.GroundHit.normal, 0.25f);
            }
            else
            {
                UnityEditor.Handles.DrawDottedLine(pos, pos - up * point.GroundRaycastDistance, 3f);
            }
        }

        if (info is { Status: SpawnPointStatus.Obstructed, OverlappingColliders: not null })
        {
            UnityEditor.Handles.color = new Color(0.94f, 0.27f, 0.27f, 0.9f);
            for (int i = 0; i < info.OverlappingColliders.Length; i++)
            {
                var col = info.OverlappingColliders[i];
                if (col)
                {
                    UnityEditor.Handles.DrawLine(worldCenter, col.bounds.center);
                    UnityEditor.Handles.DrawWireCube(col.bounds.center, col.bounds.size);
                }
            }
        }

        var labelStyle = new GUIStyle
        {
            normal = { textColor = mainColor },
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        string labelText = $"{point.gameObject.name}";
        UnityEditor.Handles.Label(topSphere + (up * 1f), labelText, labelStyle);


    }

}
}
