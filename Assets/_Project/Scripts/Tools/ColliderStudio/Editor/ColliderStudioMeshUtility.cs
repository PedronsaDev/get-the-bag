using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Project.Tools.ColliderStudio.Editor
{
    public static class ColliderStudioMeshUtility
    {
        /// <summary>
        /// Calculates the composite bounding box of all MeshFilters and SkinnedMeshRenderers
        /// in the target GameObject or its children, transformed into root collider local space.
        /// </summary>
        public static bool TryCalculateMeshBounds(GameObject root, out Bounds localBounds)
        {
            localBounds = default;
            if (root == null) return false;

            Transform rootTransform = root.transform;
            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            var skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            bool hasBounds = false;
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            void EncapsulateWorldPoints(Transform sourceTransform, Bounds bounds)
            {
                Vector3 c = bounds.center;
                Vector3 e = bounds.extents;

                Vector3[] corners = new Vector3[8]
                {
                    c + new Vector3(-e.x, -e.y, -e.z),
                    c + new Vector3( e.x, -e.y, -e.z),
                    c + new Vector3( e.x, -e.y,  e.z),
                    c + new Vector3(-e.x, -e.y,  e.z),
                    c + new Vector3(-e.x,  e.y, -e.z),
                    c + new Vector3( e.x,  e.y, -e.z),
                    c + new Vector3( e.x,  e.y,  e.z),
                    c + new Vector3(-e.x,  e.y,  e.z),
                };

                for (int i = 0; i < 8; i++)
                {
                    Vector3 worldPt = sourceTransform.TransformPoint(corners[i]);
                    Vector3 localPt = rootTransform.InverseTransformPoint(worldPt);
                    min = Vector3.Min(min, localPt);
                    max = Vector3.Max(max, localPt);
                }
                hasBounds = true;
            }

            for (int i = 0; i < meshFilters.Length; i++)
            {
                var mf = meshFilters[i];
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 0)
                {
                    EncapsulateWorldPoints(mf.transform, mf.sharedMesh.bounds);
                }
            }

            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                var smr = skinnedRenderers[i];
                if (smr != null && smr.sharedMesh != null && smr.sharedMesh.vertexCount > 0)
                {
                    EncapsulateWorldPoints(smr.transform, smr.sharedMesh.bounds);
                }
            }

            if (!hasBounds) return false;

            Vector3 size = max - min;
            Vector3 center = (min + max) * 0.5f;
            localBounds = new Bounds(center, size);
            return true;
        }

        /// <summary>
        /// Snaps collider center and size/radius to snugly fit the underlying mesh bounds.
        /// </summary>
        public static bool AutoFitCollider(Collider col)
        {
            if (col == null) return false;
            if (!TryCalculateMeshBounds(col.gameObject, out Bounds b)) return false;

            Undo.RecordObject(col, "Auto-Fit Collider to Mesh");

            switch (col)
            {
                case BoxCollider box:
                    box.center = b.center;
                    box.size = Vector3.Max(b.size, new Vector3(0.01f, 0.01f, 0.01f));
                    break;

                case SphereCollider sphere:
                    sphere.center = b.center;
                    sphere.radius = Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y, b.size.z) * 0.5f);
                    break;

                case CapsuleCollider capsule:
                    capsule.center = b.center;
                    int dir = capsule.direction;
                    if (dir == 0) // X
                    {
                        capsule.height = Mathf.Max(0.01f, b.size.x);
                        capsule.radius = Mathf.Max(0.005f, Mathf.Max(b.size.y, b.size.z) * 0.5f);
                    }
                    else if (dir == 1) // Y
                    {
                        capsule.height = Mathf.Max(0.01f, b.size.y);
                        capsule.radius = Mathf.Max(0.005f, Mathf.Max(b.size.x, b.size.z) * 0.5f);
                    }
                    else // Z
                    {
                        capsule.height = Mathf.Max(0.01f, b.size.z);
                        capsule.radius = Mathf.Max(0.005f, Mathf.Max(b.size.x, b.size.y) * 0.5f);
                    }
                    break;
            }

            EditorUtility.SetDirty(col);
            SceneView.RepaintAll();
            return true;
        }

        /// <summary>
        /// Re-centers the collider on the mesh without altering its size.
        /// </summary>
        public static bool CenterColliderOnMesh(Collider col)
        {
            if (col == null) return false;
            if (!TryCalculateMeshBounds(col.gameObject, out Bounds b)) return false;

            Undo.RecordObject(col, "Center Collider on Mesh");
            switch (col)
            {
                case BoxCollider box:
                    box.center = b.center;
                    break;
                case SphereCollider sphere:
                    sphere.center = b.center;
                    break;
                case CapsuleCollider capsule:
                    capsule.center = b.center;
                    break;
            }

            EditorUtility.SetDirty(col);
            SceneView.RepaintAll();
            return true;
        }

        /// <summary>
        /// Resets collider center to (0,0,0) and size/extents to standard defaults.
        /// </summary>
        public static void ResetCollider(Collider col)
        {
            if (col == null) return;
            Undo.RecordObject(col, "Reset Collider");

            switch (col)
            {
                case BoxCollider box:
                    box.center = Vector3.zero;
                    box.size = Vector3.one;
                    break;
                case SphereCollider sphere:
                    sphere.center = Vector3.zero;
                    sphere.radius = 0.5f;
                    break;
                case CapsuleCollider capsule:
                    capsule.center = Vector3.zero;
                    capsule.radius = 0.5f;
                    capsule.height = 2f;
                    break;
            }

            EditorUtility.SetDirty(col);
            SceneView.RepaintAll();
        }
    }
}
