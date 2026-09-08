using System;
using System.Collections.Generic;
using System.Linq;
using Project.Combat;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Combat.Editor
{
    /// <summary>
    /// UI Toolkit searchable picker popup that filters assets by exact WeaponPartType or WeaponCore.
    /// Opens as a native dropdown anchored to the triggering field.
    /// </summary>
    public class WeaponAssetPickerWindow : EditorWindow
    {
        private enum PickerMode { Part, Core }
        private PickerMode _mode = PickerMode.Part;
        private WeaponPartType _slotType;
        private WeaponCoreDefinition _activeCore;
        private Action<WeaponPartDefinition> _onPartSelected;
        private Action<WeaponCoreDefinition> _onCoreSelected;

        private string _searchQuery = string.Empty;
        private ToolbarSearchField _searchField;
        private ScrollView _itemListContainer;
        private Label _titleLabel;
        private Label _countLabel;

        private List<WeaponPartDefinition> _allSlotParts = new();
        private List<WeaponCoreDefinition> _allCores = new();

        public static void ShowPartPicker(EditorWindow parentWindow, Rect worldBound, WeaponPartType slot, WeaponCoreDefinition core, Action<WeaponPartDefinition> onSelected)
        {
            var win = CreateInstance<WeaponAssetPickerWindow>();
            win._mode = PickerMode.Part;
            win._slotType = slot;
            win._activeCore = core;
            win._onPartSelected = onSelected;
            win.titleContent = new GUIContent($"Select {slot}");
            win.LoadPartData();

            Rect screenRect = CalculateScreenRect(parentWindow, worldBound);
            win.ShowAsDropDown(screenRect, new Vector2(340, 440));
        }

        public static void ShowCorePicker(EditorWindow parentWindow, Rect worldBound, Action<WeaponCoreDefinition> onSelected)
        {
            var win = CreateInstance<WeaponAssetPickerWindow>();
            win._mode = PickerMode.Core;
            win._onCoreSelected = onSelected;
            win.titleContent = new GUIContent("Select Weapon Core");
            win.LoadCoreData();

            Rect screenRect = CalculateScreenRect(parentWindow, worldBound);
            win.ShowAsDropDown(screenRect, new Vector2(340, 440));
        }

        private static Rect CalculateScreenRect(EditorWindow parentWindow, Rect worldBound)
        {
            if (parentWindow != null)
            {
                return new Rect(
                    parentWindow.position.x + worldBound.x,
                    parentWindow.position.y + worldBound.y + 24,
                    Mathf.Max(worldBound.width, 140),
                    Mathf.Max(worldBound.height, 22)
                );
            }

            Vector2 screenPoint = GUIUtility.GUIToScreenPoint(worldBound.position);
            return new Rect(screenPoint.x, screenPoint.y, Mathf.Max(worldBound.width, 140), Mathf.Max(worldBound.height, 22));
        }

        private void LoadPartData()
        {
            _allSlotParts = GetPartsForSlot(_slotType);
        }

        private void LoadCoreData()
        {
            _allCores = GetAllCores();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = new Color(0.20f, 0.20f, 0.20f);
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;
            root.style.borderTopWidth = 1;
            root.style.borderBottomWidth = 1;
            root.style.borderLeftWidth = 1;
            root.style.borderRightWidth = 1;
            root.style.borderTopColor = new Color(0.14f, 0.14f, 0.14f);
            root.style.borderBottomColor = new Color(0.14f, 0.14f, 0.14f);
            root.style.borderLeftColor = new Color(0.14f, 0.14f, 0.14f);
            root.style.borderRightColor = new Color(0.14f, 0.14f, 0.14f);

            // 1. Header
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center, marginBottom = 6 } };
            string titleText = _mode == PickerMode.Part ? $"Select {_slotType}" : "Select Weapon Core";
            _titleLabel = new Label(titleText)
            {
                style = { fontSize = 12, unityFontStyleAndWeight = FontStyle.Bold, color = Color.white }
            };
            header.Add(_titleLabel);

            _countLabel = new Label(string.Empty)
            {
                style = { fontSize = 10, color = new Color(0.65f, 0.65f, 0.65f) }
            };
            header.Add(_countLabel);
            root.Add(header);

            // 2. Search Field
            _searchField = new ToolbarSearchField { value = _searchQuery, style = { marginBottom = 6 } };
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchQuery = evt.newValue;
                BuildList();
            });
            root.Add(_searchField);

            // Auto-focus search field
            root.schedule.Execute(() => _searchField.Q("unity-search-field")?.Focus()).ExecuteLater(50);

            // 3. Scrollable List Container
            _itemListContainer = new ScrollView { style = { flexGrow = 1 } };
            root.Add(_itemListContainer);

            // Keyboard navigation (Escape to close, Enter to pick first)
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    PickFirstMatchingItem();
                    evt.StopPropagation();
                }
            });

            BuildList();
        }

        private void BuildList()
        {
            if (_itemListContainer == null) return;
            _itemListContainer.Clear();

            if (_mode == PickerMode.Part)
            {
                BuildPartList();
            }
            else
            {
                BuildCoreList();
            }
        }

        private void BuildPartList()
        {
            // Option to detach / clear slot
            var detachItem = CreateListItem("[None] (Detach Part)", "Clear this modular socket", null, new Color(0.5f, 0.5f, 0.5f), () =>
            {
                _onPartSelected?.Invoke(null);
                Close();
            });
            _itemListContainer.Add(detachItem);

            string query = _searchQuery?.Trim();
            bool hasQuery = !string.IsNullOrEmpty(query);

            List<WeaponPartDefinition> compatibleParts = new();
            List<WeaponPartDefinition> otherParts = new();

            HashSet<WeaponPartDefinition> coreCompSet = new();
            if (_activeCore != null)
            {
                var compList = GetCompatiblePartsForSlot(_activeCore, _slotType);
                if (compList != null)
                {
                    foreach (var p in compList)
                    {
                        if (p) coreCompSet.Add(p);
                    }
                }
            }

            foreach (var part in _allSlotParts)
            {
                if (!part) continue;
                if (hasQuery && !MatchesPart(part, query)) continue;

                if (coreCompSet.Contains(part))
                {
                    compatibleParts.Add(part);
                }
                else
                {
                    otherParts.Add(part);
                }
            }

            int totalMatches = compatibleParts.Count + otherParts.Count;
            _countLabel.text = $"{totalMatches} {_slotType}s";

            // If we have an active core, show compatible group first
            if (_activeCore != null && compatibleParts.Count > 0)
            {
                _itemListContainer.Add(CreateSectionHeader($"COMPATIBLE WITH {_activeCore.BaseWeaponName.ToUpper()} ({compatibleParts.Count})"));
                foreach (var part in compatibleParts)
                {
                    _itemListContainer.Add(CreatePartRow(part));
                }
            }

            if (otherParts.Count > 0)
            {
                string label = _activeCore != null ? $"OTHER {_slotType.ToString().ToUpper()}S ({otherParts.Count})" : $"ALL {_slotType.ToString().ToUpper()}S ({otherParts.Count})";
                _itemListContainer.Add(CreateSectionHeader(label));
                foreach (var part in otherParts)
                {
                    _itemListContainer.Add(CreatePartRow(part));
                }
            }

            if (totalMatches == 0)
            {
                var noMatches = new Label($"No {_slotType}s matching '{_searchQuery}'")
                {
                    style = { color = new Color(0.60f, 0.60f, 0.60f), fontSize = 11, alignSelf = Align.Center, marginTop = 20 }
                };
                _itemListContainer.Add(noMatches);
            }
        }

        private VisualElement CreatePartRow(WeaponPartDefinition part)
        {
            Color rarityColor = RarityUtility.GetColor(part.Rarity);
            string subtitle = $"{part.PartId} • {part.Rarity}";
            if (part.IsUnique) subtitle += " [UNIQUE]";
            if (part.Manufacturer != null) subtitle += $" • {part.Manufacturer.DisplayName}";

            return CreateListItem(part.DisplayName, subtitle, part, rarityColor, () =>
            {
                _onPartSelected?.Invoke(part);
                Close();
            });
        }

        private void BuildCoreList()
        {
            string query = _searchQuery?.Trim();
            bool hasQuery = !string.IsNullOrEmpty(query);

            var matches = _allCores.Where(c => c != null && (!hasQuery || MatchesCore(c, query))).ToList();
            _countLabel.text = $"{matches.Count} Cores";

            if (matches.Count > 0)
            {
                _itemListContainer.Add(CreateSectionHeader($"WEAPON CORES ({matches.Count})"));
                foreach (var core in matches)
                {
                    string subtitle = $"{core.CoreId} • {core.WeaponType} • Base DMG {core.BaseDamage} • {core.BaseFireRate} RPM";
                    var item = CreateListItem(core.BaseWeaponName, subtitle, core, new Color(0.40f, 0.70f, 1.0f), () =>
                    {
                        _onCoreSelected?.Invoke(core);
                        Close();
                    });
                    _itemListContainer.Add(item);
                }
            }
            else
            {
                var noMatches = new Label($"No Cores matching '{_searchQuery}'")
                {
                    style = { color = new Color(0.60f, 0.60f, 0.60f), fontSize = 11, alignSelf = Align.Center, marginTop = 20 }
                };
                _itemListContainer.Add(noMatches);
            }
        }

        private VisualElement CreateListItem(string title, string subtitle, UnityEngine.Object target, Color accentColor, Action onClick)
        {
            var item = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    backgroundColor = new Color(0.18f, 0.18f, 0.18f),
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 5,
                    paddingBottom = 5,
                    marginBottom = 2,
                    borderLeftWidth = 3,
                    borderLeftColor = accentColor,
                    borderTopLeftRadius = 2,
                    borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2,
                    borderBottomRightRadius = 2,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = new Color(0.15f, 0.15f, 0.15f),
                    borderBottomColor = new Color(0.15f, 0.15f, 0.15f),
                    borderRightColor = new Color(0.15f, 0.15f, 0.15f),
                    cursor = StyleKeyword.Auto
                }
            };

            var titleLabel = new Label(title)
            {
                style = { fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold, color = Color.white }
            };
            var subLabel = new Label(subtitle)
            {
                style = { fontSize = 9, color = new Color(0.65f, 0.65f, 0.65f) }
            };

            item.Add(titleLabel);
            item.Add(subLabel);

            item.RegisterCallback<MouseEnterEvent>(evt =>
            {
                item.style.backgroundColor = new Color(0.24f, 0.37f, 0.58f, 0.45f);
            });

            item.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                item.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            });

            item.RegisterCallback<ClickEvent>(evt => onClick?.Invoke());

            return item;
        }

        private static VisualElement CreateSectionHeader(string text)
        {
            return new Label(text)
            {
                style =
                {
                    fontSize = 9,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.55f, 0.75f, 0.95f),
                    paddingLeft = 6,
                    paddingTop = 6,
                    paddingBottom = 2,
                    marginTop = 4,
                    marginBottom = 2
                }
            };
        }

        private static bool MatchesPart(WeaponPartDefinition part, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            if (part.DisplayName != null && part.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (part.PartId != null && part.PartId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (part.Rarity.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (part.Manufacturer != null && part.Manufacturer.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static bool MatchesCore(WeaponCoreDefinition core, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            if (core.BaseWeaponName != null && core.BaseWeaponName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (core.CoreId != null && core.CoreId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (core.WeaponType.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private void PickFirstMatchingItem()
        {
            if (_mode == PickerMode.Part)
            {
                string query = _searchQuery?.Trim();
                var first = _allSlotParts.FirstOrDefault(p => p != null && (string.IsNullOrEmpty(query) || MatchesPart(p, query)));
                if (first != null)
                {
                    _onPartSelected?.Invoke(first);
                    Close();
                }
            }
            else
            {
                string query = _searchQuery?.Trim();
                var first = _allCores.FirstOrDefault(c => c != null && (string.IsNullOrEmpty(query) || MatchesCore(c, query)));
                if (first != null)
                {
                    _onCoreSelected?.Invoke(first);
                    Close();
                }
            }
        }

        private static IReadOnlyList<WeaponPartDefinition> GetCompatiblePartsForSlot(WeaponCoreDefinition core, WeaponPartType slot)
        {
            if (!core) return null;
            return slot switch
            {
                WeaponPartType.Barrel => core.CompatibleBarrels,
                WeaponPartType.Grip => core.CompatibleGrips,
                WeaponPartType.Stock => core.CompatibleStocks,
                WeaponPartType.Sight => core.CompatibleSights,
                WeaponPartType.Magazine => core.CompatibleMagazines,
                WeaponPartType.Accessory => core.CompatibleAccessories,
                _ => null
            };
        }

        private static List<WeaponPartDefinition> GetPartsForSlot(WeaponPartType slot)
        {
            var list = new List<WeaponPartDefinition>();
            var reg = ItemRegistry.Instance;
            if (reg != null && reg.WeaponParts != null)
            {
                for (int i = 0; i < reg.WeaponParts.Count; i++)
                {
                    var p = reg.WeaponParts[i];
                    if (p != null && p.PartType == slot)
                    {
                        list.Add(p);
                    }
                }
            }

            if (list.Count == 0)
            {
                string[] guids = AssetDatabase.FindAssets("t:WeaponPartDefinition");
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var part = AssetDatabase.LoadAssetAtPath<WeaponPartDefinition>(path);
                    if (part != null && part.PartType == slot && !list.Contains(part))
                    {
                        list.Add(part);
                    }
                }
            }

            return list;
        }

        private static List<WeaponCoreDefinition> GetAllCores()
        {
            var list = new List<WeaponCoreDefinition>();
            var reg = ItemRegistry.Instance;
            if (reg != null && reg.WeaponCores != null)
            {
                for (int i = 0; i < reg.WeaponCores.Count; i++)
                {
                    var c = reg.WeaponCores[i];
                    if (c != null) list.Add(c);
                }
            }

            if (list.Count == 0)
            {
                string[] guids = AssetDatabase.FindAssets("t:WeaponCoreDefinition");
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var core = AssetDatabase.LoadAssetAtPath<WeaponCoreDefinition>(path);
                    if (core != null && !list.Contains(core))
                    {
                        list.Add(core);
                    }
                }
            }

            return list;
        }
    }
}
