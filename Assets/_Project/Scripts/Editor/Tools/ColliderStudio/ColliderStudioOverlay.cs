using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace Project.Tools.ColliderStudio.Editor
{
    [Overlay(typeof(SceneView), "proc-collider-studio", "Collider Studio", defaultDisplay = true)]
    public class ColliderStudioOverlay : Overlay
    {
        public override VisualElement CreatePanelContent()
        {
            return ColliderStudioPanelBuilder.CreatePanel();
        }
    }
}
