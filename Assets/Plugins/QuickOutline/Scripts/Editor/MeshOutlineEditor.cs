using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Plugins.Outline.Editor
{
    [CustomEditor(typeof(MeshOutline)), CanEditMultipleObjects]
    public sealed class MeshOutlineEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox("Rendering previews run in Play Mode. Source mesh data and material slots remain unchanged in Edit Mode. Outlines share stencil bit 0 and merge where their masks overlap.", HelpBoxMessageType.Info));
            var diagnostics = new VisualElement();
            root.Add(diagnostics);
            Action refreshDiagnostics = () =>
            {
                diagnostics.Clear();
                foreach (var item in targets)
                {
                    if (!(item is MeshOutline outline)) continue;
                    int accepted = 0;
                    foreach (var renderer in outline.GetComponentsInChildren<Renderer>(true)) if (outline.IncludesRenderer(renderer)) accepted++;
                    diagnostics.Add(new Label(outline.name + ": " + accepted + " accepted renderers, " + outline.BoundRendererCount + " runtime bindings"));
                    foreach (var issue in OutlineValidator.Validate(outline))
                        diagnostics.Add(new HelpBox(issue.Message, issue.Error ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning));
                    if (!string.IsNullOrEmpty(outline.LastPreparationError)) diagnostics.Add(new HelpBox(outline.LastPreparationError, HelpBoxMessageType.Error));
                }
            };
            root.Add(new Button(() => { foreach (var item in targets) if (item is MeshOutline outline) outline.RequestRendererRefresh(); refreshDiagnostics(); }) { text = "Rescan / Validate" });
            var bake = new Button(() => ApplyToTargets(OutlineMeshBaker.Bake, "Bake Outline Meshes")) { text = "Bake Readable Outline Data" };
            bake.SetEnabled(!Application.isPlaying);
            root.Add(bake);

            AddGroup(root, "Outline Appearance", true, "outlineMode", "outlineColor", "outlineWidth", "widthMode");
            AddGroup(root, "Surface Fill", false, "enableSurfaceFill", "fillColor", "fillSecondaryColor", "fillPattern", "fillTexture", "fillTiling", "fillAngle", "fillScrollSpeed", "fresnelPower", "fillSpace", "fillOcclusion", "twoSidedSurface", "patternLineWidth");
            AddGroup(root, "Animation and Distance", false, "pulseEnabled", "pulseSpeed", "pulseAmplitude", "distanceFadeEnabled", "fadeMinDistance", "fadeMaxDistance", "jitterEnabled", "jitterSpeed", "jitterFrequency", "jitterScale", "jitterFramerate", "animationPhase", "unscaledEffects");
            AddGroup(root, "Ownership and Filtering", false, "includeLayers", "excludedKeywords", "ignoredRenderers", "preparedMeshes");
            AddGroup(root, "Cameras and Bounds", false, "showInSceneView", "showInReflections", "screenSpaceBoundsPadding");

            var styles = new Foldout { text = "Styles and Presets", value = false };
            styles.Add(new PropertyField(serializedObject.FindProperty("style")));
            styles.Add(new Button(() => ApplyToTargets(o => o.ApplyAssignedStyle(), "Apply Outline Style")) { text = "Apply Assigned Style" });
            styles.Add(new Button(() =>
            {
                string path = EditorUtility.SaveFilePanelInProject("Save Outline Style", "OutlineStyle", "asset", "Choose a style asset location.");
                if (string.IsNullOrEmpty(path)) return;
                var style = ScriptableObject.CreateInstance<MeshOutlineStyle>();
                ((MeshOutline)target).CaptureStyle(style);
                AssetDatabase.CreateAsset(style, path);
                AssetDatabase.SaveAssetIfDirty(style);
            }) { text = "Save Current Appearance as Style" });
            var names = new List<string> { "Loot Pickup", "Tactical X-Ray", "Hologram", "Solid Silhouette", "Spider-Verse", "Holo Shield" };
            var preset = new DropdownField("Preset", names, 0);
            styles.Add(preset);
            styles.Add(new Button(() => ApplyToTargets(o => ApplyPreset(o, preset.index), "Apply Outline Preset")) { text = "Apply Preset" });
            root.Add(styles);

            var triggers = new VisualElement();
            triggers.style.flexDirection = FlexDirection.Row;
            triggers.Add(new Button(() => { foreach (var item in targets) if (item is MeshOutline o) o.Ping(); }) { text = "Ping" });
            triggers.Add(new Button(() => { foreach (var item in targets) if (item is MeshOutline o) o.Flash(Color.white); }) { text = "Flash" });
            triggers.Add(new Button(() => { foreach (var item in targets) if (item is MeshOutline o) o.StopEffects(); }) { text = "Stop Effects" });
            triggers.SetEnabled(Application.isPlaying);
            root.Add(triggers);
            root.TrackSerializedObjectValue(serializedObject, _ => refreshDiagnostics());
            root.Bind(serializedObject);
            refreshDiagnostics();
            return root;
        }

        private void AddGroup(VisualElement root, string title, bool expanded, params string[] fields)
        {
            var foldout = new Foldout { text = title, value = expanded };
            foreach (var name in fields) foldout.Add(new PropertyField(serializedObject.FindProperty(name)));
            root.Add(foldout);
        }

        private void ApplyToTargets(Action<MeshOutline> apply, string undoName)
        {
            Undo.RecordObjects(targets, undoName);
            foreach (var item in targets)
            {
                if (!(item is MeshOutline outline)) continue;
                apply(outline);
                EditorUtility.SetDirty(outline);
                if (PrefabUtility.IsPartOfPrefabInstance(outline)) PrefabUtility.RecordPrefabInstancePropertyModifications(outline);
            }
            serializedObject.Update();
        }

        private static void ApplyPreset(MeshOutline outline, int index)
        {
            switch (index)
            {
                case 0: outline.ApplyPresetLootPickup(); break;
                case 1: outline.ApplyPresetTacticalXRay(); break;
                case 2: outline.ApplyPresetHologram(); break;
                case 3: outline.ApplyPresetSolidSilhouette(); break;
                case 4: outline.ApplyPresetSpiderVerse(); break;
                case 5: outline.ApplyPresetHoloShield(); break;
            }
        }
    }

    [CustomEditor(typeof(MeshOutlineStyle))]
    public sealed class MeshOutlineStyleEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }
    }
}
