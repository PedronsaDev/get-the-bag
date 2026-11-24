using UnityEditor;
using UnityEngine;

namespace Project.Tools.ColliderStudio.Editor
{
    public class ColliderStudioWindow : EditorWindow
    {
        [MenuItem("Tools/Collider Studio/Open Window", priority = 50)]
        public static void Open()
        {
            var win = GetWindow<ColliderStudioWindow>("Collider Studio");
            win.minSize = new Vector2(300, 420);
            win.Show();
        }

        [MenuItem("Tools/Collider Studio/Toggle Active", priority = 51)]
        public static void ToggleActive()
        {
            ColliderStudioSettings.ToolEnabled = !ColliderStudioSettings.ToolEnabled;
        }

        private void CreateGUI()
        {
            rootVisualElement.Add(ColliderStudioPanelBuilder.CreatePanel());
        }
    }
}
