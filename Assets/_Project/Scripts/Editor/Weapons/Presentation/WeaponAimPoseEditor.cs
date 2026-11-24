using Project.Combat.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Weapons.Editor
{
    [CustomEditor(typeof(WeaponAimPose))]
    public sealed class WeaponAimPoseEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            var status = new HelpBox("Calibration changes the stored ADS pose only; the model stays in its current pose.", HelpBoxMessageType.Info);
            var select = new Button(() => {
                var marker = serializedObject.FindProperty("_aimReference").objectReferenceValue as Transform;
                if (marker) { Selection.activeGameObject = marker.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
            }) { text = "Select AimReference" };
            root.Add(select);
            var calculate = new Button(() => {
                serializedObject.ApplyModifiedProperties();
                if (!TryCalculate((WeaponAimPose)target, out var position, out var rotation, out var error))
                { status.text = error; status.messageType = HelpBoxMessageType.Error; return; }
                Undo.RecordObject(target, "Calculate ADS from AimReference");
                serializedObject.Update();
                serializedObject.FindProperty("_aimLocalPosition").vector3Value = position;
                serializedObject.FindProperty("_aimLocalEulerAngles").vector3Value = rotation.eulerAngles;
                serializedObject.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                status.text = "ADS pose calculated. Save the prefab, then test aiming in Play Mode.";
                status.messageType = HelpBoxMessageType.Info;
            }) { text = "Calculate ADS from AimReference" };
            calculate.SetEnabled(!Application.isPlaying);
            root.Add(calculate);
            root.Add(status);
            return root;
        }

        public static bool TryCalculate(WeaponAimPose source, out Vector3 position, out Quaternion rotation, out string error)
        {
            position = default; rotation = Quaternion.identity; error = null;
            if (Application.isPlaying) { error = "Exit Play Mode before calibration."; return false; }
            var data = new SerializedObject(source);
            var motion = data.FindProperty("_motionRoot").objectReferenceValue as Transform;
            var marker = data.FindProperty("_aimReference").objectReferenceValue as Transform;
            var clip = data.FindProperty("_referencePose").objectReferenceValue as AnimationClip;
            var distance = data.FindProperty("_sightDistance").floatValue;
            var time = data.FindProperty("_referenceTime").floatValue;
            if (!motion || !marker || marker == motion || !marker.IsChildOf(motion) || !motion.IsChildOf(source.transform))
            { error = "Assign a Motion Root beneath this component and an AimReference beneath that root."; return false; }
            if (!clip || !float.IsFinite(time) || time < 0 || time > clip.length || !float.IsFinite(distance) || distance <= 0)
            { error = "Assign a reference clip, a sample time within its duration, and a positive sight distance."; return false; }
            for (var t = marker; t; t = t.parent)
            {
                var scale = t.localScale;
                if (scale.x <= 0 || Mathf.Abs(scale.x - scale.y) > 0.0001f || Mathf.Abs(scale.x - scale.z) > 0.0001f)
                { error = "Calibration requires positive uniform scale along the marker hierarchy."; return false; }
            }
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject copy = null;
            try
            {
                copy = Instantiate(source.gameObject);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, scene);
                var copyMotion = copy.transform.Find(AnimationUtility.CalculateTransformPath(motion, source.transform));
                var copyMarker = copy.transform.Find(AnimationUtility.CalculateTransformPath(marker, source.transform));
                var animator = copyMarker.GetComponentInParent<Animator>();
                if (!animator) { error = "AimReference must be inside the authored model's Animator hierarchy."; return false; }
                animator.enabled = false;
                clip.SampleAnimation(animator.gameObject, time);
                var offset = copyMotion.InverseTransformPoint(copyMarker.position);
                var relativeRotation = Quaternion.Inverse(copyMotion.rotation) * copyMarker.rotation;
                rotation = Quaternion.Inverse(relativeRotation);
                position = new Vector3(0, 0, distance) - rotation * Vector3.Scale(copyMotion.localScale, offset);
                return true;
            }
            finally
            {
                if (copy) DestroyImmediate(copy);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [DrawGizmo(GizmoType.Active)]
        private static void DrawReference(WeaponAimPose source, GizmoType type)
        {
            var marker = new SerializedObject(source).FindProperty("_aimReference").objectReferenceValue as Transform;
            if (!marker) return;
            var color = Gizmos.color;
            Gizmos.color = Color.blue; Gizmos.DrawRay(marker.position, marker.forward * 0.15f);
            Gizmos.color = Color.green; Gizmos.DrawRay(marker.position, marker.up * 0.05f);
            Gizmos.color = Color.red; Gizmos.DrawRay(marker.position, marker.right * 0.05f);
            Gizmos.color = color;
        }
    }
}
