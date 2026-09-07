using System;
using System.Collections.Generic;
using Project.Combat;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Combat.Editor
{
    /// <summary>
    /// Weapon Designer 2.0: Modern, lightning-fast UI Toolkit editor window.
    /// Features:
    /// 1. Gunsmith Assembler with physical part-level scaling and signature locking.
    /// 2. Interactive 3D Weapon Viewport with real-time mesh hot-swapping.
    /// 3. Workbench Station Simulator (Weapon A vs Weapon B swap test).
    /// 4. Circuit Synergies visualizer.
    /// 5. Master-Detail Asset Catalog with zero-lag InspectorElement data binding.
    /// </summary>
    public class WeaponDesignerWindow : EditorWindow
    {
        [MenuItem("Tools/Combat/Weapon Designer")]
        [MenuItem("Window/Combat/Weapon Designer")]
        public static void Open()
        {
            var window = GetWindow<WeaponDesignerWindow>();
            window.titleContent = new GUIContent("Weapon Designer", EditorGUIUtility.IconContent("CustomTool").image);
            window.minSize = new Vector2(980, 720);
        }

        private enum WindowTab { Assembler, WorkbenchSimulator, CircuitSynergies, AssetCatalog }
        private WindowTab _currentTab = WindowTab.Assembler;

        // Active Assembled Weapon State
        private WeaponCoreDefinition _selectedCore;
        private readonly Dictionary<WeaponPartType, WeaponPartDefinition> _selectedParts = new();
        private readonly Dictionary<WeaponPartType, byte> _partLevels = new();
        private readonly Dictionary<WeaponPartType, bool> _partLocks = new();
        private byte _receiverLevel = 1;
        private ItemRarity _activeRarity = ItemRarity.Rare;
        private uint _activeSeed = 1337;

        // Workbench Simulator State (Weapon A vs Weapon B)
        private WeaponInstance _benchWeaponA;
        private WeaponInstance _benchWeaponB;
        private WeaponCoreDefinition _benchCoreA;
        private WeaponCoreDefinition _benchCoreB;
        private VisualElement _benchComparisonContainer;

        // Asset Catalog State
        private string _catalogSearchQuery = string.Empty;
        private string _catalogFilterType = "All";
        private UnityEngine.Object _inspectedAsset;
        private VisualElement _catalogListContainer;
        private VisualElement _catalogInspectorContainer;

        // 3D Viewport State
        private PreviewRenderUtility _previewUtility;
        private Image _previewImage;
        private float _previewRotX = 15f;
        private float _previewRotY = -45f;
        private float _previewZoom = 1.35f;
        private bool _isDragging;
        private Vector2 _lastPointerPos;
        private GameObject _previewRootObject;
        private static readonly Rect FixedPreviewRect = new Rect(0, 0, 512, 280);

        // UI Root References
        private VisualElement _tabAssemblerContent;
        private VisualElement _tabWorkbenchContent;
        private VisualElement _tabCircuitContent;
        private VisualElement _tabCatalogContent;
        private Button _tabAssemblerBtn;
        private Button _tabWorkbenchBtn;
        private Button _tabCircuitBtn;
        private Button _tabCatalogBtn;

        // Assembler Slot Field UI References
        private readonly Dictionary<WeaponPartType, ObjectField> _slotObjectFields = new();
        private readonly Dictionary<WeaponPartType, IntegerField> _slotLevelFields = new();
        private readonly Dictionary<WeaponPartType, Label> _slotLockLabels = new();
        private readonly Dictionary<WeaponPartType, VisualElement> _slotRows = new();

        // Live Weapon Card UI References
        private VisualElement _cardRoot;
        private Label _weaponNameLabel;
        private Label _rarityBadge;
        private Label _redTextLabel;
        private VisualElement _statsList;
        private VisualElement _dpsList;

        private void OnEnable()
        {
            InitializePreviewUtility();
            InitDefaultSlotLevels();
            ItemRegistry.OnRegistryUpdated += HandleRegistryUpdated;
        }

        private void OnDisable()
        {
            ItemRegistry.OnRegistryUpdated -= HandleRegistryUpdated;
            CleanupPreviewUtility();
        }

        private void HandleRegistryUpdated()
        {
            if (_catalogListContainer != null)
            {
                BuildCatalogList();
            }
        }

        private void InitDefaultSlotLevels()
        {
            foreach (WeaponPartType type in Enum.GetValues(typeof(WeaponPartType)))
            {
                if (type == WeaponPartType.Receiver) continue;
                if (!_partLevels.ContainsKey(type)) _partLevels[type] = 1;
                if (!_partLocks.ContainsKey(type)) _partLocks[type] = false;
            }
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = new Color(0.14f, 0.14f, 0.16f);

            // 1. Top Navigation Toolbar
            var topToolbar = CreateTopToolbar();
            root.Add(topToolbar);

            // 2. Main Content Body
            var body = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row } };

            _tabAssemblerContent = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row } };
            _tabWorkbenchContent = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Column } };
            _tabCircuitContent = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Column } };
            _tabCatalogContent = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row } };

            BuildAssemblerTab(_tabAssemblerContent);
            BuildWorkbenchTab(_tabWorkbenchContent);
            BuildCircuitTab(_tabCircuitContent);
            BuildCatalogTab(_tabCatalogContent);

            body.Add(_tabAssemblerContent);
            body.Add(_tabWorkbenchContent);
            body.Add(_tabCircuitContent);
            body.Add(_tabCatalogContent);

            root.Add(body);

            SwitchTab(WindowTab.Assembler);
            RefreshWeaponCard();
        }

        // ================= NAVIGATION TOOLBAR =================

        private VisualElement CreateTopToolbar()
        {
            var bar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.SpaceBetween,
                    backgroundColor = new Color(0.10f, 0.10f, 0.12f),
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.24f, 0.24f, 0.28f),
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 6,
                    paddingBottom = 6
                }
            };

            // Left: Title Brand
            var brandBox = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var title = new Label("GUNSMITH CIRCUIT") { style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.95f, 0.85f, 0.35f) } };
            var ver = new Label(" v2.0") { style = { fontSize = 10, color = Color.gray, marginRight = 16 } };
            brandBox.Add(title);
            brandBox.Add(ver);
            bar.Add(brandBox);

            // Center: Navigation Tabs
            var tabsBox = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _tabAssemblerBtn = CreateNavButton("🛠️ Gunsmith & Roll", WindowTab.Assembler);
            _tabWorkbenchBtn = CreateNavButton("🔄 Workbench Simulator", WindowTab.WorkbenchSimulator);
            _tabCircuitBtn = CreateNavButton("⚡ Circuit Synergies", WindowTab.CircuitSynergies);
            _tabCatalogBtn = CreateNavButton("📚 Asset Catalog", WindowTab.AssetCatalog);

            tabsBox.Add(_tabAssemblerBtn);
            tabsBox.Add(_tabWorkbenchBtn);
            tabsBox.Add(_tabCircuitBtn);
            tabsBox.Add(_tabCatalogBtn);
            bar.Add(tabsBox);

            // Right: Fast Actions
            var actionBox = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var scanBtn = new Button(() =>
            {
                var reg = ItemRegistry.Instance;
                if (reg)
                {
                    reg.ScanProjectAssets();
                    BuildCatalogList();
                    EditorUtility.DisplayDialog("Registry Scan", $"Item Registry scanned successfully!\n{reg.WeaponCores.Count} Cores, {reg.WeaponParts.Count} Parts, {reg.WeaponPresets.Count} Presets, {reg.Manufacturers.Count} Manufacturers.", "OK");
                }
            }) { text = "🔄 Re-scan Project" };
            scanBtn.style.height = 24;
            scanBtn.style.fontSize = 11;
            actionBox.Add(scanBtn);
            bar.Add(actionBox);

            return bar;
        }

        private Button CreateNavButton(string text, WindowTab tab)
        {
            var btn = new Button(() => SwitchTab(tab)) { text = text };
            btn.style.height = 28;
            btn.style.paddingLeft = 14;
            btn.style.paddingRight = 14;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.fontSize = 12;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.marginLeft = 2;
            btn.style.marginRight = 2;
            return btn;
        }

        private void SwitchTab(WindowTab tab)
        {
            _currentTab = tab;

            _tabAssemblerBtn.style.backgroundColor = tab == WindowTab.Assembler ? new Color(0.20f, 0.42f, 0.72f) : new Color(0.18f, 0.18f, 0.20f);
            _tabWorkbenchBtn.style.backgroundColor = tab == WindowTab.WorkbenchSimulator ? new Color(0.20f, 0.42f, 0.72f) : new Color(0.18f, 0.18f, 0.20f);
            _tabCircuitBtn.style.backgroundColor = tab == WindowTab.CircuitSynergies ? new Color(0.20f, 0.42f, 0.72f) : new Color(0.18f, 0.18f, 0.20f);
            _tabCatalogBtn.style.backgroundColor = tab == WindowTab.AssetCatalog ? new Color(0.20f, 0.42f, 0.72f) : new Color(0.18f, 0.18f, 0.20f);

            _tabAssemblerContent.style.display = tab == WindowTab.Assembler ? DisplayStyle.Flex : DisplayStyle.None;
            _tabWorkbenchContent.style.display = tab == WindowTab.WorkbenchSimulator ? DisplayStyle.Flex : DisplayStyle.None;
            _tabCircuitContent.style.display = tab == WindowTab.CircuitSynergies ? DisplayStyle.Flex : DisplayStyle.None;
            _tabCatalogContent.style.display = tab == WindowTab.AssetCatalog ? DisplayStyle.Flex : DisplayStyle.None;

            if (tab == WindowTab.Assembler)
            {
                RebuildPreviewModel();
                RefreshWeaponCard();
            }
            else if (tab == WindowTab.WorkbenchSimulator)
            {
                RefreshWorkbenchUI();
            }
            else if (tab == WindowTab.CircuitSynergies)
            {
                RefreshCircuitView();
            }
            else if (tab == WindowTab.AssetCatalog)
            {
                BuildCatalogList();
            }
        }

        // ================= TAB 1: GUNSMITH ASSEMBLER =================

        private void BuildAssemblerTab(VisualElement container)
        {
            container.Clear();

            // Left Sub-Column: Slot Controls (Scrollable)
            var leftScroll = new ScrollView { style = { width = new Length(48, LengthUnit.Percent), paddingLeft = 12, paddingRight = 12, paddingTop = 12 } };

            // 1. Core Receiver Configuration
            var coreBox = CreateCardPanel("1. Receiver / Core Archetype");
            var coreRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var coreField = new ObjectField("Core Asset") { objectType = typeof(WeaponCoreDefinition), value = _selectedCore, style = { flexGrow = 1 } };
            coreField.RegisterValueChangedCallback(evt =>
            {
                _selectedCore = evt.newValue as WeaponCoreDefinition;
                OnCoreChanged();
            });
            coreRow.Add(coreField);

            var newCoreBtn = new Button(CreateNewCoreAsset) { text = "+" };
            newCoreBtn.tooltip = "Create a new WeaponCoreDefinition ScriptableObject";
            newCoreBtn.style.width = 26;
            newCoreBtn.style.height = 24;
            newCoreBtn.style.marginLeft = 4;
            newCoreBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            coreRow.Add(newCoreBtn);
            coreBox.Add(coreRow);

            var recLvlRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginTop = 6 } };
            var recLvlSlider = new SliderInt("Receiver Level", 1, 100) { value = _receiverLevel, showInputField = true, style = { flexGrow = 1 } };
            recLvlSlider.RegisterValueChangedCallback(evt =>
            {
                _receiverLevel = (byte)evt.newValue;
                RefreshWeaponCard();
            });
            recLvlRow.Add(recLvlSlider);
            coreBox.Add(recLvlRow);

            leftScroll.Add(coreBox);

            // 2. Physical Part Sockets (With Individual Levels & Locks)
            var partsBox = CreateCardPanel("2. Physical Part Sockets");
            var infoLabel = new Label("Barrel & Receiver levels drive base damage. Signature parts on Legendaries cannot be detached.")
            {
                style = { fontSize = 10, color = Color.gray, marginBottom = 8, whiteSpace = WhiteSpace.Normal }
            };
            partsBox.Add(infoLabel);

            BuildSlotRow(partsBox, WeaponPartType.Barrel);
            BuildSlotRow(partsBox, WeaponPartType.Grip);
            BuildSlotRow(partsBox, WeaponPartType.Stock);
            BuildSlotRow(partsBox, WeaponPartType.Sight);
            BuildSlotRow(partsBox, WeaponPartType.Magazine);
            BuildSlotRow(partsBox, WeaponPartType.Accessory);

            leftScroll.Add(partsBox);

            // 3. Roll & Dedicated Preset Exporter
            var rollBox = CreateCardPanel("3. Procedural Roll & Preset Exporter");
            var rollRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 6 } };

            var rarityField = new EnumField("Target Rarity", _activeRarity) { style = { flexGrow = 1 } };
            rarityField.RegisterValueChangedCallback(evt =>
            {
                _activeRarity = (ItemRarity)evt.newValue;
                RefreshWeaponCard();
            });
            rollRow.Add(rarityField);

            var rollBtn = new Button(RollRandomWeapon) { text = "🎲 Roll Random Parts" };
            rollBtn.style.height = 26;
            rollBtn.style.marginLeft = 8;
            rollBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            rollBtn.style.backgroundColor = new Color(0.20f, 0.48f, 0.80f);
            rollRow.Add(rollBtn);
            rollBox.Add(rollRow);

            var exportPresetBtn = new Button(ExportCurrentAsDedicatedPreset)
            {
                text = "💾 Export As Dedicated Boss Drop Preset"
            };
            exportPresetBtn.style.height = 28;
            exportPresetBtn.style.backgroundColor = new Color(0.65f, 0.38f, 0.12f);
            exportPresetBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            exportPresetBtn.tooltip = "Saves this weapon as a dedicated preset with its signature components permanently locked.";
            rollBox.Add(exportPresetBtn);

            leftScroll.Add(rollBox);
            container.Add(leftScroll);

            // Right Sub-Column: 3D Viewport & Live Weapon Card
            var rightScroll = new ScrollView { style = { width = new Length(52, LengthUnit.Percent), paddingLeft = 12, paddingRight = 12, paddingTop = 12 } };

            // 3D Viewport
            var vpCard = CreateCardPanel("3D REAL-TIME VIEWPORT");
            var vpTopBar = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 4 } };
            var vpHint = new Label("Left-Drag: Orbit | Scroll: Zoom") { style = { fontSize = 10, color = Color.gray } };
            var resetCamBtn = new Button(() =>
            {
                _previewRotX = 15f;
                _previewRotY = -45f;
                _previewZoom = 1.35f;
                Render3DPreview();
            }) { text = "↺ Reset Camera" };
            resetCamBtn.style.height = 18;
            resetCamBtn.style.fontSize = 10;
            vpTopBar.Add(vpHint);
            vpTopBar.Add(resetCamBtn);
            vpCard.Add(vpTopBar);

            _previewImage = new Image
            {
                style =
                {
                    height = 250,
                    backgroundColor = new Color(0.10f, 0.10f, 0.12f),
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    marginBottom = 8
                }
            };

            SetupViewportDragControls(_previewImage);
            vpCard.Add(_previewImage);
            rightScroll.Add(vpCard);

            // Live Weapon Card
            BuildWeaponCardUI(rightScroll);

            container.Add(rightScroll);
            UpdateSlotVisibility();
        }

        private void BuildSlotRow(VisualElement parent, WeaponPartType slot)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 6,
                    backgroundColor = new Color(0.11f, 0.11f, 0.13f),
                    paddingLeft = 8,
                    paddingRight = 6,
                    paddingTop = 4,
                    paddingBottom = 4,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };

            _slotRows[slot] = row;

            var field = new ObjectField(slot.ToString())
            {
                objectType = typeof(WeaponPartDefinition),
                value = _selectedParts.TryGetValue(slot, out var p) ? p : null,
                style = { flexGrow = 1, minWidth = 140 }
            };

            field.RegisterValueChangedCallback(evt =>
            {
                var part = evt.newValue as WeaponPartDefinition;
                _selectedParts[slot] = part;
                if (part != null && part.IsUnique) _partLocks[slot] = true;
                UpdateSlotLockLabel(slot);
                RebuildPreviewModel();
                RefreshWeaponCard();
            });
            _slotObjectFields[slot] = field;
            row.Add(field);

            // Level field for this part
            var lvlField = new IntegerField("Lvl")
            {
                value = _partLevels.TryGetValue(slot, out byte l) ? l : 1,
                style = { width = 75, marginLeft = 4 }
            };
            lvlField.RegisterValueChangedCallback(evt =>
            {
                _partLevels[slot] = (byte)Mathf.Clamp(evt.newValue, 1, 100);
                RefreshWeaponCard();
            });
            _slotLevelFields[slot] = lvlField;
            row.Add(lvlField);

            // Lock / Unlock Indicator
            var lockLabel = new Label("🔓")
            {
                tooltip = "Lock state. Signature components on Legendaries are locked (🔒) and cannot be removed.",
                style = { fontSize = 14, marginLeft = 6, marginRight = 4, cursor = StyleKeyword.Auto }
            };
            _slotLockLabels[slot] = lockLabel;
            row.Add(lockLabel);

            // Quick Create Part Button
            var createPartBtn = new Button(() => CreateNewPartAsset(slot)) { text = "+" };
            createPartBtn.tooltip = $"Create new {slot} definition";
            createPartBtn.style.width = 22;
            createPartBtn.style.height = 22;
            createPartBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(createPartBtn);

            parent.Add(row);
            UpdateSlotLockLabel(slot);
        }

        private void UpdateSlotLockLabel(WeaponPartType slot)
        {
            if (!_slotLockLabels.TryGetValue(slot, out var label)) return;

            bool isLocked = _partLocks.TryGetValue(slot, out bool l) && l;
            if (_selectedParts.TryGetValue(slot, out var p) && p != null && p.IsUnique)
            {
                isLocked = true;
                _partLocks[slot] = true;
            }

            label.text = isLocked ? "🔒" : "🔓";
            label.style.color = isLocked ? new Color(1f, 0.4f, 0.4f) : new Color(0.5f, 0.9f, 0.5f);
        }

        private void OnCoreChanged()
        {
            UpdateSlotVisibility();
            RebuildPreviewModel();
            RefreshWeaponCard();
        }

        private void UpdateSlotVisibility()
        {
            if (!_selectedCore) return;

            foreach (var kvp in _slotRows)
            {
                bool supported = _selectedCore.SupportsPartSlot(kvp.Key);
                kvp.Value.style.display = supported ? DisplayStyle.Flex : DisplayStyle.None;
                if (!supported)
                {
                    _selectedParts[kvp.Key] = null;
                    if (_slotObjectFields.TryGetValue(kvp.Key, out var field))
                        field.SetValueWithoutNotify(null);
                }
            }
        }

        private void RollRandomWeapon()
        {
            if (!_selectedCore) return;

            _activeSeed = (uint)UnityEngine.Random.Range(1, 999999);
            var weapon = ItemRegistry.Instance
                ? ItemRegistry.Instance.GenerateWeapon(_selectedCore, _activeSeed, _receiverLevel, _activeRarity)
                : null;

            if (weapon == null) return;

            foreach (WeaponPartType slot in Enum.GetValues(typeof(WeaponPartType)))
            {
                if (slot == WeaponPartType.Receiver) continue;

                if (weapon.TryGetPart(slot, out var inst))
                {
                    _selectedParts[slot] = inst.Definition;
                    _partLevels[slot] = inst.Level;
                    _partLocks[slot] = inst.IsLocked;

                    if (_slotObjectFields.TryGetValue(slot, out var f)) f.SetValueWithoutNotify(inst.Definition);
                    if (_slotLevelFields.TryGetValue(slot, out var lf)) lf.SetValueWithoutNotify(inst.Level);
                }
                else
                {
                    _selectedParts[slot] = null;
                    _partLocks[slot] = false;
                    if (_slotObjectFields.TryGetValue(slot, out var f)) f.SetValueWithoutNotify(null);
                }
                UpdateSlotLockLabel(slot);
            }

            RebuildPreviewModel();
            RefreshWeaponCard();
        }

        private void ExportCurrentAsDedicatedPreset()
        {
            if (!_selectedCore)
            {
                EditorUtility.DisplayDialog("Export Preset", "Please select a Weapon Core first!", "OK");
                return;
            }

            string defaultPresetName = $"PRESET_{_selectedCore.WeaponType}_Legendary_{_selectedCore.BaseWeaponName.Replace(" ", "_")}";
            string folder = "Assets/_Project/Data/Items/Weapons/Presets";
            EnsureDirectoryExists(folder);
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Dedicated Drop Preset",
                defaultPresetName,
                "asset",
                "Select where to save the weapon preset",
                folder);

            if (string.IsNullOrEmpty(path)) return;

            var preset = CreateInstance<WeaponPresetDefinition>();

            // Lock signature parts
            var so = new SerializedObject(preset);
            string fileBase = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
            so.FindProperty("_presetId").stringValue = fileBase.StartsWith("preset_") ? fileBase : $"preset_{fileBase}";
            so.FindProperty("_presetDisplayName").stringValue = $"Named {_selectedCore.BaseWeaponName}";
            so.FindProperty("_core").objectReferenceValue = _selectedCore;
            so.FindProperty("_rarity").enumValueIndex = (int)ItemRarity.Legendary;

            if (_selectedParts.TryGetValue(WeaponPartType.Barrel, out var b) && b) so.FindProperty("_lockedBarrel").objectReferenceValue = b;
            if (_selectedParts.TryGetValue(WeaponPartType.Grip, out var g) && g) so.FindProperty("_lockedGrip").objectReferenceValue = g;
            if (_selectedParts.TryGetValue(WeaponPartType.Stock, out var st) && st) so.FindProperty("_lockedStock").objectReferenceValue = st;
            if (_selectedParts.TryGetValue(WeaponPartType.Sight, out var si) && si) so.FindProperty("_lockedSight").objectReferenceValue = si;
            if (_selectedParts.TryGetValue(WeaponPartType.Magazine, out var m) && m) so.FindProperty("_lockedMagazine").objectReferenceValue = m;
            if (_selectedParts.TryGetValue(WeaponPartType.Accessory, out var a) && a) so.FindProperty("_lockedAccessory").objectReferenceValue = a;

            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();

            if (ItemRegistry.Instance) ItemRegistry.Instance.RegisterPreset(preset);

            EditorUtility.FocusProjectWindow(); 
            Selection.activeObject = preset;
            EditorUtility.DisplayDialog("Export Preset", $"Saved dedicated drop preset successfully at:\n{path}", "OK");
        }

        // ================= TAB 2: WORKBENCH SIMULATOR =================

        private void BuildWorkbenchTab(VisualElement container)
        {
            container.Clear();

            var topBar = CreateCardPanel("Workbench Station Simulator (Dual Weapon Side-by-Side)");
            var desc = new Label("Simulates the in-game extraction workbench. Select two weapons, inspect compatible slots, and transfer parts. Signature parts on Legendaries cannot be detached.")
            {
                style = { fontSize = 11, color = Color.gray, marginBottom = 8, whiteSpace = WhiteSpace.Normal }
            };
            topBar.Add(desc);

            var selectRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };

            // Left selector (Weapon A)
            var aBox = new VisualElement { style = { flexGrow = 1, marginRight = 8 } };
            var coreAField = new ObjectField("Weapon A Core") { objectType = typeof(WeaponCoreDefinition), value = _benchCoreA };
            coreAField.RegisterValueChangedCallback(evt =>
            {
                _benchCoreA = evt.newValue as WeaponCoreDefinition;
                if (_benchCoreA) _benchWeaponA = ItemRegistry.Instance ? ItemRegistry.Instance.GenerateWeapon(_benchCoreA, 111, 10, ItemRarity.Legendary) : new WeaponInstance(_benchCoreA, seed: 111, level: 10, rarity: ItemRarity.Legendary);
                RefreshWorkbenchUI();
            });
            aBox.Add(coreAField);
            selectRow.Add(aBox);

            // Right selector (Weapon B)
            var bBox = new VisualElement { style = { flexGrow = 1, marginLeft = 8 } };
            var coreBField = new ObjectField("Weapon B Core") { objectType = typeof(WeaponCoreDefinition), value = _benchCoreB };
            coreBField.RegisterValueChangedCallback(evt =>
            {
                _benchCoreB = evt.newValue as WeaponCoreDefinition;
                if (_benchCoreB) _benchWeaponB = ItemRegistry.Instance ? ItemRegistry.Instance.GenerateWeapon(_benchCoreB, 222, 12, ItemRarity.Rare) : new WeaponInstance(_benchCoreB, seed: 222, level: 12, rarity: ItemRarity.Rare);
                RefreshWorkbenchUI();
            });
            bBox.Add(coreBField);
            selectRow.Add(bBox);

            topBar.Add(selectRow);
            container.Add(topBar);

            _benchComparisonContainer = new ScrollView { style = { flexGrow = 1, paddingLeft = 12, paddingRight = 12, paddingTop = 8 } };
            container.Add(_benchComparisonContainer);
        }

        private void RefreshWorkbenchUI()
        {
            if (_benchComparisonContainer == null) return;
            _benchComparisonContainer.Clear();

            if (_benchWeaponA == null || _benchWeaponB == null)
            {
                var prompt = new Label("Select both Weapon A and Weapon B above to begin workbench part swapping simulation.")
                {
                    style = { color = new Color(0.9f, 0.7f, 0.2f), fontSize = 12, paddingTop = 20, alignSelf = Align.Center }
                };
                _benchComparisonContainer.Add(prompt);
                return;
            }

            // Summary Header
            var summaryRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 10 } };
            summaryRow.Add(CreateWeaponMiniSummary("WEAPON A (Player Primary)", _benchWeaponA));
            summaryRow.Add(CreateWeaponMiniSummary("WEAPON B (Scavenged Drop)", _benchWeaponB));
            _benchComparisonContainer.Add(summaryRow);

            // Slot Comparison Rows
            var slotsPanel = CreateCardPanel("Modular Part Slots & Transfer Actions");

            foreach (WeaponPartType slot in Enum.GetValues(typeof(WeaponPartType)))
            {
                if (slot == WeaponPartType.Receiver) continue;
                BuildBenchSlotRow(slotsPanel, slot);
            }

            _benchComparisonContainer.Add(slotsPanel);
        }

        private VisualElement CreateWeaponMiniSummary(string title, WeaponInstance weapon)
        {
            var box = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    backgroundColor = new Color(0.11f, 0.11f, 0.13f),
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 8,
                    paddingBottom = 8,
                    borderLeftWidth = 3,
                    borderLeftColor = RarityUtility.GetColor(weapon.Rarity),
                    marginLeft = 4,
                    marginRight = 4
                }
            };

            var t = new Label(title) { style = { fontSize = 10, color = Color.gray } };
            var name = new Label(weapon.DisplayName) { style = { fontSize = 13, unityFontStyleAndWeight = FontStyle.Bold, color = Color.white } };
            var stat = new Label($"Lvl {weapon.Level} • DPS {weapon.CompiledStats.Damage * weapon.CompiledStats.PelletsPerShot * (weapon.CompiledStats.FireRate / 60f):F0} • Mag {weapon.CompiledStats.MagazineSize}")
            {
                style = { fontSize = 11, color = new Color(0.8f, 0.8f, 0.8f) }
            };

            box.Add(t);
            box.Add(name);
            box.Add(stat);
            return box;
        }

        private void BuildBenchSlotRow(VisualElement parent, WeaponPartType slot)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.SpaceBetween,
                    backgroundColor = new Color(0.13f, 0.13f, 0.15f),
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 6,
                    paddingBottom = 6,
                    marginBottom = 4,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };

            // Part A
            bool hasA = _benchWeaponA.TryGetPart(slot, out var partA);
            string textA = hasA ? $"{partA.DisplayName} (Lvl {partA.Level})" : "[Empty]";
            if (hasA && partA.IsLocked) textA += " 🔒";

            var labelA = new Label(textA) { style = { width = new Length(35, LengthUnit.Percent), fontSize = 11, color = hasA ? (partA.IsLocked ? new Color(1f, 0.5f, 0.5f) : Color.white) : Color.gray } };
            row.Add(labelA);

            // Center Swap Buttons & Status
            var actionCenter = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var canTransfer = GunsmithBenchService.CanTransferOrSwap(_benchWeaponA, _benchWeaponB, slot);
            var swapBtn = new Button(() =>
            {
                var res = GunsmithBenchService.TryTransferOrSwap(_benchWeaponA, _benchWeaponB, slot);
                if (res != GunsmithSwapResult.Success)
                {
                    EditorUtility.DisplayDialog("Workbench Transfer", $"Cannot swap {slot}:\n{res}", "OK");
                }
                RefreshWorkbenchUI();
            }) { text = $"⇄ Swap {slot}" };

            swapBtn.style.height = 24;
            swapBtn.style.fontSize = 11;

            if (canTransfer == GunsmithSwapResult.Success)
            {
                swapBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f);
                swapBtn.tooltip = "Compatible! Tap to swap parts between Weapon A and Weapon B.";
            }
            else
            {
                swapBtn.style.backgroundColor = new Color(0.35f, 0.2f, 0.2f);
                swapBtn.tooltip = $"Transfer blocked: {canTransfer}";
            }

            actionCenter.Add(swapBtn);
            row.Add(actionCenter);

            // Part B
            bool hasB = _benchWeaponB.TryGetPart(slot, out var partB);
            string textB = hasB ? $"{partB.DisplayName} (Lvl {partB.Level})" : "[Empty]";
            if (hasB && partB.IsLocked) textB += " 🔒";

            var labelB = new Label(textB) { style = { width = new Length(35, LengthUnit.Percent), fontSize = 11, color = hasB ? (partB.IsLocked ? new Color(1f, 0.5f, 0.5f) : Color.white) : Color.gray, unityTextAlign = TextAnchor.MiddleRight } };
            row.Add(labelB);

            parent.Add(row);
        }

        // ================= TAB 3: CIRCUIT SYNERGIES =================

        private void BuildCircuitTab(VisualElement container)
        {
            container.Clear();

            var panel = CreateCardPanel("Active Weapon Circuit Synergies");
            var desc = new Label("Visualizes the event execution chain of the currently assembled weapon.")
            {
                style = { fontSize = 11, color = Color.gray, marginBottom = 12 }
            };
            panel.Add(desc);

            var weapon = GetCurrentWeaponInstance();
            if (weapon == null || !weapon.Core)
            {
                panel.Add(new Label("Select a Weapon Core in the Gunsmith Assembler tab to view its circuit logic.") { style = { color = Color.gray } });
                container.Add(panel);
                return;
            }

            var chainBox = new VisualElement { style = { flexDirection = FlexDirection.Column, marginBottom = 12 } };

            AddCircuitNode(chainBox, "1. TRIGGER GATE (Grip)", FormatCircuitPart(weapon, WeaponPartType.Grip, "Default Mechanical Trigger"), "Dictates WHEN current cycles (Click, Slide, Empty Mag)");
            AddCircuitNode(chainBox, "2. POWER CAPACITOR (Receiver)", $"{weapon.Core.BaseWeaponName} (Lvl {weapon.Level})", "Dictates wattage capacity, base damage, and cyclic rate");
            AddCircuitNode(chainBox, "3. DELIVERY CONDUIT (Barrel)", FormatCircuitPart(weapon, WeaponPartType.Barrel, "Standard Rifled Barrel"), "Dictates trajectory shaping (Hitscan, Multi-pellet, Accelerating, Split)");
            AddCircuitNode(chainBox, "4. PAYLOAD SUBROUTINE (Accessory)", FormatCircuitPart(weapon, WeaponPartType.Accessory, "Kinetic Chamber"), "Dictates elemental damage, status procs, and chain reactions");
            AddCircuitNode(chainBox, "5. FEEDBACK REBOUND (Stock)", FormatCircuitPart(weapon, WeaponPartType.Stock, "Counter-Weight Stock"), "Converts recoil energy into player speed or shield recharge");

            panel.Add(chainBox);
            container.Add(panel);
        }

        private static string FormatCircuitPart(WeaponInstance weapon, WeaponPartType slot, string fallback)
        {
            if (weapon.TryGetPart(slot, out var p))
            {
                var def = p.Definition;
                string text = $"{p.DisplayName} (Lvl {p.Level}){(p.IsLocked ? " [LOCKED]" : "")}";
                if (def.ProjectileBehaviors != null && def.ProjectileBehaviors.Count > 0)
                {
                    List<string> bNames = new();
                    for (int i = 0; i < def.ProjectileBehaviors.Count; i++)
                    {
                        if (def.ProjectileBehaviors[i]) bNames.Add(def.ProjectileBehaviors[i].DisplayName);
                    }
                    text += $" • ⚡ {string.Join(", ", bNames)}";
                }
                if (def.WeaponPerks != null && def.WeaponPerks.Count > 0)
                {
                    List<string> pNames = new();
                    for (int i = 0; i < def.WeaponPerks.Count; i++)
                    {
                        if (def.WeaponPerks[i]) pNames.Add(def.WeaponPerks[i].DisplayName);
                    }
                    text += $" • ✨ {string.Join(", ", pNames)}";
                }
                return text;
            }
            return fallback;
        }

        private void RefreshCircuitView()
        {
            BuildCircuitTab(_tabCircuitContent);
        }

        private void AddCircuitNode(VisualElement parent, string stage, string component, string description)
        {
            var node = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.12f, 0.12f, 0.14f),
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 8,
                    paddingBottom = 8,
                    marginBottom = 6,
                    borderLeftWidth = 4,
                    borderLeftColor = new Color(0.3f, 0.6f, 1.0f),
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };

            var sLabel = new Label(stage) { style = { fontSize = 10, color = new Color(0.4f, 0.8f, 1f), unityFontStyleAndWeight = FontStyle.Bold } };
            var cLabel = new Label(component) { style = { fontSize = 13, color = Color.white, unityFontStyleAndWeight = FontStyle.Bold } };
            var dLabel = new Label(description) { style = { fontSize = 10, color = Color.gray } };

            node.Add(sLabel);
            node.Add(cLabel);
            node.Add(dLabel);
            parent.Add(node);
        }

        // ================= TAB 4: ASSET CATALOG (MASTER-DETAIL) =================

        private void BuildCatalogTab(VisualElement container)
        {
            container.Clear();

            // Left: Master List
            var masterCol = new VisualElement
            {
                style =
                {
                    width = new Length(40, LengthUnit.Percent),
                    borderRightWidth = 1,
                    borderRightColor = new Color(0.25f, 0.25f, 0.28f),
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 10
                }
            };

            // Search Bar & Filter Bar
            var searchField = new ToolbarSearchField { value = _catalogSearchQuery, style = { marginBottom = 6 } };
            searchField.RegisterValueChangedCallback(evt =>
            {
                _catalogSearchQuery = evt.newValue;
                BuildCatalogList();
            });
            masterCol.Add(searchField);

            var filterBar = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginBottom = 8 } };
            string[] filters = { "All", "Cores", "Barrels", "Grips", "Stocks", "Sights", "Magazines", "Accessories", "Presets", "Brands", "Behaviors", "Perks", "Patterns" };
            foreach (var f in filters)
            {
                var btn = new Button(() => { _catalogFilterType = f; BuildCatalogList(); }) { text = f };
                btn.style.height = 20;
                btn.style.fontSize = 10;
                btn.style.paddingLeft = 6;
                btn.style.paddingRight = 6;
                btn.style.marginRight = 2;
                btn.style.marginBottom = 2;
                filterBar.Add(btn);
            }
            masterCol.Add(filterBar);

            _catalogListContainer = new ScrollView { style = { flexGrow = 1 } };
            masterCol.Add(_catalogListContainer);

            // Create Buttons Toolbar
            var createBar = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 6 } };
            var addCoreBtn = new Button(CreateNewCoreAsset) { text = "+ Core", style = { flexGrow = 1, fontSize = 10 } };
            var addPartBtn = new Button(() => CreateNewPartAsset(WeaponPartType.Barrel)) { text = "+ Part", style = { flexGrow = 1, fontSize = 10 } };
            var addBrandBtn = new Button(CreateNewBrandAsset) { text = "+ Brand", style = { flexGrow = 1, fontSize = 10 } };
            var addBehBtn = new Button(ShowNewBehaviorMenu) { text = "+ Behavior", style = { flexGrow = 1, fontSize = 10 } };
            var addPerkBtn = new Button(CreateNewPerkAsset) { text = "+ Perk", style = { flexGrow = 1, fontSize = 10 } };
            var addPatternBtn = new Button(ShowNewPatternMenu) { text = "+ Pattern", style = { flexGrow = 1, fontSize = 10 } };
            createBar.Add(addCoreBtn);
            createBar.Add(addPartBtn);
            createBar.Add(addBrandBtn);
            createBar.Add(addBehBtn);
            createBar.Add(addPerkBtn);
            createBar.Add(addPatternBtn);
            masterCol.Add(createBar);

            container.Add(masterCol);

            // Right: Detail Inspector
            _catalogInspectorContainer = new ScrollView
            {
                style =
                {
                    width = new Length(60, LengthUnit.Percent),
                    paddingLeft = 14,
                    paddingRight = 14,
                    paddingTop = 10
                }
            };
            container.Add(_catalogInspectorContainer);

            BuildCatalogList();
        }

        private void BuildCatalogList()
        {
            if (_catalogListContainer == null) return;
            _catalogListContainer.Clear();

            var reg = ItemRegistry.Instance;
            if (!reg)
            {
                _catalogListContainer.Add(new Label("ItemRegistry asset not found in Resources.") { style = { color = Color.red } });
                return;
            }

            // Cores
            if (_catalogFilterType == "All" || _catalogFilterType == "Cores")
            {
                foreach (var core in reg.WeaponCores)
                {
                    if (!core) continue;
                    if (MatchesSearch(core.BaseWeaponName, core.CoreId))
                        AddCatalogItem(core.BaseWeaponName, $"Core • {core.WeaponType}", core, new Color(0.4f, 0.7f, 1f));
                }
            }

            // Parts
            if (_catalogFilterType == "All" || (_catalogFilterType != "Presets" && _catalogFilterType != "Brands"))
            {
                foreach (var part in reg.WeaponParts)
                {
                    if (!part) continue;
                    if (_catalogFilterType != "All" && _catalogFilterType != "Cores" && _catalogFilterType != $"{part.PartType}s")
                        continue;

                    if (MatchesSearch(part.DisplayName, part.PartId))
                    {
                        Color c = RarityUtility.GetColor(part.Rarity);
                        string tag = $"{part.PartType} • {part.Rarity}" + (part.IsUnique ? " [UNIQUE]" : "");
                        AddCatalogItem(part.DisplayName, tag, part, c);
                    }
                }
            }

            // Presets
            if (_catalogFilterType == "All" || _catalogFilterType == "Presets")
            {
                foreach (var preset in reg.WeaponPresets)
                {
                    if (!preset) continue;
                    if (MatchesSearch(preset.PresetDisplayName, preset.PresetId))
                        AddCatalogItem(preset.PresetDisplayName, "Dedicated Drop Preset", preset, new Color(1f, 0.6f, 0.1f));
                }
            }

            // Brands / Manufacturers
            if (_catalogFilterType == "All" || _catalogFilterType == "Brands")
            {
                foreach (var mfg in reg.Manufacturers)
                {
                    if (!mfg) continue;
                    if (MatchesSearch(mfg.DisplayName, mfg.ManufacturerId))
                        AddCatalogItem(mfg.DisplayName, $"Manufacturer Brand • {mfg.ManufacturerId}", mfg, mfg.AccentColor != default ? mfg.AccentColor : new Color(0.9f, 0.4f, 0.4f));
                }
            }

            // Projectile Behaviors
            if (_catalogFilterType == "All" || _catalogFilterType == "Behaviors")
            {
                foreach (var beh in reg.ProjectileBehaviors)
                {
                    if (!beh) continue;
                    if (MatchesSearch(beh.DisplayName, beh.BehaviorId))
                        AddCatalogItem(beh.DisplayName, $"Behavior • {beh.GetType().Name.Replace("Definition", "")}", beh, new Color(0.2f, 0.85f, 0.95f));
                }
            }

            // Weapon Perks
            if (_catalogFilterType == "All" || _catalogFilterType == "Perks")
            {
                foreach (var perk in reg.WeaponPerks)
                {
                    if (!perk) continue;
                    if (MatchesSearch(perk.DisplayName, perk.PerkId))
                        AddCatalogItem(perk.DisplayName, $"Perk • {perk.GetType().Name.Replace("Definition", "")}", perk, new Color(0.95f, 0.85f, 0.25f));
                }
            }

            // Pellet Patterns
            if (_catalogFilterType == "All" || _catalogFilterType == "Patterns")
            {
                foreach (var pat in reg.PelletPatterns)
                {
                    if (!pat) continue;
                    if (MatchesSearch(pat.DisplayName, pat.PatternId))
                        AddCatalogItem(pat.DisplayName, $"Pattern • {pat.Shape} ({pat.PelletCount} pellets)", pat, new Color(1f, 0.45f, 0.75f));
                }
            }
        }

        private bool MatchesSearch(string name, string id)
        {
            if (string.IsNullOrEmpty(_catalogSearchQuery)) return true;
            return (name != null && name.IndexOf(_catalogSearchQuery, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (id != null && id.IndexOf(_catalogSearchQuery, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void AddCatalogItem(string title, string subtitle, UnityEngine.Object target, Color accentColor)
        {
            var item = new VisualElement
            {
                style =
                {
                    backgroundColor = _inspectedAsset == target ? new Color(0.24f, 0.28f, 0.36f) : new Color(0.12f, 0.12f, 0.14f),
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 6,
                    paddingBottom = 6,
                    marginBottom = 3,
                    borderLeftWidth = 3,
                    borderLeftColor = accentColor,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    cursor = StyleKeyword.Auto
                }
            };

            var t = new Label(title) { style = { fontSize = 12, unityFontStyleAndWeight = FontStyle.Bold, color = Color.white } };
            var sub = new Label(subtitle) { style = { fontSize = 10, color = Color.gray } };

            item.Add(t);
            item.Add(sub);

            item.RegisterCallback<ClickEvent>(evt =>
            {
                _inspectedAsset = target;
                BuildCatalogList();
                InspectAssetInDetail(target);
            });

            _catalogListContainer.Add(item);
        }

        private void InspectAssetInDetail(UnityEngine.Object asset)
        {
            if (_catalogInspectorContainer == null) return;
            _catalogInspectorContainer.Clear();

            if (asset == null)
            {
                _catalogInspectorContainer.Add(new Label("Select an asset from the catalog on the left.") { style = { color = Color.gray, marginTop = 20 } });
                return;
            }

            // Header with Ping & Quick Action
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center, marginBottom = 8 } };
            var hLabel = new Label(asset.name) { style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, color = Color.white } };
            var pingBtn = new Button(() => EditorGUIUtility.PingObject(asset)) { text = "📍 Locate in Project" };
            pingBtn.style.height = 22;
            pingBtn.style.fontSize = 10;
            header.Add(hLabel);
            header.Add(pingBtn);
            _catalogInspectorContainer.Add(header);

            // Native, zero-lag UI Toolkit InspectorElement!
            var so = new SerializedObject(asset);
            var inspectorElement = new InspectorElement(so);
            _catalogInspectorContainer.Add(inspectorElement);
        }

        // ================= 3D VIEWPORT RENDERING =================

        private void InitializePreviewUtility()
        {
            if (_previewUtility == null)
            {
                _previewUtility = new PreviewRenderUtility();
                _previewUtility.cameraFieldOfView = 28f;
                _previewUtility.camera.nearClipPlane = 0.05f;
                _previewUtility.camera.farClipPlane = 25f;
                _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
                _previewUtility.camera.backgroundColor = new Color(0.10f, 0.10f, 0.12f, 1f);
                _previewUtility.camera.allowHDR = false;
                _previewUtility.camera.allowMSAA = false;

                if (_previewUtility.lights is { Length: > 0 })
                {
                    _previewUtility.lights[0].intensity = 1.4f;
                    _previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 45f, 0f);
                    _previewUtility.lights[0].color = Color.white;

                    if (_previewUtility.lights.Length > 1)
                    {
                        _previewUtility.lights[1].intensity = 0.7f;
                        _previewUtility.lights[1].transform.rotation = Quaternion.Euler(-25f, -135f, 0f);
                        _previewUtility.lights[1].color = new Color(0.7f, 0.85f, 1f);
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

        private void SetupViewportDragControls(Image img)
        {
            img.RegisterCallback<AttachToPanelEvent>(evt => RebuildPreviewModel());

            img.RegisterCallback<PointerDownEvent>(evt =>
            {
                _isDragging = true;
                _lastPointerPos = (Vector2)evt.position;
                img.CapturePointer(evt.pointerId);
            });

            img.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_isDragging)
                {
                    Vector2 delta = (Vector2)evt.position - _lastPointerPos;
                    _lastPointerPos = (Vector2)evt.position;
                    _previewRotY -= delta.x * 0.75f;
                    _previewRotX = Mathf.Clamp(_previewRotX + (delta.y * 0.75f), -85f, 85f);
                    Render3DPreview();
                }
            });

            img.RegisterCallback<PointerUpEvent>(evt =>
            {
                _isDragging = false;
                if (img.HasPointerCapture(evt.pointerId)) img.ReleasePointer(evt.pointerId);
            });

            img.RegisterCallback<PointerCaptureOutEvent>(evt => _isDragging = false);

            img.RegisterCallback<WheelEvent>(evt =>
            {
                _previewZoom = Mathf.Clamp(_previewZoom + (evt.delta.y * 0.08f), 0.35f, 4.5f);
                Render3DPreview();
                evt.StopPropagation();
            });
        }

        private void RebuildPreviewModel()
        {
            DestroyPreviewRoot();

            if (_selectedCore == null)
            {
                Render3DPreview();
                return;
            }

            _previewRootObject = new GameObject("Preview_Weapon_Root") { hideFlags = HideFlags.HideAndDontSave };
            var assembler = _previewRootObject.AddComponent<WeaponVisualAssembler>();
            var weapon = GetCurrentWeaponInstance();
            if (weapon != null)
            {
                assembler.AssembleVisuals(weapon);
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

            List<WeaponPartInstance> list = new();
            foreach (var kvp in _selectedParts)
            {
                if (kvp.Value != null)
                {
                    byte lvl = _partLevels.TryGetValue(kvp.Key, out byte l) ? l : _receiverLevel;
                    bool locked = _partLocks.TryGetValue(kvp.Key, out bool lk) ? lk : kvp.Value.IsUnique;
                    list.Add(new WeaponPartInstance(kvp.Value, lvl, locked));
                }
            }

            return new WeaponInstance(_selectedCore, list, _activeSeed, _receiverLevel, _activeRarity);
        }

        // ================= LIVE WEAPON CARD UI =================

        private void BuildWeaponCardUI(VisualElement parent)
        {
            var cardPanel = CreateCardPanel("LIVE WEAPON CARD & COMBAT DPS");

            _cardRoot = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.10f, 0.10f, 0.12f),
                    borderTopWidth = 3,
                    borderBottomWidth = 3,
                    borderLeftWidth = 3,
                    borderRightWidth = 3,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 10,
                    paddingBottom = 10
                }
            };

            _weaponNameLabel = new Label("Select a Weapon Core")
            {
                style = { fontSize = 15, unityFontStyleAndWeight = FontStyle.Bold, color = Color.white }
            };
            _cardRoot.Add(_weaponNameLabel);

            _rarityBadge = new Label("COMMON WEAPON")
            {
                style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 }
            };
            _cardRoot.Add(_rarityBadge);

            _redTextLabel = new Label(string.Empty)
            {
                style = { fontSize = 11, unityFontStyleAndWeight = FontStyle.Italic, color = new Color(0.95f, 0.3f, 0.3f), marginBottom = 8 }
            };
            _cardRoot.Add(_redTextLabel);

            _statsList = new VisualElement();
            _cardRoot.Add(_statsList);

            var div = new VisualElement { style = { height = 1, backgroundColor = new Color(0.25f, 0.25f, 0.28f), marginTop = 8, marginBottom = 8 } };
            _cardRoot.Add(div);

            _dpsList = new VisualElement();
            _cardRoot.Add(_dpsList);

            cardPanel.Add(_cardRoot);
            parent.Add(cardPanel);
        }

        private void RefreshWeaponCard()
        {
            if (_cardRoot == null) return;

            if (!_selectedCore)
            {
                _weaponNameLabel.text = "No Weapon Core Selected";
                _rarityBadge.text = "Select or roll a weapon core on the left.";
                _redTextLabel.text = string.Empty;
                _statsList.Clear();
                _dpsList.Clear();
                return;
            }

            var weapon = GetCurrentWeaponInstance();
            if (weapon == null) return;

            Color c = RarityUtility.GetColor(weapon.Rarity);
            _cardRoot.style.borderTopColor = c;
            _cardRoot.style.borderBottomColor = c;
            _cardRoot.style.borderLeftColor = c;
            _cardRoot.style.borderRightColor = c;

            _weaponNameLabel.text = weapon.DisplayName;
            _rarityBadge.text = $"LEVEL {weapon.Level} {weapon.Rarity.ToString().ToUpper()} {weapon.Core.WeaponType.ToString().ToUpper()}";
            _rarityBadge.style.color = c;
            _redTextLabel.text = !string.IsNullOrEmpty(weapon.RedText) ? $"\"{weapon.RedText}\"" : string.Empty;

            _statsList.Clear();
            var stats = weapon.CompiledStats;

            byte barrelLvl = weapon.GetPartLevel(WeaponPartType.Barrel);
            float recMultiplier = weapon.Core.CalculateLevelMultiplier(weapon.Level);
            float barMultiplier = weapon.Core.CalculateLevelMultiplier(barrelLvl);
            float effMult = (recMultiplier + barMultiplier) * 0.5f;

            AddCardRow(_statsList, "Damage", $"{stats.Damage:F1} (Base {weapon.Core.BaseDamage} × {effMult:F2})");
            if (stats.PelletsPerShot > 1)
                AddCardRow(_statsList, "Pellets", $"{stats.PelletsPerShot}x");
            if (weapon.PelletPattern != null)
                AddCardRow(_statsList, "🎯 Pattern", $"{weapon.PelletPattern.DisplayName} ({weapon.PelletPattern.Shape})", new Color(1f, 0.45f, 0.75f));
            AddCardRow(_statsList, "Fire Rate", $"{stats.FireRate:F0} RPM");
            AddCardRow(_statsList, "Magazine Size", $"{stats.MagazineSize}");
            AddCardRow(_statsList, "Reload Speed", $"{stats.ReloadSpeed:F2} s");
            AddCardRow(_statsList, "Accuracy Spread", $"{stats.BaseSpread:F1}° - {stats.MaxSpread:F1}°");
            AddCardRow(_statsList, "Recoil Kick", $"{stats.RecoilKick:F1}");
            AddCardRow(_statsList, "Critical Multiplier", $"{stats.CriticalMultiplier:F1}x");
            AddCardRow(_statsList, "Element", $"{stats.Element}");

            if (weapon.ProjectileBehaviors != null && weapon.ProjectileBehaviors.Count > 0)
            {
                List<string> bList = new();
                for (int i = 0; i < weapon.ProjectileBehaviors.Count; i++)
                {
                    if (weapon.ProjectileBehaviors[i]) bList.Add(weapon.ProjectileBehaviors[i].DisplayName);
                }
                AddCardRow(_statsList, "⚡ Trajectory", string.Join(", ", bList), new Color(0.2f, 0.85f, 0.95f));
            }

            if (weapon.WeaponPerks != null && weapon.WeaponPerks.Count > 0)
            {
                List<string> pList = new();
                for (int i = 0; i < weapon.WeaponPerks.Count; i++)
                {
                    if (weapon.WeaponPerks[i]) pList.Add(weapon.WeaponPerks[i].DisplayName);
                }
                AddCardRow(_statsList, "✨ Active Perks", string.Join(", ", pList), new Color(0.95f, 0.85f, 0.35f));
            }

            _dpsList.Clear();
            float burstDPS = stats.Damage * stats.PelletsPerShot * (stats.FireRate / 60f);
            float magCycle = stats.FireRate > 0 ? (stats.MagazineSize / (stats.FireRate / 60f)) + stats.ReloadSpeed : 1f;
            float sustainedDPS = (stats.Damage * stats.PelletsPerShot * stats.MagazineSize) / magCycle;

            AddCardRow(_dpsList, "Burst DPS", $"{burstDPS:F0} dps", new Color(1f, 0.85f, 0.3f));
            AddCardRow(_dpsList, "Sustained DPS", $"{sustainedDPS:F0} dps", new Color(0.4f, 0.9f, 0.4f));
        }

        private void AddCardRow(VisualElement container, string key, string value, Color? valueColor = null)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 2 } };
            var k = new Label(key) { style = { color = new Color(0.7f, 0.7f, 0.7f), fontSize = 11 } };
            var v = new Label(value) { style = { color = valueColor ?? Color.white, fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold } };
            row.Add(k);
            row.Add(v);
            container.Add(row);
        }

        // ================= ASSET CREATION HELPERS =================

        private static void EnsureDirectoryExists(string assetFolderPath)
        {
            if (!System.IO.Directory.Exists(assetFolderPath))
            {
                System.IO.Directory.CreateDirectory(assetFolderPath);
                AssetDatabase.Refresh();
            }
        }

        private static string GetPartFolderPath(WeaponPartType slot)
        {
            string subfolder = slot switch
            {
                WeaponPartType.Barrel => "Barrels",
                WeaponPartType.Grip => "Grips",
                WeaponPartType.Stock => "Stocks",
                WeaponPartType.Sight => "Sights",
                WeaponPartType.Magazine => "Magazines",
                WeaponPartType.Accessory => "Accessories",
                _ => ""
            };

            string folder = string.IsNullOrEmpty(subfolder)
                ? "Assets/_Project/Data/Items/Weapons/Parts"
                : $"Assets/_Project/Data/Items/Weapons/Parts/{subfolder}";

            EnsureDirectoryExists(folder);
            return folder;
        }

        private void CreateNewCoreAsset()
        {
            string folder = "Assets/_Project/Data/Items/Weapons/Cores";
            EnsureDirectoryExists(folder);
            string defaultName = $"CORE_{(_selectedCore != null ? _selectedCore.WeaponType.ToString() : "Pistol")}_New";
            string path = EditorUtility.SaveFilePanelInProject("Create Weapon Core", defaultName, "asset", "Save Weapon Core Definition", folder);
            if (string.IsNullOrEmpty(path)) return;

            var core = CreateInstance<WeaponCoreDefinition>();
            var so = new SerializedObject(core);
            so.FindProperty("_coreId").stringValue = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(core, path);
            AssetDatabase.SaveAssets();

            if (ItemRegistry.Instance) ItemRegistry.Instance.RegisterCore(core);

            _selectedCore = core;
            OnCoreChanged();
            BuildCatalogList();
            InspectAssetInDetail(core);
        }

        private void CreateNewPartAsset(WeaponPartType slot)
        {
            string folder = GetPartFolderPath(slot);
            string weaponTypeName = _selectedCore != null ? _selectedCore.WeaponType.ToString() : "Pistol";
            string defaultName = $"PART_{weaponTypeName}_{slot}_New";
            string path = EditorUtility.SaveFilePanelInProject($"Create {slot} Part", defaultName, "asset", $"Save {slot} Part Definition", folder);
            if (string.IsNullOrEmpty(path)) return;

            var part = CreateInstance<WeaponPartDefinition>();
            var so = new SerializedObject(part);
            so.FindProperty("_partId").stringValue = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
            so.FindProperty("_partType").enumValueIndex = (int)slot;
            so.FindProperty("_displayName").stringValue = $"Standard {slot}";
            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(part, path);
            AssetDatabase.SaveAssets();

            if (ItemRegistry.Instance) ItemRegistry.Instance.RegisterPart(part);

            _selectedParts[slot] = part;
            if (_slotObjectFields.TryGetValue(slot, out var f)) f.SetValueWithoutNotify(part);
            BuildCatalogList();
            InspectAssetInDetail(part);
            RebuildPreviewModel();
            RefreshWeaponCard();
        }

        private void CreateNewBrandAsset()
        {
            string folder = "Assets/_Project/Data/Items/Manufacturers";
            EnsureDirectoryExists(folder);
            string path = EditorUtility.SaveFilePanelInProject("Create Manufacturer Brand", "MFG_BrandName_New", "asset", "Save Manufacturer Brand Definition", folder);
            if (string.IsNullOrEmpty(path)) return;

            var mfg = CreateInstance<ManufacturerDefinition>();
            var so = new SerializedObject(mfg);
            so.FindProperty("_manufacturerId").stringValue = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
            so.FindProperty("_displayName").stringValue = "Brand Name";
            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(mfg, path);
            AssetDatabase.SaveAssets();

            if (ItemRegistry.Instance) ItemRegistry.Instance.RegisterManufacturer(mfg);

            BuildCatalogList();
            InspectAssetInDetail(mfg);
        }

        private void ShowNewBehaviorMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Bouncing Projectile"), false, () => CreateNewBehaviorAsset(typeof(BouncingBehaviorDefinition), "BEH_Bouncing_New"));
            menu.AddItem(new GUIContent("Gravity Trajectory"), false, () => CreateNewBehaviorAsset(typeof(GravityBehaviorDefinition), "BEH_Gravity_New"));
            menu.AddItem(new GUIContent("Wave Trajectory"), false, () => CreateNewBehaviorAsset(typeof(WaveBehaviorDefinition), "BEH_Wave_New"));
            menu.AddItem(new GUIContent("Cluster Split"), false, () => CreateNewBehaviorAsset(typeof(ClusterSplitBehaviorDefinition), "BEH_Split_New"));
            menu.AddItem(new GUIContent("Accelerating Flight"), false, () => CreateNewBehaviorAsset(typeof(AcceleratingBehaviorDefinition), "BEH_Accelerating_New"));
            menu.ShowAsContext();
        }

        private void CreateNewBehaviorAsset(Type type, string defaultName)
        {
            string folder = "Assets/_Project/Data/Items/Weapons/Behaviors";
            EnsureDirectoryExists(folder);
            string path = EditorUtility.SaveFilePanelInProject("Create Projectile Behavior", defaultName, "asset", "Save Behavior Definition", folder);
            if (string.IsNullOrEmpty(path)) return;

            var beh = CreateInstance(type) as ProjectileBehaviorDefinition;
            if (beh != null)
            {
                var so = new SerializedObject(beh);
                var idProp = so.FindProperty("_behaviorId");
                if (idProp != null) idProp.stringValue = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
                so.ApplyModifiedProperties();
            }

            AssetDatabase.CreateAsset(beh, path);
            AssetDatabase.SaveAssets();

            if (ItemRegistry.Instance) ItemRegistry.Instance.ScanProjectAssets(silent: true);

            BuildCatalogList();
            InspectAssetInDetail(beh);
        }

        private void CreateNewPerkAsset()
        {
            string folder = "Assets/_Project/Data/Items/Weapons/Perks";
            EnsureDirectoryExists(folder);
            string path = EditorUtility.SaveFilePanelInProject("Create Weapon Perk", "PERK_Refund_New", "asset", "Save Weapon Perk Definition", folder);
            if (string.IsNullOrEmpty(path)) return;

            var perk = CreateInstance<AmmoRefundPerkDefinition>();
            var so = new SerializedObject(perk);
            var idProp = so.FindProperty("_perkId");
            if (idProp != null) idProp.stringValue = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(perk, path);
            AssetDatabase.SaveAssets();

            if (ItemRegistry.Instance) ItemRegistry.Instance.ScanProjectAssets(silent: true);

            BuildCatalogList();
            InspectAssetInDetail(perk);
        }

        private void ShowNewPatternMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Horizontal Line (Sand Hawk)"), false, () => CreateNewPatternAsset(PatternShape.HorizontalLine, 5, 1.2f, "PAT_Line_5P_New"));
            menu.AddItem(new GUIContent("3x3 Matrix Grid (Blockhead)"), false, () => CreateNewPatternAsset(PatternShape.Grid3x3, 9, 1.0f, "PAT_Grid_9P_New"));
            menu.AddItem(new GUIContent("2x2 Matrix Grid"), false, () => CreateNewPatternAsset(PatternShape.Grid2x2, 4, 1.0f, "PAT_Grid_4P_New"));
            menu.AddItem(new GUIContent("Ring Circle"), false, () => CreateNewPatternAsset(PatternShape.Ring, 8, 1.2f, "PAT_Ring_8P_New"));
            menu.AddItem(new GUIContent("Cross (+)"), false, () => CreateNewPatternAsset(PatternShape.Cross, 5, 1.2f, "PAT_Cross_5P_New"));
            menu.AddItem(new GUIContent("X Shape"), false, () => CreateNewPatternAsset(PatternShape.XShape, 5, 1.2f, "PAT_X_5P_New"));
            menu.AddItem(new GUIContent("Heart (Heart Breaker)"), false, () => CreateNewPatternAsset(PatternShape.Heart, 12, 1.5f, "PAT_Heart_12P_New"));
            menu.AddItem(new GUIContent("Custom Points"), false, () => CreateNewPatternAsset(PatternShape.Custom, 6, 1.0f, "PAT_Custom_6P_New"));
            menu.ShowAsContext();
        }

        private void CreateNewPatternAsset(PatternShape shape, int count, float spacing, string defaultName)
        {
            string folder = "Assets/_Project/Data/Items/Weapons/Patterns";
            EnsureDirectoryExists(folder);
            string path = EditorUtility.SaveFilePanelInProject("Create Pellet Pattern", defaultName, "asset", "Save Pellet Pattern Definition", folder);
            if (string.IsNullOrEmpty(path)) return;

            var pat = CreateInstance<PelletPatternDefinition>();
            var so = new SerializedObject(pat);
            so.FindProperty("_patternId").stringValue = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
            so.FindProperty("_displayName").stringValue = ObjectNames.NicifyVariableName(shape.ToString());
            so.FindProperty("_shape").enumValueIndex = (int)shape;
            so.FindProperty("_pelletCount").intValue = count;
            so.FindProperty("_spacingDegrees").floatValue = spacing;
            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(pat, path);
            AssetDatabase.SaveAssets();

            if (ItemRegistry.Instance) ItemRegistry.Instance.RegisterPattern(pat);

            BuildCatalogList();
            InspectAssetInDetail(pat);
        }

        private static VisualElement CreateCardPanel(string title)
        {
            var panel = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.12f, 0.12f, 0.14f),
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 10,
                    paddingBottom = 10,
                    marginBottom = 10,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = new Color(0.20f, 0.20f, 0.22f),
                    borderBottomColor = new Color(0.20f, 0.20f, 0.22f),
                    borderLeftColor = new Color(0.20f, 0.20f, 0.22f),
                    borderRightColor = new Color(0.20f, 0.20f, 0.22f)
                }
            };

            var t = new Label(title)
            {
                style =
                {
                    fontSize = 12,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.85f, 0.85f, 0.9f),
                    marginBottom = 8
                }
            };
            panel.Add(t);
            return panel;
        }
    }
}
