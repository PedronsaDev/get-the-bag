using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Tools.ColliderStudio.Editor
{
    public static class ColliderStudioPanelBuilder
    {
        public static VisualElement CreatePanel()
        {
            var root = new ScrollView(ScrollViewMode.Vertical);
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;
            root.style.minWidth = 280;

            // 1. Header Section
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;
            header.style.paddingBottom = 6;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.28f, 0.28f, 0.28f);

            var titleLabel = new Label("COLLIDER STUDIO");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 13;
            titleLabel.style.color = new Color(0f, 0.9f, 1f); // Cyan accent

            var toolToggle = new Toggle("Active")
            {
                value = ColliderStudioSettings.ToolEnabled,
                tooltip = "Enable or disable Collider Studio Scene View handles and volume gizmos."
            };
            toolToggle.RegisterValueChangedCallback(evt =>
            {
                ColliderStudioSettings.ToolEnabled = evt.newValue;
            });

            header.Add(titleLabel);
            header.Add(toolToggle);
            root.Add(header);

            // 2. Selection Info & Quick Add Box
            var selectionBox = new VisualElement();
            selectionBox.style.paddingLeft = 6;
            selectionBox.style.paddingRight = 6;
            selectionBox.style.paddingTop = 6;
            selectionBox.style.paddingBottom = 6;
            selectionBox.style.marginBottom = 10;
            selectionBox.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.9f);
            selectionBox.style.borderTopLeftRadius = 4;
            selectionBox.style.borderTopRightRadius = 4;
            selectionBox.style.borderBottomLeftRadius = 4;
            selectionBox.style.borderBottomRightRadius = 4;
            selectionBox.style.borderLeftWidth = 1;
            selectionBox.style.borderRightWidth = 1;
            selectionBox.style.borderTopWidth = 1;
            selectionBox.style.borderBottomWidth = 1;
            selectionBox.style.borderLeftColor = new Color(0.25f, 0.25f, 0.25f);
            selectionBox.style.borderRightColor = new Color(0.25f, 0.25f, 0.25f);
            selectionBox.style.borderTopColor = new Color(0.25f, 0.25f, 0.25f);
            selectionBox.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);

            var selectionLabel = new Label("Selection: None");
            selectionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            selectionLabel.style.fontSize = 11;
            selectionLabel.style.marginBottom = 4;
            selectionBox.Add(selectionLabel);

            var quickAddRow = new VisualElement();
            quickAddRow.style.flexDirection = FlexDirection.Row;
            quickAddRow.style.display = DisplayStyle.None;

            var addBoxBtn = new Button(() => AddColliderToSelected<BoxCollider>()) { text = "+ Box" };
            var addSphereBtn = new Button(() => AddColliderToSelected<SphereCollider>()) { text = "+ Sphere" };
            var addCapsuleBtn = new Button(() => AddColliderToSelected<CapsuleCollider>()) { text = "+ Capsule" };
            addBoxBtn.style.flexGrow = 1;
            addSphereBtn.style.flexGrow = 1;
            addCapsuleBtn.style.flexGrow = 1;
            quickAddRow.Add(addBoxBtn);
            quickAddRow.Add(addSphereBtn);
            quickAddRow.Add(addCapsuleBtn);
            selectionBox.Add(quickAddRow);

            root.Add(selectionBox);

            // 3. Handle Interaction Mode (Segmented Toolbar)
            var modeLabel = CreateSectionHeader("INTERACTION MODE");
            root.Add(modeLabel);

            var modeRow = new VisualElement();
            modeRow.style.flexDirection = FlexDirection.Row;
            modeRow.style.marginBottom = 8;

            Button btnAll = new Button { text = "All" };
            Button btnCorners = new Button { text = "Corners" };
            Button btnFaces = new Button { text = "Faces" };
            Button btnCenter = new Button { text = "Center" };

            btnAll.style.flexGrow = 1;
            btnCorners.style.flexGrow = 1;
            btnFaces.style.flexGrow = 1;
            btnCenter.style.flexGrow = 1;

            void UpdateModeButtons()
            {
                var current = ColliderStudioSettings.InteractionMode;
                Color activeBg = new Color(0.12f, 0.45f, 0.65f, 1f);
                Color inactiveBg = new Color(0.24f, 0.24f, 0.24f, 1f);

                btnAll.style.backgroundColor = current == HandleInteractionMode.All ? activeBg : inactiveBg;
                btnCorners.style.backgroundColor = current == HandleInteractionMode.CornersOnly ? activeBg : inactiveBg;
                btnFaces.style.backgroundColor = current == HandleInteractionMode.FacesOnly ? activeBg : inactiveBg;
                btnCenter.style.backgroundColor = current == HandleInteractionMode.CenterOnly ? activeBg : inactiveBg;
            }

            btnAll.clicked += () => { ColliderStudioSettings.InteractionMode = HandleInteractionMode.All; UpdateModeButtons(); };
            btnCorners.clicked += () => { ColliderStudioSettings.InteractionMode = HandleInteractionMode.CornersOnly; UpdateModeButtons(); };
            btnFaces.clicked += () => { ColliderStudioSettings.InteractionMode = HandleInteractionMode.FacesOnly; UpdateModeButtons(); };
            btnCenter.clicked += () => { ColliderStudioSettings.InteractionMode = HandleInteractionMode.CenterOnly; UpdateModeButtons(); };

            UpdateModeButtons();

            modeRow.Add(btnAll);
            modeRow.Add(btnCorners);
            modeRow.Add(btnFaces);
            modeRow.Add(btnCenter);
            root.Add(modeRow);

            var handleScaleSlider = new Slider("Handle Size", 0.1f, 2.5f)
            {
                value = ColliderStudioSettings.HandleScale,
                tooltip = "Adjust visual handle cap scale in Scene View."
            };
            handleScaleSlider.RegisterValueChangedCallback(e => ColliderStudioSettings.HandleScale = e.newValue);
            handleScaleSlider.style.marginBottom = 4;
            root.Add(handleScaleSlider);

            // 4. Symmetry Options
            var cornerSymToggle = new Toggle("Corner Symmetric (or hold Alt)")
            {
                value = ColliderStudioSettings.CornerSymmetric,
                tooltip = "When enabled, dragging a corner scales symmetrically from the center instead of anchoring the opposite corner."
            };
            cornerSymToggle.RegisterValueChangedCallback(e => ColliderStudioSettings.CornerSymmetric = e.newValue);
            root.Add(cornerSymToggle);

            var faceSymToggle = new Toggle("Face Symmetric (or hold Alt)")
            {
                value = ColliderStudioSettings.FaceSymmetric,
                tooltip = "When enabled, dragging a face scales symmetrically about the opposite face."
            };
            faceSymToggle.RegisterValueChangedCallback(e => ColliderStudioSettings.FaceSymmetric = e.newValue);
            faceSymToggle.style.marginBottom = 8;
            root.Add(faceSymToggle);

            // 5. Grid Snapping
            root.Add(CreateSectionHeader("SNAPPING"));

            var snapRow = new VisualElement();
            snapRow.style.flexDirection = FlexDirection.Row;
            snapRow.style.alignItems = Align.Center;
            snapRow.style.marginBottom = 4;

            var snapToggle = new Toggle("Snap")
            {
                value = ColliderStudioSettings.SnapEnabled,
                tooltip = "Toggle grid snapping for corner, face, and center manipulations (or hold Ctrl in Scene View)."
            };
            snapToggle.RegisterValueChangedCallback(e => ColliderStudioSettings.SnapEnabled = e.newValue);

            var snapField = new FloatField
            {
                value = ColliderStudioSettings.SnapValue,
                tooltip = "Grid snap increment in meters."
            };
            snapField.style.flexGrow = 1;
            snapField.style.marginLeft = 8;
            snapField.RegisterValueChangedCallback(e => ColliderStudioSettings.SnapValue = Mathf.Max(0.001f, e.newValue));

            snapRow.Add(snapToggle);
            snapRow.Add(snapField);
            root.Add(snapRow);

            // Snap Preset Quick Buttons
            var snapPresetsRow = new VisualElement();
            snapPresetsRow.style.flexDirection = FlexDirection.Row;
            snapPresetsRow.style.marginBottom = 8;

            float[] presets = { 0.05f, 0.1f, 0.25f, 0.5f, 1.0f };
            for (int i = 0; i < presets.Length; i++)
            {
                float val = presets[i];
                var pBtn = new Button(() =>
                {
                    ColliderStudioSettings.SnapEnabled = true;
                    ColliderStudioSettings.SnapValue = val;
                    snapToggle.value = true;
                    snapField.value = val;
                })
                { text = val.ToString("0.0#") + "m" };
                pBtn.style.flexGrow = 1;
                pBtn.style.fontSize = 9;
                snapPresetsRow.Add(pBtn);
            }
            root.Add(snapPresetsRow);

            // 6. Visuals & Colors
            root.Add(CreateSectionHeader("VISUALIZATION & GIZMOS"));

            var colorModeField = new EnumField("Color Mode", ColliderStudioSettings.ColorMode);
            colorModeField.RegisterValueChangedCallback(e => ColliderStudioSettings.ColorMode = (ColliderColorMode)e.newValue);
            root.Add(colorModeField);

            var solidColorField = new ColorField("Solid Color") { value = ColliderStudioSettings.SolidColor };
            solidColorField.RegisterValueChangedCallback(e => ColliderStudioSettings.SolidColor = e.newValue);
            root.Add(solidColorField);

            var triggerColorField = new ColorField("Trigger Color") { value = ColliderStudioSettings.TriggerColor };
            triggerColorField.RegisterValueChangedCallback(e => ColliderStudioSettings.TriggerColor = e.newValue);
            root.Add(triggerColorField);

            var customColorField = new ColorField("Custom Color") { value = ColliderStudioSettings.CustomColor };
            customColorField.RegisterValueChangedCallback(e => ColliderStudioSettings.CustomColor = e.newValue);
            root.Add(customColorField);

            // Quick Color Palette
            var paletteRow = new VisualElement();
            paletteRow.style.flexDirection = FlexDirection.Row;
            paletteRow.style.marginBottom = 8;
            paletteRow.style.marginTop = 2;

            Color[] palette =
            {
                new Color(0f, 0.9f, 1f),      // Cyan
                new Color(1f, 0.57f, 0f),     // Amber
                new Color(0.46f, 1f, 0.01f),  // Lime
                new Color(1f, 0.1f, 0.5f),    // Magenta
                new Color(1f, 0.95f, 0.2f),   // Yellow
                new Color(1f, 1f, 1f)         // White
            };

            for (int i = 0; i < palette.Length; i++)
            {
                Color c = palette[i];
                var swatch = new Button(() =>
                {
                    ColliderStudioSettings.ColorMode = ColliderColorMode.CustomColor;
                    ColliderStudioSettings.CustomColor = c;
                    customColorField.value = c;
                    colorModeField.value = ColliderColorMode.CustomColor;
                });
                swatch.style.flexGrow = 1;
                swatch.style.height = 14;
                swatch.style.backgroundColor = c;
                swatch.style.borderTopLeftRadius = 2;
                swatch.style.borderTopRightRadius = 2;
                swatch.style.borderBottomLeftRadius = 2;
                swatch.style.borderBottomRightRadius = 2;
                paletteRow.Add(swatch);
            }
            root.Add(paletteRow);

            var fillToggle = new Toggle("Fill Faces") { value = ColliderStudioSettings.FillFaces };
            fillToggle.RegisterValueChangedCallback(e => ColliderStudioSettings.FillFaces = e.newValue);
            root.Add(fillToggle);

            var fillAlphaSlider = new Slider("Fill Alpha", 0f, 1f) { value = ColliderStudioSettings.FillAlpha };
            fillAlphaSlider.RegisterValueChangedCallback(e => ColliderStudioSettings.FillAlpha = e.newValue);
            root.Add(fillAlphaSlider);

            var wireAlphaSlider = new Slider("Wireframe Alpha", 0f, 1f) { value = ColliderStudioSettings.WireframeAlpha };
            wireAlphaSlider.RegisterValueChangedCallback(e => ColliderStudioSettings.WireframeAlpha = e.newValue);
            root.Add(wireAlphaSlider);

            var xrayToggle = new Toggle("X-Ray Mode (See Through Walls)") { value = ColliderStudioSettings.XRay };
            xrayToggle.RegisterValueChangedCallback(e => ColliderStudioSettings.XRay = e.newValue);
            xrayToggle.style.marginBottom = 10;
            root.Add(xrayToggle);

            // 7. Quick Mesh Actions
            root.Add(CreateSectionHeader("QUICK MESH ACTIONS"));

            var autoFitBtn = new Button(AutoFitSelected)
            {
                text = "Auto-Fit to Mesh",
                tooltip = "Calculates local bounds of all meshes on this object or children, and fits the collider snugly around them."
            };
            autoFitBtn.style.backgroundColor = new Color(0.18f, 0.48f, 0.24f, 1f); // Fresh green
            autoFitBtn.style.color = Color.white;
            autoFitBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            autoFitBtn.style.height = 24;
            autoFitBtn.style.marginBottom = 4;
            root.Add(autoFitBtn);

            var actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.marginBottom = 8;

            var centerMeshBtn = new Button(CenterSelectedOnMesh)
            {
                text = "Center on Mesh",
                tooltip = "Re-centers the collider on the mesh without modifying its size."
            };
            centerMeshBtn.style.flexGrow = 1;

            var resetBtn = new Button(ResetSelectedCollider)
            {
                text = "Reset Bounds",
                tooltip = "Resets collider center to (0,0,0) and size/radius to standard default."
            };
            resetBtn.style.flexGrow = 1;

            actionRow.Add(centerMeshBtn);
            actionRow.Add(resetBtn);
            root.Add(actionRow);

            // Dynamic selection update
            void RefreshSelection()
            {
                var activeGo = Selection.activeGameObject;
                if (activeGo == null)
                {
                    selectionLabel.text = "Selection: None (Select an object)";
                    selectionLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                    quickAddRow.style.display = DisplayStyle.None;
                    autoFitBtn.SetEnabled(false);
                    centerMeshBtn.SetEnabled(false);
                    resetBtn.SetEnabled(false);
                    return;
                }

                var colliders = activeGo.GetComponents<Collider>();
                if (colliders.Length == 0)
                {
                    selectionLabel.text = $"{activeGo.name} (No Collider)";
                    selectionLabel.style.color = new Color(1f, 0.75f, 0.3f);
                    quickAddRow.style.display = DisplayStyle.Flex;
                    autoFitBtn.SetEnabled(false);
                    centerMeshBtn.SetEnabled(false);
                    resetBtn.SetEnabled(false);
                }
                else
                {
                    string colTypes = "";
                    for (int i = 0; i < colliders.Length; i++)
                    {
                        var c = colliders[i];
                        string trig = c.isTrigger ? " [Trigger]" : "";
                        colTypes += (i > 0 ? ", " : "") + c.GetType().Name + trig;
                    }

                    selectionLabel.text = $"{activeGo.name}: {colTypes}";
                    selectionLabel.style.color = new Color(0f, 0.9f, 1f);
                    quickAddRow.style.display = DisplayStyle.None;
                    autoFitBtn.SetEnabled(true);
                    centerMeshBtn.SetEnabled(true);
                    resetBtn.SetEnabled(true);
                }
            }

            RefreshSelection();
            Selection.selectionChanged += RefreshSelection;

            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                Selection.selectionChanged -= RefreshSelection;
            });

            return root;
        }

        private static Label CreateSectionHeader(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 10;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.color = new Color(0.65f, 0.65f, 0.65f);
            l.style.marginTop = 4;
            l.style.marginBottom = 3;
            return l;
        }

        private static void AutoFitSelected()
        {
            var go = Selection.activeGameObject;
            if (go == null) return;

            var colliders = go.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                ColliderStudioMeshUtility.AutoFitCollider(colliders[i]);
            }
        }

        private static void CenterSelectedOnMesh()
        {
            var go = Selection.activeGameObject;
            if (go == null) return;

            var colliders = go.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                ColliderStudioMeshUtility.CenterColliderOnMesh(colliders[i]);
            }
        }

        private static void ResetSelectedCollider()
        {
            var go = Selection.activeGameObject;
            if (go == null) return;

            var colliders = go.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                ColliderStudioMeshUtility.ResetCollider(colliders[i]);
            }
        }

        private static void AddColliderToSelected<T>() where T : Collider
        {
            var go = Selection.activeGameObject;
            if (go == null) return;

            Undo.AddComponent<T>(go);
            var col = go.GetComponent<T>();
            ColliderStudioMeshUtility.AutoFitCollider(col);
            EditorUtility.SetDirty(go);
            Selection.activeGameObject = go; // Refresh
        }
    }
}
