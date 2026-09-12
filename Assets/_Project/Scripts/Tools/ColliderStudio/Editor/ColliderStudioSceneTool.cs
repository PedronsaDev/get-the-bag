using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Project.Tools.ColliderStudio.Editor
{
    [EditorTool("Collider Studio", typeof(Collider))]
    public class ColliderStudioEditorTool : EditorTool
    {
        private GUIContent _icon;

        public override GUIContent toolbarIcon
        {
            get
            {
                if (_icon == null || _icon.image == null)
                {
                    _icon = new GUIContent(
                        EditorGUIUtility.IconContent("BoxCollider Icon").image,
                        "Collider Studio: Grab corners/faces to scale, move center, and inspect with custom volume gizmos."
                    );
                }
                return _icon;
            }
        }

        public override void OnToolGUI(EditorWindow window)
        {
            // Drawing is handled centrally in ColliderStudioSceneListener.OnDuringSceneGUI
            // to support multi-collider GameObjects and multi-selection seamlessly.
        }
    }

    [InitializeOnLoad]
    public static class ColliderStudioSceneListener
    {
        static ColliderStudioSceneListener()
        {
            SceneView.duringSceneGui -= OnDuringSceneGUI;
            SceneView.duringSceneGui += OnDuringSceneGUI;
        }

        private static void OnDuringSceneGUI(SceneView sceneView)
        {
            if (!ColliderStudioSettings.ToolEnabled) return;

            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0) return;

            for (int i = 0; i < selected.Length; i++)
            {
                var go = selected[i];
                if (go == null) continue;

                var colliders = go.GetComponents<Collider>();
                for (int c = 0; c < colliders.Length; c++)
                {
                    var col = colliders[c];
                    if (col != null && col.enabled)
                    {
                        ColliderStudioHandles.DrawColliderHandles(col);
                    }
                }
            }
        }
    }
}
