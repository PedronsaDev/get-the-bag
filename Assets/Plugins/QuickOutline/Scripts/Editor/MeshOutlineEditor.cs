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

            // --- Header Banner ---
            var headerBox = new VisualElement();
            headerBox.style.backgroundColor = new Color(0.15f, 0.2f, 0.25f, 0.8f);
            headerBox.style.borderTopLeftRadius = 6;
            headerBox.style.borderTopRightRadius = 6;
            headerBox.style.borderBottomLeftRadius = 6;
            headerBox.style.borderBottomRightRadius = 6;
            headerBox.style.paddingTop = 8;
            headerBox.style.paddingBottom = 8;
            headerBox.style.paddingLeft = 10;
            headerBox.style.paddingRight = 10;
            headerBox.style.marginBottom = 8;

            var titleLabel = new Label("Linework Mesh Outline");
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.4f, 0.85f, 1f);

            var subtitleLabel = new Label("Scale-Invariant Inverted Hull & Silhouette Fill (URP 17)");
            subtitleLabel.style.fontSize = 10;
            subtitleLabel.style.color = new Color(0.7f, 0.7f, 0.7f);

            headerBox.Add(titleLabel);
            headerBox.Add(subtitleLabel);
            root.Add(headerBox);

            // --- Hierarchy Status & Rescan Button ---
            var statusBar = new VisualElement();
            statusBar.style.flexDirection = FlexDirection.Row;
            statusBar.style.alignItems = Align.Center;
            statusBar.style.marginBottom = 8;

            int childCount = outline ? outline.GetComponentsInChildren<Renderer>(true).Length : 0;
            var statusLabel = new Label($"Renderers detected: {childCount}");
            statusLabel.style.flexGrow = 1;
            statusLabel.style.fontSize = 11;
            statusLabel.style.color = childCount > 0 ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.9f, 0.5f, 0.4f);

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

            statusBar.Add(statusLabel);
            statusBar.Add(rescanBtn);
            root.Add(statusBar);

            // --- Presets Section ---
            var presetsBox = new VisualElement();
            presetsBox.style.borderTopWidth = 1;
            presetsBox.style.borderBottomWidth = 1;
            presetsBox.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            presetsBox.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            presetsBox.style.paddingTop = 6;
            presetsBox.style.paddingBottom = 6;
            presetsBox.style.marginBottom = 8;

            var presetsTitle = new Label("Quick Presets");
            presetsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            presetsTitle.style.marginBottom = 4;
            presetsBox.Add(presetsTitle);

            var presetBtnRow = new VisualElement();
            presetBtnRow.style.flexDirection = FlexDirection.Row;
            presetBtnRow.style.flexWrap = Wrap.Wrap;

            var lootBtn = new Button(() => ApplyPresetToTargets(mo => mo.ApplyPresetLootPickup())) { text = "Loot Pickup" };
            lootBtn.style.flexGrow = 1;
            lootBtn.style.height = 24;

            var xrayBtn = new Button(() => ApplyPresetToTargets(mo => mo.ApplyPresetTacticalXRay())) { text = "Tactical X-Ray" };
            xrayBtn.style.flexGrow = 1;
            xrayBtn.style.height = 24;

            var holoBtn = new Button(() => ApplyPresetToTargets(mo => mo.ApplyPresetHologram())) { text = "Hologram" };
            holoBtn.style.flexGrow = 1;
            holoBtn.style.height = 24;

            var silBtn = new Button(() => ApplyPresetToTargets(mo => mo.ApplyPresetSolidSilhouette())) { text = "Silhouette Only" };
            silBtn.style.flexGrow = 1;
            silBtn.style.height = 24;

            presetBtnRow.Add(lootBtn);
            presetBtnRow.Add(xrayBtn);
            presetBtnRow.Add(holoBtn);
            presetBtnRow.Add(silBtn);
            presetsBox.Add(presetBtnRow);
            root.Add(presetsBox);

            // --- Outline Section ---
            var outlineFoldout = new Foldout { text = "Outline Settings", value = true };
            outlineFoldout.style.marginBottom = 6;

            var outlineModeProp = serializedObject.FindProperty("outlineMode");
            var outlineColorProp = serializedObject.FindProperty("outlineColor");
            var outlineWidthProp = serializedObject.FindProperty("outlineWidth");
            var widthModeProp = serializedObject.FindProperty("widthMode");
            var stencilRefProp = serializedObject.FindProperty("stencilReference");

            outlineFoldout.Add(new PropertyField(outlineModeProp));
            outlineFoldout.Add(new PropertyField(outlineColorProp));
            outlineFoldout.Add(new PropertyField(outlineWidthProp));
            outlineFoldout.Add(new PropertyField(widthModeProp));
            outlineFoldout.Add(new PropertyField(stencilRefProp));
            root.Add(outlineFoldout);

            // --- Surface Fill Section ---
            var fillFoldout = new Foldout { text = "Surface Fill & Scrolling Patterns", value = true };
            fillFoldout.style.marginBottom = 6;

            var enableFillProp = serializedObject.FindProperty("enableSurfaceFill");
            var fillColorProp = serializedObject.FindProperty("fillColor");
            var fillSecColorProp = serializedObject.FindProperty("fillSecondaryColor");
            var fillPatternProp = serializedObject.FindProperty("fillPattern");
            var fillTexProp = serializedObject.FindProperty("fillTexture");
            var fillTilingProp = serializedObject.FindProperty("fillTiling");
            var fillAngleProp = serializedObject.FindProperty("fillAngle");
            var fillSpeedProp = serializedObject.FindProperty("fillScrollSpeed");
            var fillSpaceProp = serializedObject.FindProperty("fillSpace");
            var fillOcclusionProp = serializedObject.FindProperty("fillOcclusion");

            fillFoldout.Add(new PropertyField(enableFillProp));
            fillFoldout.Add(new PropertyField(fillColorProp));
            fillFoldout.Add(new PropertyField(fillSecColorProp));
            fillFoldout.Add(new PropertyField(fillPatternProp));
            fillFoldout.Add(new PropertyField(fillTexProp));
            fillFoldout.Add(new PropertyField(fillTilingProp));
            fillFoldout.Add(new PropertyField(fillAngleProp));
            fillFoldout.Add(new PropertyField(fillSpeedProp));
            fillFoldout.Add(new PropertyField(fillSpaceProp));
            fillFoldout.Add(new PropertyField(fillOcclusionProp));
            root.Add(fillFoldout);

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
