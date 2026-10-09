using System;
using System.Collections.Generic;
using Project.Combat.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Weapons.Editor
{
[CustomEditor(typeof(WeaponDefinition))]
public class WeaponDefinitionEditor : UnityEditor.Editor
{
    private WeaponDefinition _targetWeapon;
    private readonly Dictionary<WeaponAttachmentSlot, WeaponAttachmentDefinition> _selectedSimAttachments = new();

    private HelpBox _compatHelpBox;
    private VisualElement _simTable;

    private Label _damageValueLabel;
    private Label _damageDeltaLabel;
    private Label _magValueLabel;
    private Label _magDeltaLabel;
    private Label _reloadValueLabel;
    private Label _reloadDeltaLabel;
    private Label _fireRateValueLabel;
    private Label _fireRateDeltaLabel;
    private Label _dpsValueLabel;
    private Label _dpsDeltaLabel;
    private Label _rangeValueLabel;
    private Label _rangeDeltaLabel;

    private void OnEnable()
    {
        _targetWeapon = (WeaponDefinition)target;
        _selectedSimAttachments.Clear();
        foreach (WeaponAttachmentSlot slot in Enum.GetValues(typeof(WeaponAttachmentSlot)))
        {
            _selectedSimAttachments[slot] = null;
        }
    }

    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.style.paddingTop = 4;
        root.style.paddingBottom = 8;

        // Simple native info header
        var headerBox = new HelpBox($"Category: {_targetWeapon.Category}  |  ID: {_targetWeapon.Id}", HelpBoxMessageType.None);
        headerBox.style.marginBottom = 8;
        root.Add(headerBox);

        // Core Serialized Properties (Native Unity fields)
        var prop = serializedObject.GetIterator();
        if (prop.NextVisible(true))
        {
            do
            {
                if (prop.name is "m_Script") continue;

                var field = new PropertyField(prop);
                field.Bind(serializedObject);

                if (prop.name is "_baseDamage" or "_baseMagazineSize" or "_baseReloadDuration" or "_baseFireRate" or "_baseRange" or "_category")
                {
                    field.RegisterValueChangeCallback(_ =>
                    {
                        serializedObject.ApplyModifiedProperties();
                        RefreshCompatibilitySummary();
                        UpdateSimulationResults();
                    });
                }

                root.Add(field);
            }
            while (prop.NextVisible(false));
        }

        AddSeparator(root);

        // Attachment Compatibility Foldout
        var compatFoldout = new Foldout { text = "Attachment Compatibility Management", value = false };
        compatFoldout.style.marginBottom = 6;

        _compatHelpBox = new HelpBox("", HelpBoxMessageType.Info);
        _compatHelpBox.style.marginBottom = 6;
        compatFoldout.Add(_compatHelpBox);

        var syncButton = new Button(OnSyncButtonClicked) { text = "Sync Attachments with Project", tooltip = "Scans all attachments and updates this weapon based on category rules and overrides." };
        syncButton.style.height = 24;
        syncButton.style.marginBottom = 6;
        compatFoldout.Add(syncButton);

        root.Add(compatFoldout);

        AddSeparator(root);

        // Live Stat Simulator Foldout
        var simFoldout = new Foldout { text = "Live Stat Simulator (Loadout Preview)", value = true };
        simFoldout.style.marginBottom = 6;

        var simBox = new Box();
        simBox.style.paddingLeft = 8;
        simBox.style.paddingRight = 8;
        simBox.style.paddingTop = 8;
        simBox.style.paddingBottom = 8;
        simBox.style.marginBottom = 6;

        // Slot Selectors
        var slotSelectorsContainer = new VisualElement { style = { marginBottom = 8 } };
        BuildSlotSelectors(slotSelectorsContainer);
        simBox.Add(slotSelectorsContainer);

        // Comparison Table
        _simTable = new VisualElement();
        BuildSimulationResultsTable(_simTable);
        simBox.Add(_simTable);

        simFoldout.Add(simBox);
        root.Add(simFoldout);

        RefreshCompatibilitySummary();
        UpdateSimulationResults();

        return root;
    }

    private void BuildSlotSelectors(VisualElement container)
    {
        container.Clear();
        var allAttachments = _targetWeapon.CompatibleAttachments;
        var attachmentsBySlot = new Dictionary<WeaponAttachmentSlot, List<WeaponAttachmentDefinition>>();

        foreach (WeaponAttachmentSlot slot in Enum.GetValues(typeof(WeaponAttachmentSlot)))
        {
            attachmentsBySlot[slot] = new List<WeaponAttachmentDefinition>();
        }

        for (var i = 0; i < allAttachments.Count; i++)
        {
            var att = allAttachments[i];
            if (att) attachmentsBySlot[att.Slot].Add(att);
        }

        foreach (WeaponAttachmentSlot slot in Enum.GetValues(typeof(WeaponAttachmentSlot)))
        {
            var available = attachmentsBySlot[slot];
            var choices = new List<string> { "(None)" };
            var listRef = new List<WeaponAttachmentDefinition> { null };

            for (var i = 0; i < available.Count; i++)
            {
                var att = available[i];
                choices.Add(att.DisplayName);
                listRef.Add(att);
            }

            var dropdown = new PopupField<string>(slot.ToString(), choices, 0);
            dropdown.style.marginBottom = 2;
            var currentSlot = slot;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                var index = choices.IndexOf(evt.newValue);
                _selectedSimAttachments[currentSlot] = index >= 0 && index < listRef.Count ? listRef[index] : null;
                UpdateSimulationResults();
            });

            container.Add(dropdown);
        }
    }

    private void BuildSimulationResultsTable(VisualElement table)
    {
        table.Clear();

        var headerRow = CreateTableRow("Stat", "Base", "Resolved", "Delta", true);
        table.Add(headerRow);

        table.Add(CreateStatRow("Damage", out _damageValueLabel, out _damageDeltaLabel));
        table.Add(CreateStatRow("Magazine Size", out _magValueLabel, out _magDeltaLabel));
        table.Add(CreateStatRow("Reload Duration", out _reloadValueLabel, out _reloadDeltaLabel));
        table.Add(CreateStatRow("Fire Rate", out _fireRateValueLabel, out _fireRateDeltaLabel));
        table.Add(CreateStatRow("Effective DPS", out _dpsValueLabel, out _dpsDeltaLabel));
        table.Add(CreateStatRow("Effective Range", out _rangeValueLabel, out _rangeDeltaLabel));
    }

    private VisualElement CreateStatRow(string label, out Label valLabel, out Label deltaLabel)
    {
        var row = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.SpaceBetween,
                alignItems = Align.Center,
                paddingTop = 2,
                paddingBottom = 2
            }
        };

        var nameLbl = new Label(label) { style = { width = 110, fontSize = 11 } };
        valLabel = new Label("--") { style = { width = 120, fontSize = 11 } };
        deltaLabel = new Label("--") { style = { flexGrow = 1, fontSize = 11, unityTextAlign = TextAnchor.MiddleRight } };

        row.Add(nameLbl);
        row.Add(valLabel);
        row.Add(deltaLabel);
        return row;
    }

    private VisualElement CreateTableRow(string c1, string c2, string c3, string c4, bool isHeader)
    {
        var row = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.SpaceBetween,
                alignItems = Align.Center,
                paddingBottom = 3,
                marginBottom = 3,
                borderBottomWidth = 1,
                borderBottomColor = new Color(0f, 0f, 0f, 0.2f)
            }
        };

        var style = isHeader ? FontStyle.Bold : FontStyle.Normal;
        row.Add(new Label(c1) { style = { width = 110, fontSize = 11, unityFontStyleAndWeight = style } });
        row.Add(new Label(c3) { style = { width = 120, fontSize = 11, unityFontStyleAndWeight = style } });
        row.Add(new Label(c4) { style = { flexGrow = 1, fontSize = 11, unityFontStyleAndWeight = style, unityTextAlign = TextAnchor.MiddleRight } });
        return row;
    }

    private void UpdateSimulationResults()
    {
        if (!_targetWeapon) return;

        var selectedIds = new List<string>();
        foreach (var kvp in _selectedSimAttachments)
        {
            if (kvp.Value && !string.IsNullOrEmpty(kvp.Value.Id))
            {
                selectedIds.Add(kvp.Value.Id);
            }
        }

        WeaponResolvedStats resolved;
        try
        {
            resolved = WeaponStatResolver.Resolve(_targetWeapon, selectedIds, out _, false);
        }
        catch (Exception ex)
        {
            if (_damageValueLabel != null) _damageValueLabel.text = $"Err: {ex.Message}";
            return;
        }

        // Damage
        FormatDelta(_damageValueLabel, _damageDeltaLabel, _targetWeapon.BaseDamage, resolved.Damage, false, "");

        // Magazine Size
        FormatDelta(_magValueLabel, _magDeltaLabel, _targetWeapon.BaseMagazineSize, resolved.MagazineSize, false, " rds");

        // Reload Duration (lower is better!)
        FormatDeltaFloat(_reloadValueLabel, _reloadDeltaLabel, _targetWeapon.BaseReloadDuration, resolved.ReloadDuration, true, "s");

        // Fire Rate
        FormatDelta(_fireRateValueLabel, _fireRateDeltaLabel, _targetWeapon.BaseFireRate, resolved.FireRate, false, " RPM");

        // DPS
        var baseDps = _targetWeapon.BaseDamage * (_targetWeapon.BaseFireRate / 60f);
        var resolvedDps = resolved.Damage * (resolved.FireRate / 60f);
        FormatDeltaFloat(_dpsValueLabel, _dpsDeltaLabel, baseDps, resolvedDps, false, "");

        // Range
        FormatDeltaFloat(_rangeValueLabel, _rangeDeltaLabel, _targetWeapon.BaseRange, resolved.Range, false, "m");
    }

    private void FormatDelta(Label valueLabel, Label deltaLabel, int baseVal, int resolvedVal, bool lowerIsBetter, string suffix)
    {
        if (valueLabel == null || deltaLabel == null) return;
        valueLabel.text = $"{baseVal} -> {resolvedVal}{suffix}";
        var diff = resolvedVal - baseVal;
        if (diff == 0)
        {
            deltaLabel.text = "-";
            deltaLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
        }
        else
        {
            var isGood = lowerIsBetter ? diff < 0 : diff > 0;
            var sign = diff > 0 ? "+" : "";
            var pct = baseVal != 0 ? (float)diff / baseVal * 100f : 0f;
            deltaLabel.text = $"{sign}{diff} ({sign}{pct:F0}%)";
            deltaLabel.style.color = isGood ? new Color(0.35f, 0.75f, 0.35f) : new Color(0.85f, 0.4f, 0.4f);
        }
    }

    private void FormatDeltaFloat(Label valueLabel, Label deltaLabel, float baseVal, float resolvedVal, bool lowerIsBetter, string suffix)
    {
        if (valueLabel == null || deltaLabel == null) return;
        valueLabel.text = $"{baseVal:F2}{suffix} -> {resolvedVal:F2}{suffix}";
        var diff = resolvedVal - baseVal;
        if (Mathf.Abs(diff) < 0.001f)
        {
            deltaLabel.text = "-";
            deltaLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
        }
        else
        {
            var isGood = lowerIsBetter ? diff < 0 : diff > 0;
            var sign = diff > 0 ? "+" : "";
            var pct = baseVal != 0 ? diff / baseVal * 100f : 0f;
            deltaLabel.text = $"{sign}{diff:F2} ({sign}{pct:F0}%)";
            deltaLabel.style.color = isGood ? new Color(0.35f, 0.75f, 0.35f) : new Color(0.85f, 0.4f, 0.4f);
        }
    }

    private void RefreshCompatibilitySummary()
    {
        if (_compatHelpBox == null || _targetWeapon == null) return;
        var count = _targetWeapon.CompatibleAttachments.Count;
        _compatHelpBox.text = $"Auto-Match By Category: {(_targetWeapon.AutoMatchByCategory ? "Enabled" : "Disabled")}\nTotal Compatible Attachments: {count}";
    }

    private void OnSyncButtonClicked()
    {
        var all = WeaponEditorUtilities.FindAllProjectAttachments();
        _targetWeapon.SyncCompatibleAttachments(all);
        RefreshCompatibilitySummary();
        UpdateSimulationResults();
    }

    private void AddSeparator(VisualElement root)
    {
        var sep = new VisualElement
        {
            style =
            {
                height = 1,
                backgroundColor = new Color(0f, 0f, 0f, 0.15f),
                marginTop = 8,
                marginBottom = 8
            }
        };
        root.Add(sep);
    }
}
}
