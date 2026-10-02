using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Plugins.Outline.Editor
{
    [CustomEditor(typeof(MeshOutline))]
    [CanEditMultipleObjects]
    public class MeshOutlineEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.style.paddingTop = 6;
            root.style.paddingBottom = 6;
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;

            var outline = target as MeshOutline;

            // --- Hierarchy Status & Rescan Button ---
            var statusBar = new VisualElement();
            statusBar.style.flexDirection = FlexDirection.Row;
            statusBar.style.alignItems = Align.Center;
            statusBar.style.marginBottom = 8;
            

            var rescanBtn = new Button(() =>
            {
                foreach (var t in targets)
                {
                    if (t is MeshOutline mo)
                    {
                        mo.RefreshRenderers();
                        mo.UpdateMaterialProperties();
                        EditorUtility.SetDirty(mo);
                    }
                }
            }) { text = "Rescan Hierarchy" };
            rescanBtn.style.height = 22;

            statusBar.Add(rescanBtn);
            root.Add(statusBar);

            // --- Interactive Runtime Triggers ---
            var triggersBox = new VisualElement();
            triggersBox.style.flexDirection = FlexDirection.Row;
            triggersBox.style.marginBottom = 8;

            // --- Outline Section ---
            var outlineFoldout = new Foldout { text = "Outline Appearance", value = true };
            outlineFoldout.style.marginBottom = 6;
            outlineFoldout.Add(new PropertyField(serializedObject.FindProperty("outlineMode")));
            outlineFoldout.Add(new PropertyField(serializedObject.FindProperty("outlineColor")));
            outlineFoldout.Add(new PropertyField(serializedObject.FindProperty("outlineWidth")));
            outlineFoldout.Add(new PropertyField(serializedObject.FindProperty("widthMode")));
            outlineFoldout.Add(new PropertyField(serializedObject.FindProperty("stencilReference")));
            root.Add(outlineFoldout);

            // --- Surface Fill Section ---
            var fillFoldout = new Foldout { text = "Surface Fill & Scrolling Patterns", value = true };
            fillFoldout.style.marginBottom = 6;
            fillFoldout.Add(new PropertyField(serializedObject.FindProperty("enableSurfaceFill")));
            fillFoldout.Add(new PropertyField(serializedObject.FindProperty("fillColor")));
            fillFoldout.Add(new PropertyField(serializedObject.FindProperty("fillSecondaryColor")));
            fillFoldout.Add(new PropertyField(serializedObject.FindProperty("fillPattern")));
            fillFoldout.Add(new PropertyField(serializedObject.FindProperty("fresnelPower")));
            fillFoldout.Add(new PropertyField(serializedObject.FindProperty("fillTexture")));
            fillFoldout.Add(new PropertyField(serializedObject.FindProperty("fillTiling")));
            fillFoldout.Add(new PropertyField(serializedObject.FindProperty("fillAngle")));
            fillFoldout.Add(new PropertyField(serializedObject.FindProperty("fillScrollSpeed")));
            fillFoldout.Add(new PropertyField(serializedObject.FindProperty("fillSpace")));
            fillFoldout.Add(new PropertyField(serializedObject.FindProperty("fillOcclusion")));
            root.Add(fillFoldout);

            // --- Animation & Pulse Section ---
            var animFoldout = new Foldout { text = "Animation & Pulse", value = false };
            animFoldout.style.marginBottom = 6;
            animFoldout.Add(new PropertyField(serializedObject.FindProperty("pulseEnabled")));
            animFoldout.Add(new PropertyField(serializedObject.FindProperty("pulseSpeed")));
            animFoldout.Add(new PropertyField(serializedObject.FindProperty("pulseAmplitude")));
            root.Add(animFoldout);

            // --- Stylized Jitter Wobble Section ---
            var jitterFoldout = new Foldout { text = "Stylized Wobble (Spider-Verse Jitter)", value = false };
            jitterFoldout.style.marginBottom = 6;
            jitterFoldout.Add(new PropertyField(serializedObject.FindProperty("jitterEnabled")));
            jitterFoldout.Add(new PropertyField(serializedObject.FindProperty("jitterSpeed")));
            jitterFoldout.Add(new PropertyField(serializedObject.FindProperty("jitterFrequency")));
            jitterFoldout.Add(new PropertyField(serializedObject.FindProperty("jitterScale")));
            jitterFoldout.Add(new PropertyField(serializedObject.FindProperty("jitterFramerate")));
            root.Add(jitterFoldout);

            // --- Distance Fading Section ---
            var distFoldout = new Foldout { text = "Distance Fading", value = false };
            distFoldout.style.marginBottom = 6;
            distFoldout.Add(new PropertyField(serializedObject.FindProperty("distanceFadeEnabled")));
            distFoldout.Add(new PropertyField(serializedObject.FindProperty("fadeMinDistance")));
            distFoldout.Add(new PropertyField(serializedObject.FindProperty("fadeMaxDistance")));
            root.Add(distFoldout);

            // --- Hierarchy Filtering Section ---
            var filterFoldout = new Foldout { text = "Hierarchy & Filtering", value = false };
            filterFoldout.style.marginBottom = 6;
            filterFoldout.Add(new PropertyField(serializedObject.FindProperty("includeLayers")));
            filterFoldout.Add(new PropertyField(serializedObject.FindProperty("excludedKeywords")));
            filterFoldout.Add(new PropertyField(serializedObject.FindProperty("ignoredRenderers")));
            root.Add(filterFoldout);

            // --- Live Update Callbacks ---
            root.TrackSerializedObjectValue(serializedObject, so =>
            {
                foreach (var t in targets)
                {
                    if (t is MeshOutline mo)
                    {
                        mo.UpdateMaterialProperties();
                    }
                }
            });

            root.Bind(serializedObject);
            return root;
        }

        private void ApplyPresetToTargets(System.Action<MeshOutline> action)
        {
            Undo.RecordObjects(targets, "Apply Outline Preset");
            foreach (var t in targets)
            {
                if (t is MeshOutline mo)
                {
                    action(mo);
                    EditorUtility.SetDirty(mo);
                }
            }
            serializedObject.Update();
        }
    }
}
