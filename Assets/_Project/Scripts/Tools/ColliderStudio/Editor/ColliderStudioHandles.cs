using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Tools.ColliderStudio.Editor
{
    public static class ColliderStudioHandles
    {
        private static readonly Vector3[] BoxCornerSigns =
        {
            new Vector3(-1, -1, -1),
            new Vector3( 1, -1, -1),
            new Vector3( 1, -1,  1),
            new Vector3(-1, -1,  1),
            new Vector3(-1,  1, -1),
            new Vector3( 1,  1, -1),
            new Vector3( 1,  1,  1),
            new Vector3(-1,  1,  1),
        };

        private static readonly Vector3[] BoxFaceNormals =
        {
            Vector3.right,
            Vector3.left,
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back
        };

        public static void DrawColliderHandles(Collider collider)
        {
            if (collider == null || !collider.enabled) return;

            Transform targetTransform = collider.transform;
            Color baseColor = ColliderStudioSettings.GetEffectiveColor(collider);

            CompareFunction previousZTest = Handles.zTest;
            Matrix4x4 previousMatrix = Handles.matrix;
            Color previousColor = Handles.color;

            try
            {
                Handles.zTest = ColliderStudioSettings.XRay ? CompareFunction.Always : CompareFunction.LessEqual;

                switch (collider)
                {
                    case BoxCollider box:
                        DrawBoxCollider(box, targetTransform, baseColor);
                        break;
                    case SphereCollider sphere:
                        DrawSphereCollider(sphere, targetTransform, baseColor);
                        break;
                    case CapsuleCollider capsule:
                        DrawCapsuleCollider(capsule, targetTransform, baseColor);
                        break;
                    case MeshCollider meshCol:
                        DrawMeshCollider(meshCol, targetTransform, baseColor);
                        break;
                }
            }
            finally
            {
                Handles.zTest = previousZTest;
                Handles.matrix = previousMatrix;
                Handles.color = previousColor;
            }
        }

        #region BoxCollider Editing & Gizmos

        private static void DrawBoxCollider(BoxCollider box, Transform targetTransform, Color color)
        {
            Vector3 center = box.center;
            Vector3 size = box.size;

            // 1. Draw Volume Gizmo in Local Space
            Handles.matrix = targetTransform.localToWorldMatrix;
            DrawBoxVolumeGizmo(center, size, color);
            Handles.matrix = Matrix4x4.identity;

            // Clamping bounds in world space so small objects never have oversized handles
            Vector3 worldExtent = Vector3.Scale(size, targetTransform.lossyScale);
            float minDim = Mathf.Min(Mathf.Abs(worldExtent.x), Mathf.Min(Mathf.Abs(worldExtent.y), Mathf.Abs(worldExtent.z)));
            float maxCapSize = Mathf.Max(0.001f, minDim * 0.18f);

            var mode = ColliderStudioSettings.InteractionMode;

            // 2. Center Translation Handle (World Space)
            if (mode == HandleInteractionMode.All || mode == HandleInteractionMode.CenterOnly)
            {
                DrawCenterHandle(center, targetTransform, newCenter =>
                {
                    Undo.RecordObject(box, "Move BoxCollider Center");
                    box.center = SnapVector(newCenter);
                });
            }

            // 3. Corner Scaling Handles (8 Corners in World Space)
            if (mode == HandleInteractionMode.All || mode == HandleInteractionMode.CornersOnly)
            {
                DrawBoxCornerHandles(box, targetTransform, color, maxCapSize);
            }

            // 4. Face Pull Handles (6 Faces in World Space)
            if (mode == HandleInteractionMode.All || mode == HandleInteractionMode.FacesOnly)
            {
                DrawBoxFaceHandles(box, targetTransform, color, maxCapSize);
            }
        }

        private static void DrawBoxVolumeGizmo(Vector3 center, Vector3 size, Color color)
        {
            Vector3 half = size * 0.5f;

            Vector3 p0 = center + new Vector3(-half.x, -half.y, -half.z);
            Vector3 p1 = center + new Vector3( half.x, -half.y, -half.z);
            Vector3 p2 = center + new Vector3( half.x, -half.y,  half.z);
            Vector3 p3 = center + new Vector3(-half.x, -half.y,  half.z);

            Vector3 p4 = center + new Vector3(-half.x,  half.y, -half.z);
            Vector3 p5 = center + new Vector3( half.x,  half.y, -half.z);
            Vector3 p6 = center + new Vector3( half.x,  half.y,  half.z);
            Vector3 p7 = center + new Vector3(-half.x,  half.y,  half.z);

            Color fillColor = color;
            fillColor.a = ColliderStudioSettings.FillAlpha;

            Color wireColor = color;
            wireColor.a = ColliderStudioSettings.WireframeAlpha;

            if (ColliderStudioSettings.FillFaces && fillColor.a > 0.001f)
            {
                Handles.DrawSolidRectangleWithOutline(new[] { p0, p1, p2, p3 }, fillColor, Color.clear); // Bottom
                Handles.DrawSolidRectangleWithOutline(new[] { p4, p5, p6, p7 }, fillColor, Color.clear); // Top
                Handles.DrawSolidRectangleWithOutline(new[] { p0, p1, p5, p4 }, fillColor, Color.clear); // Front (-Z)
                Handles.DrawSolidRectangleWithOutline(new[] { p2, p3, p7, p6 }, fillColor, Color.clear); // Back (+Z)
                Handles.DrawSolidRectangleWithOutline(new[] { p0, p3, p7, p4 }, fillColor, Color.clear); // Left (-X)
                Handles.DrawSolidRectangleWithOutline(new[] { p1, p2, p6, p5 }, fillColor, Color.clear); // Right (+X)
            }

            Handles.color = wireColor;
            Handles.DrawLine(p0, p1);
            Handles.DrawLine(p1, p2);
            Handles.DrawLine(p2, p3);
            Handles.DrawLine(p3, p0);

            Handles.DrawLine(p4, p5);
            Handles.DrawLine(p5, p6);
            Handles.DrawLine(p6, p7);
            Handles.DrawLine(p7, p4);

            Handles.DrawLine(p0, p4);
            Handles.DrawLine(p1, p5);
            Handles.DrawLine(p2, p6);
            Handles.DrawLine(p3, p7);
        }

        private static void DrawBoxCornerHandles(BoxCollider box, Transform targetTransform, Color color, float maxCapSize)
        {
            Vector3 center = box.center;
            Vector3 size = box.size;
            bool symmetric = ColliderStudioSettings.CornerSymmetric || Event.current.alt;
            Quaternion rot = targetTransform.rotation;

            Handles.color = color;

            for (int i = 0; i < BoxCornerSigns.Length; i++)
            {
                Vector3 sign = BoxCornerSigns[i];
                Vector3 localCorner = center + Vector3.Scale(size * 0.5f, sign);
                Vector3 worldCorner = targetTransform.TransformPoint(localCorner);

                float screenHandleSize = HandleUtility.GetHandleSize(worldCorner) * 0.035f * ColliderStudioSettings.HandleScale;
                float handleSize = Mathf.Min(screenHandleSize, maxCapSize);
                int id = GUIUtility.GetControlID(FocusType.Passive);

                EditorGUI.BeginChangeCheck();
                Vector3 newWorldCorner = Handles.FreeMoveHandle(
                    id,
                    worldCorner,
                    handleSize,
                    Vector3.zero,
                    (controlID, position, rotation, capSize, eventType) =>
                    {
                        Handles.CubeHandleCap(controlID, position, rot, capSize, eventType);
                    }
                );

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(box, "Scale BoxCollider Corner");

                    Vector3 newLocalCorner = targetTransform.InverseTransformPoint(newWorldCorner);
                    newLocalCorner = SnapVector(newLocalCorner);

                    if (symmetric)
                    {
                        Vector3 halfDelta = new Vector3(
                            Mathf.Abs(newLocalCorner.x - center.x),
                            Mathf.Abs(newLocalCorner.y - center.y),
                            Mathf.Abs(newLocalCorner.z - center.z)
                        );
                        box.size = Vector3.Max(halfDelta * 2f, new Vector3(0.01f, 0.01f, 0.01f));
                    }
                    else
                    {
                        Vector3 oppCorner = center - Vector3.Scale(size * 0.5f, sign);

                        Vector3 newCenter = (newLocalCorner + oppCorner) * 0.5f;
                        Vector3 newSize = new Vector3(
                            Mathf.Max(0.01f, Mathf.Abs(newLocalCorner.x - oppCorner.x)),
                            Mathf.Max(0.01f, Mathf.Abs(newLocalCorner.y - oppCorner.y)),
                            Mathf.Max(0.01f, Mathf.Abs(newLocalCorner.z - oppCorner.z))
                        );

                        box.center = newCenter;
                        box.size = newSize;
                    }
                }
            }
        }

        private static void DrawBoxFaceHandles(BoxCollider box, Transform targetTransform, Color color, float maxCapSize)
        {
            Vector3 center = box.center;
            Vector3 size = box.size;
            bool symmetric = ColliderStudioSettings.FaceSymmetric || Event.current.alt;

            Handles.color = color * 0.95f;

            for (int i = 0; i < BoxFaceNormals.Length; i++)
            {
                Vector3 normal = BoxFaceNormals[i];
                Vector3 localFace = center + Vector3.Scale(size * 0.5f, normal);
                Vector3 worldFace = targetTransform.TransformPoint(localFace);
                Vector3 worldNormal = targetTransform.TransformDirection(normal).normalized;

                float screenHandleSize = HandleUtility.GetHandleSize(worldFace) * 0.025f * ColliderStudioSettings.HandleScale;
                float handleSize = Mathf.Min(screenHandleSize, maxCapSize * 0.75f);
                int id = GUIUtility.GetControlID(FocusType.Passive);

                EditorGUI.BeginChangeCheck();
                Vector3 newWorldFace = Handles.Slider(id, worldFace, worldNormal, handleSize, Handles.DotHandleCap, 0f);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(box, "Scale BoxCollider Face");

                    Vector3 newLocalFace = targetTransform.InverseTransformPoint(newWorldFace);
                    newLocalFace = SnapVector(newLocalFace);
                    int axis = normal.x != 0 ? 0 : (normal.y != 0 ? 1 : 2);
                    float sign = normal[axis];

                    if (symmetric)
                    {
                        float halfDist = Mathf.Abs(newLocalFace[axis] - center[axis]);
                        Vector3 newSize = size;
                        newSize[axis] = Mathf.Max(0.01f, halfDist * 2f);
                        box.size = newSize;
                    }
                    else
                    {
                        float oppPos = center[axis] - (size[axis] * 0.5f * sign);
                        float currentPos = newLocalFace[axis];

                        float newDim = Mathf.Max(0.01f, Mathf.Abs(currentPos - oppPos));
                        float newMid = (currentPos + oppPos) * 0.5f;

                        Vector3 newCenter = center;
                        newCenter[axis] = newMid;

                        Vector3 newSize = size;
                        newSize[axis] = newDim;

                        box.center = newCenter;
                        box.size = newSize;
                    }
                }
            }
        }

        #endregion

        #region SphereCollider Editing & Gizmos

        private static void DrawSphereCollider(SphereCollider sphere, Transform targetTransform, Color color)
        {
            Vector3 center = sphere.center;
            float radius = sphere.radius;

            Color fillColor = color;
            fillColor.a = ColliderStudioSettings.FillAlpha;

            Color wireColor = color;
            wireColor.a = ColliderStudioSettings.WireframeAlpha;

            // Geometry in Local Matrix
            Handles.matrix = targetTransform.localToWorldMatrix;
            Handles.color = wireColor;
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.right, radius);
            Handles.DrawWireDisc(center, Vector3.forward, radius);

            if (ColliderStudioSettings.FillFaces && fillColor.a > 0.001f)
            {
                Handles.color = fillColor;
                Handles.DrawSolidDisc(center, Vector3.up, radius);
                Handles.DrawSolidDisc(center, Vector3.right, radius);
                Handles.DrawSolidDisc(center, Vector3.forward, radius);
            }

            Handles.matrix = Matrix4x4.identity;

            var mode = ColliderStudioSettings.InteractionMode;
            float maxScale = Mathf.Max(Mathf.Abs(targetTransform.lossyScale.x), Mathf.Max(Mathf.Abs(targetTransform.lossyScale.y), Mathf.Abs(targetTransform.lossyScale.z)));
            float worldRadius = radius * Mathf.Max(0.001f, maxScale);
            float maxCapSize = Mathf.Max(0.001f, worldRadius * 0.18f);

            // Center Move Handle
            if (mode == HandleInteractionMode.All || mode == HandleInteractionMode.CenterOnly)
            {
                DrawCenterHandle(center, targetTransform, newCenter =>
                {
                    Undo.RecordObject(sphere, "Move SphereCollider Center");
                    sphere.center = SnapVector(newCenter);
                });
            }

            // Radial Handles (6 directions)
            if (mode == HandleInteractionMode.All || mode == HandleInteractionMode.FacesOnly || mode == HandleInteractionMode.CornersOnly)
            {
                Handles.color = color;
                for (int i = 0; i < BoxFaceNormals.Length; i++)
                {
                    Vector3 dir = BoxFaceNormals[i];
                    Vector3 localPos = center + dir * radius;
                    Vector3 worldPos = targetTransform.TransformPoint(localPos);
                    Vector3 worldDir = targetTransform.TransformDirection(dir).normalized;

                    float screenHandleSize = HandleUtility.GetHandleSize(worldPos) * 0.025f * ColliderStudioSettings.HandleScale;
                    float handleSize = Mathf.Min(screenHandleSize, maxCapSize * 0.75f);
                    int id = GUIUtility.GetControlID(FocusType.Passive);

                    EditorGUI.BeginChangeCheck();
                    Vector3 newWorld = Handles.Slider(id, worldPos, worldDir, handleSize, Handles.DotHandleCap, 0f);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(sphere, "Scale SphereCollider Radius");
                        Vector3 newLocal = targetTransform.InverseTransformPoint(newWorld);
                        float newRadius = Mathf.Max(0.01f, Vector3.Distance(newLocal, center));
                        if (ColliderStudioSettings.SnapEnabled)
                        {
                            newRadius = Mathf.Round(newRadius / ColliderStudioSettings.SnapValue) * ColliderStudioSettings.SnapValue;
                        }
                        sphere.radius = Mathf.Max(0.01f, newRadius);
                    }
                }
            }
        }

        #endregion

        #region CapsuleCollider Editing & Gizmos

        private static void DrawCapsuleCollider(CapsuleCollider capsule, Transform targetTransform, Color color)
        {
            Vector3 center = capsule.center;
            float radius = capsule.radius;
            float height = Mathf.Max(capsule.height, radius * 2f);
            int direction = capsule.direction; // 0=X, 1=Y, 2=Z

            Vector3 axis = direction == 0 ? Vector3.right : (direction == 1 ? Vector3.up : Vector3.forward);
            Vector3 sideA = direction == 0 ? Vector3.up : (direction == 1 ? Vector3.forward : Vector3.right);
            Vector3 sideB = Vector3.Cross(axis, sideA);

            float halfHeight = height * 0.5f;
            float capOffset = Mathf.Max(0f, halfHeight - radius);

            Vector3 topCapCenter = center + axis * capOffset;
            Vector3 botCapCenter = center - axis * capOffset;

            Color wireColor = color;
            wireColor.a = ColliderStudioSettings.WireframeAlpha;

            Color fillColor = color;
            fillColor.a = ColliderStudioSettings.FillAlpha;

            // Geometry in Local Matrix
            Handles.matrix = targetTransform.localToWorldMatrix;
            Handles.color = wireColor;

            // Cap discs
            Handles.DrawWireDisc(topCapCenter, axis, radius);
            Handles.DrawWireDisc(botCapCenter, axis, radius);

            // Connect lines
            Handles.DrawLine(topCapCenter + sideA * radius, botCapCenter + sideA * radius);
            Handles.DrawLine(topCapCenter - sideA * radius, botCapCenter - sideA * radius);
            Handles.DrawLine(topCapCenter + sideB * radius, botCapCenter + sideB * radius);
            Handles.DrawLine(topCapCenter - sideB * radius, botCapCenter - sideB * radius);

            // Hemisphere arcs
            Handles.DrawWireArc(topCapCenter, sideB, sideA, 180f, radius);
            Handles.DrawWireArc(topCapCenter, sideA, -sideB, 180f, radius);
            Handles.DrawWireArc(botCapCenter, sideB, -sideA, 180f, radius);
            Handles.DrawWireArc(botCapCenter, sideA, sideB, 180f, radius);

            if (ColliderStudioSettings.FillFaces && fillColor.a > 0.001f)
            {
                Handles.color = fillColor;
                Handles.DrawSolidDisc(topCapCenter, axis, radius);
                Handles.DrawSolidDisc(botCapCenter, axis, radius);
            }

            Handles.matrix = Matrix4x4.identity;

            var mode = ColliderStudioSettings.InteractionMode;
            float maxScale = Mathf.Max(Mathf.Abs(targetTransform.lossyScale.x), Mathf.Max(Mathf.Abs(targetTransform.lossyScale.y), Mathf.Abs(targetTransform.lossyScale.z)));
            float worldHeight = height * maxScale;
            float maxCapSize = Mathf.Max(0.001f, radius * maxScale * 0.2f);

            // Center Handle
            if (mode == HandleInteractionMode.All || mode == HandleInteractionMode.CenterOnly)
            {
                DrawCenterHandle(center, targetTransform, newCenter =>
                {
                    Undo.RecordObject(capsule, "Move CapsuleCollider Center");
                    capsule.center = SnapVector(newCenter);
                });
            }

            Vector3 worldAxis = targetTransform.TransformDirection(axis).normalized;
            Vector3 worldSideA = targetTransform.TransformDirection(sideA).normalized;

            // Height handles on top and bottom caps
            if (mode == HandleInteractionMode.All || mode == HandleInteractionMode.FacesOnly)
            {
                Handles.color = color;
                Vector3 topPos = center + axis * halfHeight;
                Vector3 botPos = center - axis * halfHeight;
                Vector3 worldTop = targetTransform.TransformPoint(topPos);
                Vector3 worldBot = targetTransform.TransformPoint(botPos);

                float screenHandleSize = HandleUtility.GetHandleSize(worldTop) * 0.025f * ColliderStudioSettings.HandleScale;
                float handleSize = Mathf.Min(screenHandleSize, maxCapSize);

                // Top
                EditorGUI.BeginChangeCheck();
                Vector3 newTopWorld = Handles.Slider(worldTop, worldAxis, handleSize, Handles.DotHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(capsule, "Scale CapsuleCollider Height");
                    Vector3 newTopLocal = targetTransform.InverseTransformPoint(newTopWorld);
                    float newHalf = Mathf.Max(radius, Vector3.Dot(newTopLocal - center, axis));
                    capsule.height = SnapFloat(newHalf * 2f);
                }

                // Bottom
                EditorGUI.BeginChangeCheck();
                Vector3 newBotWorld = Handles.Slider(worldBot, -worldAxis, handleSize, Handles.DotHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(capsule, "Scale CapsuleCollider Height");
                    Vector3 newBotLocal = targetTransform.InverseTransformPoint(newBotWorld);
                    float newHalf = Mathf.Max(radius, Vector3.Dot(center - newBotLocal, axis));
                    capsule.height = SnapFloat(newHalf * 2f);
                }
            }

            // Radius handle
            if (mode == HandleInteractionMode.All || mode == HandleInteractionMode.CornersOnly)
            {
                Handles.color = color;
                Vector3 radialPos = center + sideA * radius;
                Vector3 worldRadial = targetTransform.TransformPoint(radialPos);

                float screenHandleSize = HandleUtility.GetHandleSize(worldRadial) * 0.025f * ColliderStudioSettings.HandleScale;
                float handleSize = Mathf.Min(screenHandleSize, maxCapSize);

                EditorGUI.BeginChangeCheck();
                Vector3 newRadialWorld = Handles.Slider(worldRadial, worldSideA, handleSize, Handles.DotHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(capsule, "Scale CapsuleCollider Radius");
                    Vector3 newRadialLocal = targetTransform.InverseTransformPoint(newRadialWorld);
                    float newR = Mathf.Max(0.01f, Vector3.Distance(newRadialLocal, center));
                    capsule.radius = SnapFloat(newR);
                }
            }
        }

        #endregion

        #region MeshCollider Gizmos

        private static void DrawMeshCollider(MeshCollider meshCol, Transform targetTransform, Color color)
        {
            if (meshCol.sharedMesh == null) return;

            Color wireColor = color;
            wireColor.a = ColliderStudioSettings.WireframeAlpha;

            Handles.matrix = targetTransform.localToWorldMatrix;
            Handles.color = wireColor;
            Handles.DrawWireCube(meshCol.sharedMesh.bounds.center, meshCol.sharedMesh.bounds.size);
            Handles.matrix = Matrix4x4.identity;
        }

        #endregion

        #region Helper Functions

        private static void DrawCenterHandle(Vector3 localCenter, Transform targetTransform, Action<Vector3> onMove)
        {
            Vector3 worldCenter = targetTransform.TransformPoint(localCenter);
            Quaternion worldRot = targetTransform.rotation;

            // Prominent center anchor dot for visibility and free-dragging
            float dotSize = HandleUtility.GetHandleSize(worldCenter) * 0.06f * ColliderStudioSettings.HandleScale;
            Handles.color = Color.white;
            int centerDotId = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUI.BeginChangeCheck();
            Vector3 freePos = Handles.FreeMoveHandle(centerDotId, worldCenter, dotSize, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 freeLocal = targetTransform.InverseTransformPoint(freePos);
                onMove?.Invoke(freeLocal);
                return;
            }

            // Full-size 3-axis PositionHandle in world space (standard large Unity translation arrows and plane sliders)
            EditorGUI.BeginChangeCheck();
            Vector3 newWorldCenter = Handles.PositionHandle(worldCenter, worldRot);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 newLocal = targetTransform.InverseTransformPoint(newWorldCenter);
                onMove?.Invoke(newLocal);
            }
        }

        private static Vector3 SnapVector(Vector3 v)
        {
            if (!ColliderStudioSettings.SnapEnabled && !Event.current.control) return v;
            float snap = ColliderStudioSettings.SnapValue;
            if (snap <= 0.0001f) return v;

            return new Vector3(
                Mathf.Round(v.x / snap) * snap,
                Mathf.Round(v.y / snap) * snap,
                Mathf.Round(v.z / snap) * snap
            );
        }

        private static float SnapFloat(float f)
        {
            if (!ColliderStudioSettings.SnapEnabled && !Event.current.control) return f;
            float snap = ColliderStudioSettings.SnapValue;
            if (snap <= 0.0001f) return f;
            return Mathf.Round(f / snap) * snap;
        }

        #endregion
    }
}
