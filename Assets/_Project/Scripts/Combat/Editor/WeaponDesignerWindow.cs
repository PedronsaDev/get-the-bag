using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Combat.Editor
{
    /// <summary>
    /// Comprehensive UI Toolkit custom editor window for visually authoring, editing,
    /// inspecting, and procedurally generating Borderlands-style modular weapons.
    /// Allows live in-window stat editing for Cores and Parts, instant 3D viewport previewing,
    /// DPS analytics, and one-click asset creation.
    /// </summary>
    public class WeaponDesignerWindow : EditorWindow
    {
        [MenuItem("Tools/Combat/Weapon Designer")]
        [MenuItem("Window/Combat/Weapon Designer")]
        public static void Open()
        {
            var window = GetWindow<WeaponDesignerWindow>();
            window.titleContent = new GUIContent("Weapon Designer");
            window.minSize = new Vector2(880, 680);
        }

        private enum EditorTab { AssembleAndRoll, EditCore, EditPart }
        private EditorTab _currentTab = EditorTab.AssembleAndRoll;

        // Current Weapon State
        private WeaponCoreDefinition _selectedCore;
        private WeaponPartDefinition _selectedBarrel;
        private WeaponPartDefinition _selectedGrip;
        private WeaponPartDefinition _selectedStock;
        private WeaponPartDefinition _selectedSight;
        private WeaponPartDefinition _selectedMagazine;
        private WeaponPartDefinition _selectedAccessory;

        private WeaponPartDefinition _inspectedPart;
        private bool _isRebuildingUI;
        private WeaponCoreDefinition _builtCoreTarget;
        private WeaponPartDefinition _builtPartTarget;

        private uint _proceduralSeed = 1337;
        private ItemRarity _proceduralRarity = ItemRarity.Rare;
        private byte _itemLevel = 1;

        // 3D Viewport State
        private PreviewRenderUtility _previewUtility;
        private Image _previewImage;
        private float _previewRotX = 15f;
        private float _previewRotY = -45f;
        private float _previewZoom = 1.4f;
        private bool _isDragging;
        private Vector2 _lastPointerPos;
        private GameObject _previewRootObject;
        private static readonly Rect FixedPreviewRect = new Rect(0, 0, 512, 256);

        // UI References - Navigation & Tabs
        private Button _tabAssembleBtn;
        private Button _tabCoreBtn;
        private Button _tabPartBtn;
        private VisualElement _tabAssembleContent;
        private VisualElement _tabCoreContent;
        private VisualElement _tabPartContent;

        // UI References - Slot Fields & Rows
        private ObjectField _coreObjectField;
        private ObjectField _barrelObjectField;
        private ObjectField _gripObjectField;
        private ObjectField _stockObjectField;
        private ObjectField _sightObjectField;
        private ObjectField _magObjectField;
        private ObjectField _accessoryObjectField;

        private VisualElement _barrelRow;
        private VisualElement _gripRow;
        private VisualElement _stockRow;
        private VisualElement _sightRow;
        private VisualElement _magRow;
        private VisualElement _accessoryRow;

        // UI References - Live Card & DPS
        private VisualElement _cardRoot;
        private Label _weaponNameLabel;
        private Label _rarityLabel;
        private Label _redTextLabel;
        private VisualElement _statsContainer;
        private VisualElement _dpsContainer;

        private void OnEnable()
        {
            InitializePreviewUtility();
        }

        private void OnDisable()
        {
            CleanupPreviewUtility();
        }

        private void InitializePreviewUtility()
        {
            if (_previewUtility == null)
            {
                _previewUtility = new PreviewRenderUtility();
                _previewUtility.cameraFieldOfView = 30f;
                _previewUtility.camera.nearClipPlane = 0.05f;
                _previewUtility.camera.farClipPlane = 25f;
                _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
                _previewUtility.camera.backgroundColor = new Color(0.12f, 0.12f, 0.15f, 1f);
                _previewUtility.camera.allowHDR = false;
                _previewUtility.camera.allowMSAA = false;
                _previewUtility.camera.useOcclusionCulling = false;

                if (_previewUtility.lights is { Length: > 0 })
                {
                    _previewUtility.lights[0].intensity = 1.3f;
                    _previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 40f, 0f);
                    _previewUtility.lights[0].color = Color.white;

                    if (_previewUtility.lights.Length > 1)
                    {
                        _previewUtility.lights[1].intensity = 0.7f;
                        _previewUtility.lights[1].transform.rotation = Quaternion.Euler(-25f, -140f, 0f);
                        _previewUtility.lights[1].color = new Color(0.7f, 0.85f, 1.0f);
                    }
                }
            }
        }

        private void CleanupPreviewUtility()
        {
            DestroyPreviewRoot();
            if (_previewUtility != null)
            {
                _previewUtility.Cleanup();
                _previewUtility = null;
            }
        }

        private void DestroyPreviewRoot()
        {
            if (_previewRootObject)
            {
                DestroyImmediate(_previewRootObject);
                _previewRootObject = null;
            }
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;
            root.style.backgroundColor = new Color(0.18f, 0.18f, 0.20f);

            // ================= LEFT COLUMN: WORKSPACE TABS =================
            var leftCol = new VisualElement();
            leftCol.style.width = new Length(48, LengthUnit.Percent);
            leftCol.style.paddingRight = 12;

            // Title Header
            var titleBox = new VisualElement();
            titleBox.style.marginBottom = 8;
            titleBox.style.paddingBottom = 4;
            titleBox.style.borderBottomWidth = 1;
            titleBox.style.borderBottomColor = new Color(0.3f, 0.3f, 0.35f);

            var titleLabel = new Label("PROCEDURAL WEAPON DESIGNER");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 15;
            titleLabel.style.color = new Color(0.95f, 0.85f, 0.3f);
            titleBox.Add(titleLabel);

            var subtitle = new Label("Live In-Editor Stat Authoring, Assembly & 3D Visualizer");
            subtitle.style.fontSize = 10;
            subtitle.style.color = Color.gray;
            titleBox.Add(subtitle);
            leftCol.Add(titleBox);

            // Tab Navigation Bar
            var tabBar = new VisualElement();
            tabBar.style.flexDirection = FlexDirection.Row;
            tabBar.style.marginBottom = 10;

            _tabAssembleBtn = CreateTabButton("Assemble & Roll", EditorTab.AssembleAndRoll);
            _tabCoreBtn = CreateTabButton("Core Stats", EditorTab.EditCore);
            _tabPartBtn = CreateTabButton("Part Editor", EditorTab.EditPart);

            tabBar.Add(_tabAssembleBtn);
            tabBar.Add(_tabCoreBtn);
            tabBar.Add(_tabPartBtn);
            leftCol.Add(tabBar);

            // Tab Content Containers
            var tabContentScroll = new ScrollView();
            tabContentScroll.style.flexGrow = 1;

            _tabAssembleContent = new VisualElement();
            _tabCoreContent = new VisualElement();
            _tabPartContent = new VisualElement();

            BuildAssembleTab(_tabAssembleContent);
            BuildCoreEditorTab(_tabCoreContent);
            BuildPartEditorTab(_tabPartContent);

            tabContentScroll.Add(_tabAssembleContent);
            tabContentScroll.Add(_tabCoreContent);
            tabContentScroll.Add(_tabPartContent);
            leftCol.Add(tabContentScroll);

            root.Add(leftCol);

            // ================= RIGHT COLUMN: 3D VIEWPORT & WEAPON CARD =================
            var rightCol = new ScrollView();
            rightCol.style.width = new Length(52, LengthUnit.Percent);
            rightCol.style.paddingLeft = 12;

            // 3D Viewport Header
            var viewportHeader = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 6 } };
            var vpLabel = new Label("3D WEAPON VIEWPORT");
            vpLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            vpLabel.style.fontSize = 13;
            viewportHeader.Add(vpLabel);

            var resetCamBtn = new Button(() => { _previewRotX = 15f; _previewRotY = -45f; _previewZoom = 1.4f; Render3DPreview(); }) { text = "↺ Reset Camera" };
            resetCamBtn.style.height = 18;
            resetCamBtn.style.fontSize = 10;
            viewportHeader.Add(resetCamBtn);
            rightCol.Add(viewportHeader);

            // 3D Viewport Image Container
            _previewImage = new Image();
            _previewImage.style.height = 240;
            _previewImage.style.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
            _previewImage.style.borderTopLeftRadius = 6;
            _previewImage.style.borderTopRightRadius = 6;
            _previewImage.style.borderBottomLeftRadius = 6;
            _previewImage.style.borderBottomRightRadius = 6;
            _previewImage.style.marginBottom = 10;

            // Interactive Drag Controls for 3D Camera
            _previewImage.RegisterCallback<AttachToPanelEvent>(evt => RebuildPreviewModel());

            _previewImage.RegisterCallback<PointerDownEvent>(evt =>
            {
                _isDragging = true;
                _lastPointerPos = (Vector2)evt.position;
                _previewImage.CapturePointer(evt.pointerId);
            });

            _previewImage.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_isDragging)
                {
                    Vector2 currentPos = (Vector2)evt.position;
                    Vector2 delta = currentPos - _lastPointerPos;
                    _lastPointerPos = currentPos;
                    _previewRotY += delta.x * 0.75f;
                    _previewRotX = Mathf.Clamp(_previewRotX + delta.y * 0.75f, -85f, 85f);
                    Render3DPreview();
                }
            });

            _previewImage.RegisterCallback<PointerUpEvent>(evt =>
            {
                _isDragging = false;
                if (_previewImage.HasPointerCapture(evt.pointerId))
                    _previewImage.ReleasePointer(evt.pointerId);
            });

            _previewImage.RegisterCallback<PointerCaptureOutEvent>(evt => _isDragging = false);

            _previewImage.RegisterCallback<WheelEvent>(evt =>
            {
                _previewZoom = Mathf.Clamp(_previewZoom + (evt.delta.y * 0.08f), 0.35f, 4.5f);
                Render3DPreview();
                evt.StopPropagation();
            });

            rightCol.Add(_previewImage);

            // Borderlands-Style Weapon Card
            var cardHeader = new Label("LIVE WEAPON CARD & DPS");
            cardHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            cardHeader.style.fontSize = 13;
            cardHeader.style.marginBottom = 6;
            rightCol.Add(cardHeader);

            _cardRoot = new VisualElement();
            _cardRoot.style.backgroundColor = new Color(0.11f, 0.11f, 0.13f);
            _cardRoot.style.borderTopWidth = 3;
            _cardRoot.style.borderBottomWidth = 3;
            _cardRoot.style.borderLeftWidth = 3;
            _cardRoot.style.borderRightWidth = 3;
            _cardRoot.style.borderTopColor = Color.white;
            _cardRoot.style.borderBottomColor = Color.white;
            _cardRoot.style.borderLeftColor = Color.white;
            _cardRoot.style.borderRightColor = Color.white;
            _cardRoot.style.borderTopLeftRadius = 8;
            _cardRoot.style.borderTopRightRadius = 8;
            _cardRoot.style.borderBottomLeftRadius = 8;
            _cardRoot.style.borderBottomRightRadius = 8;
            _cardRoot.style.paddingLeft = 14;
            _cardRoot.style.paddingRight = 14;
            _cardRoot.style.paddingTop = 14;
            _cardRoot.style.paddingBottom = 14;

            _weaponNameLabel = new Label("Select a Weapon Core");
            _weaponNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _weaponNameLabel.style.fontSize = 16;
            _cardRoot.Add(_weaponNameLabel);

            _rarityLabel = new Label("COMMON WEAPON");
            _rarityLabel.style.fontSize = 11;
            _rarityLabel.style.marginBottom = 6;
            _cardRoot.Add(_rarityLabel);

            _redTextLabel = new Label(string.Empty);
            _redTextLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            _redTextLabel.style.color = new Color(0.95f, 0.25f, 0.25f);
            _redTextLabel.style.marginBottom = 8;
            _cardRoot.Add(_redTextLabel);

            // Combat Stats
            _statsContainer = new VisualElement();
            _cardRoot.Add(_statsContainer);

            // DPS Section Divider
            var dpsDivider = new VisualElement();
            dpsDivider.style.height = 1;
            dpsDivider.style.backgroundColor = new Color(0.25f, 0.25f, 0.3f);
            dpsDivider.style.marginTop = 8;
            dpsDivider.style.marginBottom = 8;
            _cardRoot.Add(dpsDivider);

            _dpsContainer = new VisualElement();
            _cardRoot.Add(_dpsContainer);

            rightCol.Add(_cardRoot);
            root.Add(rightCol);

            SwitchTab(EditorTab.AssembleAndRoll);
            RefreshWeaponCard();
        }

        // ================= TAB MANAGEMENT =================

        private Button CreateTabButton(string label, EditorTab tab)
        {
            var btn = new Button(() => SwitchTab(tab)) { text = label };
            btn.style.flexGrow = 1;
            btn.style.height = 28;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            return btn;
        }

        private void SwitchTab(EditorTab tab)
        {
            _currentTab = tab;

            _tabAssembleBtn.style.backgroundColor = tab == EditorTab.AssembleAndRoll ? new Color(0.25f, 0.45f, 0.75f) : new Color(0.22f, 0.22f, 0.24f);
            _tabCoreBtn.style.backgroundColor = tab == EditorTab.EditCore ? new Color(0.25f, 0.45f, 0.75f) : new Color(0.22f, 0.22f, 0.24f);
            _tabPartBtn.style.backgroundColor = tab == EditorTab.EditPart ? new Color(0.25f, 0.45f, 0.75f) : new Color(0.22f, 0.22f, 0.24f);

            _tabAssembleContent.style.display = tab == EditorTab.AssembleAndRoll ? DisplayStyle.Flex : DisplayStyle.None;
            _tabCoreContent.style.display = tab == EditorTab.EditCore ? DisplayStyle.Flex : DisplayStyle.None;
            _tabPartContent.style.display = tab == EditorTab.EditPart ? DisplayStyle.Flex : DisplayStyle.None;

            if (tab == EditorTab.EditCore && _builtCoreTarget != _selectedCore)
            {
                BuildCoreEditorTab(_tabCoreContent);
            }
            else if (tab == EditorTab.EditPart && _builtPartTarget != _inspectedPart)
            {
                BuildPartEditorTab(_tabPartContent);
            }
        }

        // ================= TAB 1: ASSEMBLE & ROLL =================

        private void BuildAssembleTab(VisualElement container)
        {
            container.Clear();

            // 1. Core Archetype
            var coreSection = CreateStyledSection("1. Core Archetype");
            var coreRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _coreObjectField = new ObjectField { objectType = typeof(WeaponCoreDefinition), style = { flexGrow = 1 }, value = _selectedCore };
            _coreObjectField.RegisterValueChangedCallback(evt =>
            {
                if (_isRebuildingUI) return;
                _selectedCore = evt.newValue as WeaponCoreDefinition;
                OnCoreSelected();
            });
            coreRow.Add(_coreObjectField);

            var editCoreBtn = new Button(() =>
            {
                BuildCoreEditorTab(_tabCoreContent);
                SwitchTab(EditorTab.EditCore);
            }) { text = "✏️" };
            editCoreBtn.tooltip = "Edit this Core's base stats and properties";
            editCoreBtn.style.marginLeft = 4;
            editCoreBtn.style.width = 28;
            coreRow.Add(editCoreBtn);

            var newCoreBtn = new Button(CreateNewCoreAsset) { text = "+" };
            newCoreBtn.tooltip = "Create a new Weapon Core asset";
            newCoreBtn.style.marginLeft = 4;
            newCoreBtn.style.width = 28;
            newCoreBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            coreRow.Add(newCoreBtn);

            coreSection.Add(coreRow);
            container.Add(coreSection);

            // 2. Modular Part Slots
            var partsSection = CreateStyledSection("2. Modular Part Slots");
            _barrelObjectField = CreateSlotRow(partsSection, "Barrel", WeaponPartType.Barrel, p => _selectedBarrel = p, out _barrelRow);
            _gripObjectField = CreateSlotRow(partsSection, "Grip", WeaponPartType.Grip, p => _selectedGrip = p, out _gripRow);
            _stockObjectField = CreateSlotRow(partsSection, "Stock", WeaponPartType.Stock, p => _selectedStock = p, out _stockRow);
            _sightObjectField = CreateSlotRow(partsSection, "Sight", WeaponPartType.Sight, p => _selectedSight = p, out _sightRow);
            _magObjectField = CreateSlotRow(partsSection, "Magazine", WeaponPartType.Magazine, p => _selectedMagazine = p, out _magRow);
            _accessoryObjectField = CreateSlotRow(partsSection, "Accessory", WeaponPartType.Accessory, p => _selectedAccessory = p, out _accessoryRow);
            container.Add(partsSection);

            // 3. Procedural Generator
            var rollSection = CreateStyledSection("3. Procedural Roll");
            var seedField = new IntegerField("Random Seed") { value = (int)_proceduralSeed };
            seedField.RegisterValueChangedCallback(evt => _proceduralSeed = (uint)Mathf.Max(0, evt.newValue));
            rollSection.Add(seedField);

            var rarityField = new EnumField("Rarity", _proceduralRarity);
            rarityField.RegisterValueChangedCallback(evt => { _proceduralRarity = (ItemRarity)evt.newValue; RefreshWeaponCard(); });
            rollSection.Add(rarityField);

            var levelField = new SliderInt("Level", 1, 50) { value = _itemLevel, showInputField = true };
            levelField.RegisterValueChangedCallback(evt => { _itemLevel = (byte)evt.newValue; RefreshWeaponCard(); });
            rollSection.Add(levelField);

            var rollBtn = new Button(RollProceduralWeapon) { text = "Roll Random Weapon" };
            rollBtn.style.marginTop = 8;
            rollBtn.style.height = 32;
            rollBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            rollBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.8f);
            rollSection.Add(rollBtn);

            var exportPresetBtn = new Button(ExportCurrentAsPreset) { text = "Export As Dedicated Boss Drop Preset" };
            exportPresetBtn.style.marginTop = 6;
            exportPresetBtn.style.height = 28;
            exportPresetBtn.style.backgroundColor = new Color(0.6f, 0.35f, 0.1f);
            exportPresetBtn.tooltip = "Saves the currently assembled combination as a dedicated named weapon preset for boss drops or quest rewards.";
            rollSection.Add(exportPresetBtn);

            container.Add(rollSection);
            UpdateSlotVisibility();
        }

        private ObjectField CreateSlotRow(VisualElement parent, string label, WeaponPartType partType, Action<WeaponPartDefinition> onSelected, out VisualElement createdRow)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
            createdRow = row;

            var field = new ObjectField(label) { objectType = typeof(WeaponPartDefinition), style = { flexGrow = 1 } };
            field.RegisterValueChangedCallback(evt =>
            {
                if (_isRebuildingUI) return;
                onSelected?.Invoke(evt.newValue as WeaponPartDefinition);
                RebuildPreviewModel();
                RefreshWeaponCard();
            });
            row.Add(field);

            var editBtn = new Button(() =>
            {
                var currentPart = field.value as WeaponPartDefinition;
                if (currentPart != null)
                {
                    _inspectedPart = currentPart;
                    BuildPartEditorTab(_tabPartContent);
                    SwitchTab(EditorTab.EditPart);
                }
            }) { text = "✏️" };
            editBtn.tooltip = $"Edit this {label}'s stats, red text, and modifiers";
            editBtn.style.marginLeft = 4;
            editBtn.style.width = 28;
            row.Add(editBtn);

            var createBtn = new Button(() => CreateNewPartAsset(partType)) { text = "+" };
            createBtn.tooltip = $"Create new {label} in Assets/_Project/Data/Weapons/Parts/";
            createBtn.style.marginLeft = 4;
            createBtn.style.width = 28;
            createBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(createBtn);

            parent.Add(row);
            return field;
        }

        // ================= TAB 2: EDIT CORE STATS =================

        private void BuildCoreEditorTab(VisualElement container)
        {
            _builtCoreTarget = _selectedCore;
            container.Clear();

            if (!_selectedCore)
            {
                var warnBox = new Label("No Weapon Core is currently selected.\nSelect or create a core in the 'Assemble & Roll' tab first.");
                warnBox.style.color = new Color(0.9f, 0.6f, 0.2f);
                warnBox.style.paddingTop = 16;
                container.Add(warnBox);
                return;
            }

            _isRebuildingUI = true;
            try
            {
                var so = new SerializedObject(_selectedCore);
                so.Update();

                // Core Switcher Header
                var topBar = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 8 } };
                var coreField = new ObjectField("Active Core") { objectType = typeof(WeaponCoreDefinition), value = _selectedCore, style = { flexGrow = 1 } };
                coreField.RegisterValueChangedCallback(evt =>
                {
                    if (_isRebuildingUI) return;
                    _selectedCore = evt.newValue as WeaponCoreDefinition;
                    if (_coreObjectField != null) _coreObjectField.SetValueWithoutNotify(_selectedCore);
                    OnCoreSelected();
                    BuildCoreEditorTab(container);
                });
                topBar.Add(coreField);
                container.Add(topBar);

                // 1. Identity
                var idSec = CreateStyledSection("Core Identity");
                var guidLabel = new Label($"Asset GUID: {_selectedCore.Guid}") { style = { color = Color.gray, fontSize = 10, marginBottom = 4 } };
                idSec.Add(guidLabel);
                BindTextField(idSec, so, "_coreId", "Command ID (Console / Debug)");
                BindTextField(idSec, so, "_baseWeaponName", "Base Weapon Name");
                BindPropertyField(idSec, so, "_weaponType", "Weapon Type", () => { UpdateSlotVisibility(); });
                BindPropertyField(idSec, so, "_manufacturer", "Manufacturer");
                BindPropertyField(idSec, so, "_defaultRarity", "Default Rarity");
                container.Add(idSec);

                // 2. Base Combat Stats
                var statSec = CreateStyledSection("Base Combat Stats");
                BindFloatField(statSec, so, "_baseDamage", "Base Damage");
                BindFloatField(statSec, so, "_baseFireRate", "Base Fire Rate (/sec)");
                BindIntField(statSec, so, "_baseMagazineSize", "Magazine Size");
                BindFloatField(statSec, so, "_baseReloadSpeed", "Reload Speed (sec)");
                BindFloatField(statSec, so, "_baseCriticalMultiplier", "Critical Multiplier");
                container.Add(statSec);

                // 3. Handling & Ballistics
                var handlingSec = CreateStyledSection("Handling, Recoil & Pellets");
                BindFloatField(handlingSec, so, "_baseSpread", "Base Spread (deg)");
                BindFloatField(handlingSec, so, "_maxSpread", "Max Spread (deg)");
                BindFloatField(handlingSec, so, "_baseRecoilKick", "Recoil Kick");
                BindIntField(handlingSec, so, "_basePelletsPerShot", "Pellets Per Shot");
                BindFloatField(handlingSec, so, "_baseProjectileSpeed", "Projectile Speed (0=Hitscan)");
                container.Add(handlingSec);

                // 4. Firing Cycle & Elements
                var fireSec = CreateStyledSection("Firing Cycle & Delivery");
                BindPropertyField(fireSec, so, "_baseElement", "Base Element");
                BindPropertyField(fireSec, so, "_baseFiringMode", "Firing Mode");
                BindIntField(fireSec, so, "_baseBurstCount", "Burst Count");
                BindFloatField(fireSec, so, "_baseBurstInterval", "Burst Interval (sec)");
                container.Add(fireSec);

                // 5. Global & Local Level Scaling
                var scaleSec = CreateStyledSection("Level Damage Scaling");

                var globalBox = new VisualElement();
                globalBox.style.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
                globalBox.style.paddingLeft = 8;
                globalBox.style.paddingTop = 8;
                globalBox.style.paddingRight = 8;
                globalBox.style.paddingBottom = 8;
                globalBox.style.marginBottom = 8;
                globalBox.style.borderTopLeftRadius = 4;
                globalBox.style.borderTopRightRadius = 4;
                globalBox.style.borderBottomLeftRadius = 4;
                globalBox.style.borderBottomRightRadius = 4;

                var globalTitle = new Label("🌐 Global Scaling (Shared by All Weapons & Items)");
                globalTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                globalTitle.style.color = new Color(0.4f, 0.8f, 1f);
                globalTitle.style.marginBottom = 6;
                globalBox.Add(globalTitle);

                var globalSettings = ItemScalingSettings.Instance;
                if (globalSettings != null)
                {
                    var globalSO = new SerializedObject(globalSettings);
                    BindPropertyField(globalBox, globalSO, "_damageScalingMode", "Global Mode (Exp / Linear)");
                    BindFloatField(globalBox, globalSO, "_damageScalePerLevel", "Global Rate / Level (e.g. 0.08 = 8%)");
                    BindFloatField(globalBox, globalSO, "_shieldScalePerLevel", "Global Shield Rate / Level");
                    BindFloatField(globalBox, globalSO, "_healthScalePerLevel", "Global Enemy Health Rate");
                    BindToggle(globalBox, globalSO, "_useCustomDamageCurve", "Use Global Custom Curve");
                    BindPropertyField(globalBox, globalSO, "_customDamageCurve", "Global Curve");

                    var pingBtn = new Button(() => EditorGUIUtility.PingObject(globalSettings)) { text = "📍 Select Global Settings Asset" };
                    pingBtn.style.marginTop = 6;
                    globalBox.Add(pingBtn);
                }
                scaleSec.Add(globalBox);

                // Local Core Override
                var overrideToggle = new Toggle("Override Global Scaling on this Core") { value = so.FindProperty("_overrideGlobalScaling").boolValue };
                var localBox = new VisualElement();
                localBox.style.display = overrideToggle.value ? DisplayStyle.Flex : DisplayStyle.None;
                localBox.style.marginTop = 6;
                localBox.style.paddingLeft = 8;

                overrideToggle.RegisterValueChangedCallback(evt =>
                {
                    if (_isRebuildingUI) return;
                    so.FindProperty("_overrideGlobalScaling").boolValue = evt.newValue;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_selectedCore);
                    localBox.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                    RefreshWeaponCard();
                });
                scaleSec.Add(overrideToggle);

                BindPropertyField(localBox, so, "_levelScalingMode", "Core Override Mode");
                BindFloatField(localBox, so, "_damageScalePerLevel", "Core Scale Rate / Level");
                BindToggle(localBox, so, "_useCustomLevelCurve", "Core Use Custom Curve");
                BindPropertyField(localBox, so, "_customLevelCurve", "Core Custom Curve");
                scaleSec.Add(localBox);

                container.Add(scaleSec);

                // 6. Visual Body Prefab & Sockets
                var visualSec = CreateStyledSection("3D Visual Body & Sockets");
                BindPropertyField(visualSec, so, "_bodyPrefab", "Body Prefab (Sockets Root)", () => RebuildPreviewModel());

                var autoDetectBtn = new Button(() => AutoDetectSocketsFromPrefab(so)) { text = "Auto-Detect Sockets on Body Prefab" };
                autoDetectBtn.style.marginTop = 6;
                autoDetectBtn.style.marginBottom = 6;
                visualSec.Add(autoDetectBtn);

                var socketsBox = new VisualElement();
                socketsBox.style.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
                socketsBox.style.paddingLeft = 6;
                socketsBox.style.paddingTop = 6;
                socketsBox.style.paddingBottom = 6;
                socketsBox.style.paddingRight = 6;
                socketsBox.style.borderTopLeftRadius = 4;
                socketsBox.style.borderTopRightRadius = 4;
                socketsBox.style.borderBottomLeftRadius = 4;
                socketsBox.style.borderBottomRightRadius = 4;
                var socketsTitle = new Label("Physical Slot Support on Body:");
                socketsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                socketsTitle.style.marginBottom = 4;
                socketsBox.Add(socketsTitle);

                BindToggle(socketsBox, so, "_supportsBarrel", "Barrel Slot", () => UpdateSlotVisibility());
                BindToggle(socketsBox, so, "_supportsGrip", "Grip Slot", () => UpdateSlotVisibility());
                BindToggle(socketsBox, so, "_supportsStock", "Stock Slot", () => UpdateSlotVisibility());
                BindToggle(socketsBox, so, "_supportsSight", "Sight Slot", () => UpdateSlotVisibility());
                BindToggle(socketsBox, so, "_supportsMagazine", "Magazine Slot", () => UpdateSlotVisibility());
                BindToggle(socketsBox, so, "_supportsAccessory", "Accessory Slot", () => UpdateSlotVisibility());
                visualSec.Add(socketsBox);

                container.Add(visualSec);
            }
            finally
            {
                _isRebuildingUI = false;
            }
        }

        private void AutoDetectSocketsFromPrefab(SerializedObject coreSO)
        {
            var prefabProp = coreSO.FindProperty("_bodyPrefab");
            var prefab = prefabProp.objectReferenceValue as GameObject;
            if (!prefab)
            {
                EditorUtility.DisplayDialog("Auto-Detect Sockets", "Please assign a Body Prefab first!", "OK");
                return;
            }

            bool hasBarrel = false, hasGrip = false, hasStock = false, hasSight = false, hasMag = false, hasAcc = false;
            foreach (var trans in prefab.GetComponentsInChildren<Transform>(true))
            {
                string name = trans.name.ToLower();
                if (name.Contains("socket_barrel")) hasBarrel = true;
                if (name.Contains("socket_grip")) hasGrip = true;
                if (name.Contains("socket_stock")) hasStock = true;
                if (name.Contains("socket_sight")) hasSight = true;
                if (name.Contains("socket_mag")) hasMag = true;
                if (name.Contains("socket_accessory")) hasAcc = true;
            }

            coreSO.FindProperty("_supportsBarrel").boolValue = hasBarrel;
            coreSO.FindProperty("_supportsGrip").boolValue = hasGrip;
            coreSO.FindProperty("_supportsStock").boolValue = hasStock;
            coreSO.FindProperty("_supportsSight").boolValue = hasSight;
            coreSO.FindProperty("_supportsMagazine").boolValue = hasMag;
            coreSO.FindProperty("_supportsAccessory").boolValue = hasAcc;
            coreSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(_selectedCore);

            UpdateSlotVisibility();
            BuildCoreEditorTab(_tabCoreContent);
            EditorUtility.DisplayDialog("Auto-Detect Sockets", $"Sockets detected on '{prefab.name}':\nBarrel: {hasBarrel}, Grip: {hasGrip}, Stock: {hasStock}, Sight: {hasSight}, Mag: {hasMag}, Acc: {hasAcc}", "OK");
        }

        // ================= TAB 3: EDIT PART STATS =================

        private void BuildPartEditorTab(VisualElement container)
        {
            _builtPartTarget = _inspectedPart;
            container.Clear();

            // Part Selector Bar
            var topBar = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 8 } };
            var partField = new ObjectField("Active Part") { objectType = typeof(WeaponPartDefinition), value = _inspectedPart, style = { flexGrow = 1 } };
            partField.RegisterValueChangedCallback(evt =>
            {
                if (_isRebuildingUI) return;
                _inspectedPart = evt.newValue as WeaponPartDefinition;
                BuildPartEditorTab(container);
            });
            topBar.Add(partField);
            container.Add(topBar);

            if (_inspectedPart == null)
            {
                // Display quick selection buttons from currently mounted parts
                var selectHint = new Label("Select a part above or choose from the currently mounted weapon parts:");
                selectHint.style.color = Color.gray;
                selectHint.style.marginTop = 6;
                selectHint.style.marginBottom = 6;
                container.Add(selectHint);

                AddQuickPartButton(container, "Barrel", _selectedBarrel);
                AddQuickPartButton(container, "Grip", _selectedGrip);
                AddQuickPartButton(container, "Stock", _selectedStock);
                AddQuickPartButton(container, "Sight", _selectedSight);
                AddQuickPartButton(container, "Magazine", _selectedMagazine);
                AddQuickPartButton(container, "Accessory", _selectedAccessory);
                return;
            }

            _isRebuildingUI = true;
            try
            {
                var so = new SerializedObject(_inspectedPart);
                so.Update();

                // 1. Part Identity
                var idSec = CreateStyledSection("Part Identity & Scoping");
                var guidLabel = new Label($"Asset GUID: {_inspectedPart.Guid}") { style = { color = Color.gray, fontSize = 10, marginBottom = 4 } };
                idSec.Add(guidLabel);
                BindTextField(idSec, so, "_displayName", "Display Name");
                BindTextField(idSec, so, "_partId", "Command ID (Console / Debug)");

                var socketSectionContainer = new VisualElement();

                BindPropertyField(idSec, so, "_partType", "Part Type", () =>
                {
                    UpdatePartSocketUI(so, socketSectionContainer);
                    RebuildPreviewModel();
                });
                BindPropertyField(idSec, so, "_weaponType", "Weapon Type");
                BindToggle(idSec, so, "_isUniversal", "Universal Compatibility (Fits Any Weapon Type)");
                BindPropertyField(idSec, so, "_manufacturer", "Manufacturer");
                BindPropertyField(idSec, so, "_rarity", "Part Rarity");
                container.Add(idSec);

                // 2. Visual Mounting
                var visualSec = CreateStyledSection("Visual Mounting");
                BindPropertyField(visualSec, so, "_meshPrefab", "Mesh Prefab", () => RebuildPreviewModel());
                visualSec.Add(socketSectionContainer);
                UpdatePartSocketUI(so, socketSectionContainer);
                container.Add(visualSec);

                // 3. Legendary Perks & Red Text
                var perkSec = CreateStyledSection("Legendary Perks & Red Text");
                BindToggle(perkSec, so, "_isUnique", "Is Unique / Legendary Part");
                BindTextField(perkSec, so, "_redText", "Red Text Quote");

                // Behavior Preset Dropdown
                var behaviorProp = so.FindProperty("_uniqueBehaviorId");
                var presetRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
                var presetLabel = new Label("Unique Behavior Preset") { style = { width = 150, color = new Color(0.8f, 0.8f, 0.8f) } };
                var presetDropdown = new DropdownField(new List<string> {
                    "[None]",
                    "ConferenceCall_Split (Conference Call)",
                    "SandHawk_Wave (Sand Hawk)",
                    "Harold_AccelerateSplit (Unkempt Harold)",
                    "TheButcher_AmmoRefund (The Butcher)",
                    "[Custom ID]"
                }, GetBehaviorIndex(behaviorProp.stringValue));

                presetDropdown.style.flexGrow = 1;
                presetDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (_isRebuildingUI) return;
                    if (evt.newValue.StartsWith("[None]")) behaviorProp.stringValue = string.Empty;
                    else if (evt.newValue.StartsWith("ConferenceCall")) behaviorProp.stringValue = LegendaryBehaviorIds.ConferenceCall;
                    else if (evt.newValue.StartsWith("SandHawk")) behaviorProp.stringValue = LegendaryBehaviorIds.SandHawk;
                    else if (evt.newValue.StartsWith("Harold")) behaviorProp.stringValue = LegendaryBehaviorIds.UnkemptHarold;
                    else if (evt.newValue.StartsWith("TheButcher")) behaviorProp.stringValue = LegendaryBehaviorIds.TheButcher;

                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_inspectedPart);
                    RefreshWeaponCard();
                });

                presetRow.Add(presetLabel);
                presetRow.Add(presetDropdown);
                perkSec.Add(presetRow);

                BindTextField(perkSec, so, "_uniqueBehaviorId", "Custom Behavior ID");
                container.Add(perkSec);

                // 4. Overrides
                var overrideSec = CreateStyledSection("Behavior Overrides");
                BindToggle(overrideSec, so, "_overridesElement", "Override Element");
                BindPropertyField(overrideSec, so, "_elementOverride", "Element Override");
                BindToggle(overrideSec, so, "_overridesFiringMode", "Override Firing Mode");
                BindPropertyField(overrideSec, so, "_firingModeOverride", "Firing Mode Override");
                container.Add(overrideSec);

                // 5. Stat Modifiers List
                var modSec = CreateStyledSection("Stat Modifiers List");
                var modListContainer = new VisualElement();
                modSec.Add(modListContainer);

                RebuildModifiersList(so, modListContainer);

                var addModBtn = new Button(() =>
                {
                    var modProp = so.FindProperty("_modifiers");
                    modProp.arraySize++;
                    var newElem = modProp.GetArrayElementAtIndex(modProp.arraySize - 1);
                    newElem.FindPropertyRelative("TargetStat").enumValueIndex = (int)StatType.Damage;
                    newElem.FindPropertyRelative("Operation").enumValueIndex = (int)ModifierOperation.PercentageAdditive;
                    newElem.FindPropertyRelative("Value").floatValue = 15f;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_inspectedPart);
                    RebuildModifiersList(so, modListContainer);
                    RefreshWeaponCard();
                }) { text = "+ Add Stat Modifier" };
                addModBtn.style.marginTop = 6;
                modSec.Add(addModBtn);

                container.Add(modSec);
            }
            finally
            {
                _isRebuildingUI = false;
            }
        }

        private void UpdatePartSocketUI(SerializedObject so, VisualElement socketContainer)
        {
            socketContainer.Clear();
            if (_inspectedPart == null) return;

            var partTypeProp = so.FindProperty("_partType");
            WeaponPartType partType = (WeaponPartType)partTypeProp.enumValueIndex;

            if (partType != WeaponPartType.Accessory)
            {
                var socketBadge = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        backgroundColor = new Color(0.18f, 0.22f, 0.26f),
                        borderTopLeftRadius = 4,
                        borderTopRightRadius = 4,
                        borderBottomLeftRadius = 4,
                        borderBottomRightRadius = 4,
                        paddingTop = 6,
                        paddingBottom = 6,
                        paddingLeft = 10,
                        paddingRight = 10,
                        marginTop = 6
                    }
                };
                var socketTitle = new Label("Target Socket:")
                {
                    style = { width = 150, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.7f, 0.85f, 1f) }
                };
                var socketValue = new Label($"\"{_inspectedPart.TargetSocketName}\"  (Auto-routed by {partType})")
                {
                    style = { color = new Color(0.45f, 0.9f, 0.5f), unityFontStyleAndWeight = FontStyle.Bold }
                };
                socketBadge.Add(socketTitle);
                socketBadge.Add(socketValue);
                socketContainer.Add(socketBadge);
            }
            else
            {
                var socketRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4, marginTop = 4 } };
                var socketLabel = new Label("Accessory Socket") { style = { width = 150, color = new Color(0.8f, 0.8f, 0.8f) } };

                var socketOptions = new List<string>
                {
                    $"{WeaponSocketNames.Accessory} (Default Body/Forebody)",
                    $"{WeaponSocketNames.Muzzle} (Muzzle / Suppressor / Bayonet)",
                    $"{WeaponSocketNames.Underbarrel} (Underbarrel / Grip / Bipod)",
                    $"{WeaponSocketNames.SideRail} (Side Rail / Laser / Light)",
                    "Custom..."
                };

                string currentSocket = _inspectedPart.CustomSocketName;
                int defaultIndex = 0;
                if (currentSocket == WeaponSocketNames.Muzzle) defaultIndex = 1;
                else if (currentSocket == WeaponSocketNames.Underbarrel) defaultIndex = 2;
                else if (currentSocket == WeaponSocketNames.SideRail) defaultIndex = 3;
                else if (!string.IsNullOrEmpty(currentSocket) && currentSocket != WeaponSocketNames.Accessory) defaultIndex = 4;

                TextField customSocketField = null;
                var socketDropdown = new DropdownField(socketOptions, defaultIndex) { style = { flexGrow = 1 } };
                socketDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (_isRebuildingUI) return;
                    string selected = evt.newValue;
                    string resolved = WeaponSocketNames.Accessory;
                    if (selected.StartsWith(WeaponSocketNames.Muzzle)) resolved = WeaponSocketNames.Muzzle;
                    else if (selected.StartsWith(WeaponSocketNames.Underbarrel)) resolved = WeaponSocketNames.Underbarrel;
                    else if (selected.StartsWith(WeaponSocketNames.SideRail)) resolved = WeaponSocketNames.SideRail;
                    else if (selected == "Custom...")
                    {
                        if (customSocketField != null) customSocketField.style.display = DisplayStyle.Flex;
                        return;
                    }

                    if (customSocketField != null)
                    {
                        customSocketField.SetValueWithoutNotify(resolved);
                        customSocketField.style.display = DisplayStyle.None;
                    }

                    so.FindProperty("_customSocketName").stringValue = resolved;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_inspectedPart);
                    RebuildPreviewModel();
                });

                socketRow.Add(socketLabel);
                socketRow.Add(socketDropdown);
                socketContainer.Add(socketRow);

                customSocketField = BindTextField(socketContainer, so, "_customSocketName", "Custom Socket Name", () => RebuildPreviewModel());
                if (customSocketField != null)
                {
                    customSocketField.style.display = defaultIndex == 4 ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        private void RebuildModifiersList(SerializedObject so, VisualElement modListContainer)
        {
            modListContainer.Clear();
            var modProp = so.FindProperty("_modifiers");
            if (modProp == null) return;

            for (int i = 0; i < modProp.arraySize; i++)
            {
                int index = i;
                var elem = modProp.GetArrayElementAtIndex(i);
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom = 4;
                row.style.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
                row.style.paddingLeft = 4;
                row.style.paddingRight = 4;
                row.style.paddingTop = 2;
                row.style.paddingBottom = 2;
                row.style.borderTopLeftRadius = 4;
                row.style.borderTopRightRadius = 4;
                row.style.borderBottomLeftRadius = 4;
                row.style.borderBottomRightRadius = 4;

                var statField = new PropertyField(elem.FindPropertyRelative("TargetStat")) { style = { flexGrow = 1 } };
                statField.RegisterValueChangeCallback(evt =>
                {
                    if (_isRebuildingUI) return;
                    EditorUtility.SetDirty(so.targetObject);
                    RefreshWeaponCard();
                });
                row.Add(statField);

                var opField = new PropertyField(elem.FindPropertyRelative("Operation")) { style = { flexGrow = 1, marginLeft = 4 } };
                opField.RegisterValueChangeCallback(evt =>
                {
                    if (_isRebuildingUI) return;
                    EditorUtility.SetDirty(so.targetObject);
                    RefreshWeaponCard();
                });
                row.Add(opField);

                var valField = new PropertyField(elem.FindPropertyRelative("Value")) { style = { width = 60, marginLeft = 4 } };
                valField.RegisterValueChangeCallback(evt =>
                {
                    if (_isRebuildingUI) return;
                    EditorUtility.SetDirty(so.targetObject);
                    RefreshWeaponCard();
                });
                row.Add(valField);

                var delBtn = new Button(() =>
                {
                    modProp.DeleteArrayElementAtIndex(index);
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_inspectedPart);
                    RebuildModifiersList(so, modListContainer);
                    RefreshWeaponCard();
                }) { text = "✕" };
                delBtn.style.marginLeft = 4;
                delBtn.style.color = new Color(0.9f, 0.3f, 0.3f);
                row.Add(delBtn);

                modListContainer.Add(row);
            }

            modListContainer.Bind(so);
        }

        private static int GetBehaviorIndex(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            if (id == LegendaryBehaviorIds.ConferenceCall) return 1;
            if (id == LegendaryBehaviorIds.SandHawk) return 2;
            if (id == LegendaryBehaviorIds.UnkemptHarold) return 3;
            if (id == LegendaryBehaviorIds.TheButcher) return 4;
            return 5;
        }

        private void AddQuickPartButton(VisualElement parent, string label, WeaponPartDefinition part)
        {
            if (part == null) return;
            var btn = new Button(() =>
            {
                _inspectedPart = part;
                BuildPartEditorTab(_tabPartContent);
            }) { text = $"Edit {label}: {part.DisplayName}" };
            btn.style.marginBottom = 4;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            parent.Add(btn);
        }

        // ================= UI BINDING HELPERS =================

        private static VisualElement CreateStyledSection(string title)
        {
            var section = new VisualElement();
            section.style.marginBottom = 10;
            section.style.paddingLeft = 8;
            section.style.paddingRight = 8;
            section.style.paddingTop = 8;
            section.style.paddingBottom = 8;
            section.style.backgroundColor = new Color(0.14f, 0.14f, 0.16f);
            section.style.borderTopLeftRadius = 6;
            section.style.borderTopRightRadius = 6;
            section.style.borderBottomLeftRadius = 6;
            section.style.borderBottomRightRadius = 6;

            var label = new Label(title);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 6;
            section.Add(label);
            return section;
        }

        private TextField BindTextField(VisualElement parent, SerializedObject so, string propName, string label, Action onModified = null)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) return null;

            var field = new TextField(label) { value = prop.stringValue, isDelayed = true };
            field.RegisterValueChangedCallback(evt =>
            {
                if (_isRebuildingUI) return;
                prop.stringValue = evt.newValue;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(so.targetObject);
                onModified?.Invoke();
                RefreshWeaponCard();
            });
            parent.Add(field);
            return field;
        }

        private void BindFloatField(VisualElement parent, SerializedObject so, string propName, string label, Action onModified = null)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) return;

            var field = new FloatField(label) { value = prop.floatValue, isDelayed = true };
            field.RegisterValueChangedCallback(evt =>
            {
                if (_isRebuildingUI) return;
                prop.floatValue = evt.newValue;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(so.targetObject);
                onModified?.Invoke();
                RefreshWeaponCard();
            });
            parent.Add(field);
        }

        private void BindIntField(VisualElement parent, SerializedObject so, string propName, string label, Action onModified = null)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) return;

            var field = new IntegerField(label) { value = prop.intValue, isDelayed = true };
            field.RegisterValueChangedCallback(evt =>
            {
                if (_isRebuildingUI) return;
                prop.intValue = evt.newValue;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(so.targetObject);
                onModified?.Invoke();
                RefreshWeaponCard();
            });
            parent.Add(field);
        }

        private void BindToggle(VisualElement parent, SerializedObject so, string propName, string label, Action onModified = null)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) return;

            var toggle = new Toggle(label) { value = prop.boolValue };
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (_isRebuildingUI) return;
                prop.boolValue = evt.newValue;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(so.targetObject);
                onModified?.Invoke();
                RefreshWeaponCard();
            });
            parent.Add(toggle);
        }

        private void BindPropertyField(VisualElement parent, SerializedObject so, string propName, string label, Action onModified = null)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) return;

            var field = new PropertyField(prop, label);
            field.Bind(so);
            field.RegisterValueChangeCallback(evt =>
            {
                if (_isRebuildingUI) return;
                EditorUtility.SetDirty(so.targetObject);
                onModified?.Invoke();
                RefreshWeaponCard();
            });
            parent.Add(field);
        }

        // ================= CREATION IN DESIGNATED FOLDERS =================

        private void CreateNewCoreAsset()
        {
            string folderPath = "Assets/_Project/Data/Weapons/Cores";
            EnsureFolderHierarchy(folderPath);

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/WeaponCore_New.asset");
            var core = ScriptableObject.CreateInstance<WeaponCoreDefinition>();

            AssetDatabase.CreateAsset(core, assetPath);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLower();

            var so = new SerializedObject(core);
            so.FindProperty("_guid").stringValue = guid;
            so.FindProperty("_coreId").stringValue = "core_" + fileName;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ItemRegistry.Instance?.RegisterCore(core);

            _selectedCore = core;
            if (_coreObjectField != null) _coreObjectField.SetValueWithoutNotify(core);

            EditorGUIUtility.PingObject(core);
            Selection.activeObject = core;
            OnCoreSelected();
            BuildCoreEditorTab(_tabCoreContent);
            SwitchTab(EditorTab.EditCore);
        }

        private void CreateNewPartAsset(WeaponPartType partType)
        {
            WeaponType weaponType = _selectedCore ? _selectedCore.WeaponType : WeaponType.Pistol;
            string weaponTypeFolder = weaponType.ToString();
            string subfolder = partType switch
            {
                WeaponPartType.Barrel => "Barrels",
                WeaponPartType.Grip => "Grips",
                WeaponPartType.Stock => "Stocks",
                WeaponPartType.Sight => "Sights",
                WeaponPartType.Magazine => "Magazines",
                WeaponPartType.Accessory => "Accessories",
                _ => "General"
            };

            string folderPath = $"Assets/_Project/Data/Weapons/Parts/{weaponTypeFolder}/{subfolder}";
            EnsureFolderHierarchy(folderPath);

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/Part_{subfolder}_{weaponType}_New.asset");
            var part = ScriptableObject.CreateInstance<WeaponPartDefinition>();

            AssetDatabase.CreateAsset(part, assetPath);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLower();

            var so = new SerializedObject(part);
            so.FindProperty("_guid").stringValue = guid;
            so.FindProperty("_partId").stringValue = "part_" + fileName;
            so.FindProperty("_partType").enumValueIndex = (int)partType;
            so.FindProperty("_weaponType").enumValueIndex = (int)weaponType;
            so.FindProperty("_displayName").stringValue = $"{weaponType} {subfolder}";
            if (partType == WeaponPartType.Accessory)
            {
                so.FindProperty("_customSocketName").stringValue = WeaponSocketNames.Accessory;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ItemRegistry.Instance?.RegisterPart(part);

            // Automatically add this new part to the selected core's compatible pool
            if (_selectedCore != null)
            {
                var coreSO = new SerializedObject(_selectedCore);
                string poolPropName = partType switch
                {
                    WeaponPartType.Barrel => "_compatibleBarrels",
                    WeaponPartType.Grip => "_compatibleGrips",
                    WeaponPartType.Stock => "_compatibleStocks",
                    WeaponPartType.Sight => "_compatibleSights",
                    WeaponPartType.Magazine => "_compatibleMagazines",
                    WeaponPartType.Accessory => "_compatibleAccessories",
                    _ => null
                };

                if (poolPropName != null)
                {
                    var poolProp = coreSO.FindProperty(poolPropName);
                    if (poolProp != null)
                    {
                        poolProp.arraySize++;
                        poolProp.GetArrayElementAtIndex(poolProp.arraySize - 1).objectReferenceValue = part;
                        coreSO.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_selectedCore);
                    }
                }
            }

            switch (partType)
            {
                case WeaponPartType.Barrel: _selectedBarrel = part; _barrelObjectField?.SetValueWithoutNotify(part); break;
                case WeaponPartType.Grip: _selectedGrip = part; _gripObjectField?.SetValueWithoutNotify(part); break;
                case WeaponPartType.Stock: _selectedStock = part; _stockObjectField?.SetValueWithoutNotify(part); break;
                case WeaponPartType.Sight: _selectedSight = part; _sightObjectField?.SetValueWithoutNotify(part); break;
                case WeaponPartType.Magazine: _selectedMagazine = part; _magObjectField?.SetValueWithoutNotify(part); break;
                case WeaponPartType.Accessory: _selectedAccessory = part; _accessoryObjectField?.SetValueWithoutNotify(part); break;
            }

            _inspectedPart = part;
            EditorGUIUtility.PingObject(part);
            Selection.activeObject = part;
            RebuildPreviewModel();
            RefreshWeaponCard();
            BuildPartEditorTab(_tabPartContent);
            SwitchTab(EditorTab.EditPart);
        }

        private void ExportCurrentAsPreset()
        {
            if (_selectedCore == null)
            {
                EditorUtility.DisplayDialog("Export Preset", "Please select an Active Weapon Core first.", "OK");
                return;
            }

            string folderPath = "Assets/_Project/Data/Weapons/Presets";
            EnsureFolderHierarchy(folderPath);

            var currentWeapon = GetCurrentWeaponInstance();
            string defaultName = currentWeapon != null ? currentWeapon.DisplayName : _selectedCore.BaseWeaponName;
            string cleanFileName = defaultName.Replace(" ", "_").Replace("-", "_");

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/Preset_{cleanFileName}.asset");
            var preset = ScriptableObject.CreateInstance<WeaponPresetDefinition>();

            AssetDatabase.CreateAsset(preset, assetPath);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);

            var so = new SerializedObject(preset);
            so.FindProperty("_guid").stringValue = guid;
            so.FindProperty("_presetId").stringValue = "preset_" + cleanFileName.ToLower();
            so.FindProperty("_presetDisplayName").stringValue = defaultName;
            so.FindProperty("_core").objectReferenceValue = _selectedCore;
            so.FindProperty("_rarity").enumValueIndex = (int)(_proceduralRarity >= ItemRarity.Legendary ? _proceduralRarity : ItemRarity.Legendary);

            // Pin currently mounted parts
            if (_selectedBarrel) so.FindProperty("_lockedBarrel").objectReferenceValue = _selectedBarrel;
            if (_selectedGrip) so.FindProperty("_lockedGrip").objectReferenceValue = _selectedGrip;
            if (_selectedStock) so.FindProperty("_lockedStock").objectReferenceValue = _selectedStock;
            if (_selectedSight) so.FindProperty("_lockedSight").objectReferenceValue = _selectedSight;
            if (_selectedMagazine) so.FindProperty("_lockedMagazine").objectReferenceValue = _selectedMagazine;
            if (_selectedAccessory) so.FindProperty("_lockedAccessory").objectReferenceValue = _selectedAccessory;

            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ItemRegistry.Instance?.RegisterPreset(preset);

            EditorGUIUtility.PingObject(preset);
            Selection.activeObject = preset;

            EditorUtility.DisplayDialog("Dedicated Preset Exported!",
                $"Preset '{defaultName}' successfully created at:\n{assetPath}\n\nSmartConsole Command:\ngive_preset preset_{cleanFileName.ToLower()}",
                "Awesome!");
        }

        private static void EnsureFolderHierarchy(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] elements = path.Split('/');
            string current = elements[0];
            for (int i = 1; i < elements.Length; i++)
            {
                string next = current + "/" + elements[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, elements[i]);
                }
                current = next;
            }
        }

        private static string GetDefaultSocketForPartType(WeaponPartType type) => type switch
        {
            WeaponPartType.Barrel => WeaponSocketNames.Barrel,
            WeaponPartType.Grip => WeaponSocketNames.Grip,
            WeaponPartType.Stock => WeaponSocketNames.Stock,
            WeaponPartType.Sight => WeaponSocketNames.Sight,
            WeaponPartType.Magazine => WeaponSocketNames.Magazine,
            WeaponPartType.Accessory => WeaponSocketNames.Accessory,
            _ => WeaponSocketNames.Barrel
        };

        // ================= PROCEDURAL ROLLS & DATA REFRESH =================

        private void OnCoreSelected()
        {
            UpdateSlotVisibility();

            if (_selectedCore != null)
            {
                RollProceduralWeapon();
            }
            else
            {
                RebuildPreviewModel();
                RefreshWeaponCard();
            }
        }

        private void UpdateSlotVisibility()
        {
            if (_selectedCore == null)
            {
                SetRowVisible(_barrelRow, true);
                SetRowVisible(_gripRow, true);
                SetRowVisible(_stockRow, true);
                SetRowVisible(_sightRow, true);
                SetRowVisible(_magRow, true);
                SetRowVisible(_accessoryRow, true);
                return;
            }

            SetRowVisible(_barrelRow, _selectedCore.SupportsPartSlot(WeaponPartType.Barrel));
            SetRowVisible(_gripRow, _selectedCore.SupportsPartSlot(WeaponPartType.Grip));
            SetRowVisible(_stockRow, _selectedCore.SupportsPartSlot(WeaponPartType.Stock));
            SetRowVisible(_sightRow, _selectedCore.SupportsPartSlot(WeaponPartType.Sight));
            SetRowVisible(_magRow, _selectedCore.SupportsPartSlot(WeaponPartType.Magazine));
            SetRowVisible(_accessoryRow, _selectedCore.SupportsPartSlot(WeaponPartType.Accessory));

            if (!_selectedCore.SupportsPartSlot(WeaponPartType.Barrel)) { _selectedBarrel = null; _barrelObjectField?.SetValueWithoutNotify(null); }
            if (!_selectedCore.SupportsPartSlot(WeaponPartType.Grip)) { _selectedGrip = null; _gripObjectField?.SetValueWithoutNotify(null); }
            if (!_selectedCore.SupportsPartSlot(WeaponPartType.Stock)) { _selectedStock = null; _stockObjectField?.SetValueWithoutNotify(null); }
            if (!_selectedCore.SupportsPartSlot(WeaponPartType.Sight)) { _selectedSight = null; _sightObjectField?.SetValueWithoutNotify(null); }
            if (!_selectedCore.SupportsPartSlot(WeaponPartType.Magazine)) { _selectedMagazine = null; _magObjectField?.SetValueWithoutNotify(null); }
            if (!_selectedCore.SupportsPartSlot(WeaponPartType.Accessory)) { _selectedAccessory = null; _accessoryObjectField?.SetValueWithoutNotify(null); }
        }

        private static void SetRowVisible(VisualElement row, bool visible)
        {
            if (row != null) row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RollProceduralWeapon()
        {
            if (!_selectedCore) return;

            _proceduralSeed = (uint)UnityEngine.Random.Range(1, 999999);
            System.Random prng = new System.Random((int)_proceduralSeed);

            _selectedBarrel = _selectedCore.SupportsPartSlot(WeaponPartType.Barrel) ? RollFromPool(_selectedCore.CompatibleBarrels, prng) : null;
            _selectedGrip = _selectedCore.SupportsPartSlot(WeaponPartType.Grip) ? RollFromPool(_selectedCore.CompatibleGrips, prng) : null;
            _selectedStock = _selectedCore.SupportsPartSlot(WeaponPartType.Stock) ? RollFromPool(_selectedCore.CompatibleStocks, prng) : null;
            _selectedSight = _selectedCore.SupportsPartSlot(WeaponPartType.Sight) ? RollFromPool(_selectedCore.CompatibleSights, prng) : null;
            _selectedMagazine = _selectedCore.SupportsPartSlot(WeaponPartType.Magazine) ? RollFromPool(_selectedCore.CompatibleMagazines, prng) : null;

            _selectedAccessory = (_selectedCore.SupportsPartSlot(WeaponPartType.Accessory) && _proceduralRarity >= ItemRarity.Rare && _selectedCore.CompatibleAccessories.Count > 0)
                ? RollFromPool(_selectedCore.CompatibleAccessories, prng)
                : null;

            UpdateFieldValues();
            RebuildPreviewModel();
            RefreshWeaponCard();
        }

        private void UpdateFieldValues()
        {
            _barrelObjectField?.SetValueWithoutNotify(_selectedBarrel);
            _gripObjectField?.SetValueWithoutNotify(_selectedGrip);
            _stockObjectField?.SetValueWithoutNotify(_selectedStock);
            _sightObjectField?.SetValueWithoutNotify(_selectedSight);
            _magObjectField?.SetValueWithoutNotify(_selectedMagazine);
            _accessoryObjectField?.SetValueWithoutNotify(_selectedAccessory);
        }

        private static WeaponPartDefinition RollFromPool(IReadOnlyList<WeaponPartDefinition> pool, System.Random prng)
        {
            if (pool == null || pool.Count == 0) return null;
            return pool[prng.Next(0, pool.Count)];
        }

        // ================= 3D VIEWPORT RENDERING =================

        private void RebuildPreviewModel()
        {
            DestroyPreviewRoot();

            if (_selectedCore == null)
            {
                Render3DPreview();
                return;
            }

            _previewRootObject = new GameObject("Preview_Weapon_Root");
            _previewRootObject.hideFlags = HideFlags.HideAndDontSave;

            var assembler = _previewRootObject.AddComponent<WeaponVisualAssembler>();
            var weapon = GetCurrentWeaponInstance();
            if (weapon != null)
            {
                assembler.AssembleVisuals(weapon);
            }

            // Fallback placeholder if no meshes exist
            if (_previewRootObject.transform.childCount == 0)
            {
                var placeholderCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                placeholderCube.hideFlags = HideFlags.HideAndDontSave;
                var col = placeholderCube.GetComponent<Collider>();
                if (col) DestroyImmediate(col);

                placeholderCube.transform.SetParent(_previewRootObject.transform, false);
                placeholderCube.transform.localPosition = Vector3.zero;
                placeholderCube.transform.localScale = new Vector3(0.12f, 0.22f, 0.6f);
            }

            InitializePreviewUtility();
            if (_previewUtility != null)
            {
                _previewUtility.AddSingleGO(_previewRootObject);
            }

            Render3DPreview();
        }

        private void Render3DPreview()
        {
            if (_previewImage == null || _previewUtility == null) return;

            _previewUtility.BeginPreview(FixedPreviewRect, GUIStyle.none);

            if (_previewRootObject != null)
            {
                _previewRootObject.transform.position = Vector3.zero;
                _previewRootObject.transform.rotation = Quaternion.Euler(_previewRotX, _previewRotY, 0f);
            }

            var cam = _previewUtility.camera;
            cam.transform.position = Vector3.back * _previewZoom;
            cam.transform.rotation = Quaternion.identity;

            cam.Render();
            Texture previewTexture = _previewUtility.EndPreview();
            if (previewTexture != null)
            {
                _previewImage.image = previewTexture;
                _previewImage.MarkDirtyRepaint();
            }
        }

        private WeaponInstance GetCurrentWeaponInstance()
        {
            if (!_selectedCore) return null;

            var parts = new List<WeaponPartDefinition>();
            if (_selectedBarrel) parts.Add(_selectedBarrel);
            if (_selectedGrip) parts.Add(_selectedGrip);
            if (_selectedStock) parts.Add(_selectedStock);
            if (_selectedSight) parts.Add(_selectedSight);
            if (_selectedMagazine) parts.Add(_selectedMagazine);
            if (_selectedAccessory) parts.Add(_selectedAccessory);

            return new WeaponInstance(_selectedCore, parts, _proceduralSeed, _itemLevel, _proceduralRarity);
        }

        private void RefreshWeaponCard()
        {
            if (_cardRoot == null) return;

            if (!_selectedCore)
            {
                _weaponNameLabel.text = "No Weapon Core Selected";
                _weaponNameLabel.style.color = Color.gray;
                _rarityLabel.text = "Select or create a core on the left to begin.";
                _redTextLabel.text = string.Empty;
                _cardRoot.style.borderTopColor = Color.gray;
                _cardRoot.style.borderBottomColor = Color.gray;
                _cardRoot.style.borderLeftColor = Color.gray;
                _cardRoot.style.borderRightColor = Color.gray;
                _statsContainer.Clear();
                _dpsContainer.Clear();
                return;
            }

            var weapon = GetCurrentWeaponInstance();
            if (weapon == null) return;

            Color rarityColor = RarityUtility.GetColor(_proceduralRarity);
            _cardRoot.style.borderTopColor = rarityColor;
            _cardRoot.style.borderBottomColor = rarityColor;
            _cardRoot.style.borderLeftColor = rarityColor;
            _cardRoot.style.borderRightColor = rarityColor;

            _weaponNameLabel.text = weapon.DisplayName;
            _weaponNameLabel.style.color = Color.white;

            _rarityLabel.text = $"LEVEL {_itemLevel} {_proceduralRarity.ToString().ToUpper()} {weapon.Core.WeaponType.ToString().ToUpper()}";
            _rarityLabel.style.color = rarityColor;

            _redTextLabel.text = !string.IsNullOrEmpty(weapon.RedText) ? $"\"{weapon.RedText}\"" : string.Empty;

            // Render stats list
            _statsContainer.Clear();
            var stats = weapon.CompiledStats;

            float totalDamagePerShot = stats.Damage * stats.PelletsPerShot;
            string damageDisplay = stats.PelletsPerShot > 1 ? $"{stats.Damage:F1} x {stats.PelletsPerShot} ({totalDamagePerShot:F1})" : $"{stats.Damage:F1}";

            AddStatRow(_statsContainer, "Damage", damageDisplay);
            AddStatRow(_statsContainer, "Fire Rate", $"{stats.FireRate:F1} /s");
            AddStatRow(_statsContainer, "Magazine Size", $"{stats.MagazineSize}");
            AddStatRow(_statsContainer, "Reload Speed", $"{stats.ReloadSpeed:F2} s");
            AddStatRow(_statsContainer, "Accuracy / Spread", $"{stats.BaseSpread:F1}° - {stats.MaxSpread:F1}°");
            AddStatRow(_statsContainer, "Recoil Kick", $"{stats.RecoilKick:F1}");
            AddStatRow(_statsContainer, "Critical Multiplier", $"{stats.CriticalMultiplier:F1}x");
            AddStatRow(_statsContainer, "Element", $"{stats.Element}");
            AddStatRow(_statsContainer, "Firing Mode", $"{stats.FiringMode}" + (stats.FiringMode == FiringMode.Burst ? $" ({stats.BurstCount} rnd)" : ""));

            // DPS Analytics Section
            _dpsContainer.Clear();
            float burstDPS = totalDamagePerShot * stats.FireRate;
            float magEmptyTime = stats.FireRate > 0f ? (stats.MagazineSize / stats.FireRate) : 0f;
            float cycleTime = magEmptyTime + stats.ReloadSpeed;
            float sustainedDPS = cycleTime > 0f ? (totalDamagePerShot * stats.MagazineSize) / cycleTime : 0f;

            AddStatRow(_dpsContainer, "Burst DPS", $"{burstDPS:F1} dps", new Color(0.95f, 0.8f, 0.3f));
            AddStatRow(_dpsContainer, "Sustained DPS", $"{sustainedDPS:F1} dps", new Color(0.4f, 0.9f, 0.4f));
        }

        private void AddStatRow(VisualElement container, string labelText, string valueText, Color? valueColor = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 3;

            var label = new Label(labelText);
            label.style.color = new Color(0.7f, 0.7f, 0.7f);

            var val = new Label(valueText);
            val.style.unityFontStyleAndWeight = FontStyle.Bold;
            val.style.color = valueColor ?? Color.white;

            row.Add(label);
            row.Add(val);
            container.Add(row);
        }
    }
}
