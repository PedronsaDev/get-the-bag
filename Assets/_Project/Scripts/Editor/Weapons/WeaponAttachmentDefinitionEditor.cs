using System;
using System.Collections.Generic;
using Project.Combat.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Weapons.Editor
{
[CustomEditor(typeof(WeaponAttachmentDefinition))]
public class WeaponAttachmentDefinitionEditor : UnityEditor.Editor
{
    private WeaponAttachmentDefinition _targetAttachment;
    private WeaponDefinition _previewWeapon;

    private HelpBox _headerHelpBox;
    private HelpBox _previewCompatHelpBox;
    private VisualElement _previewTable;

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
        _targetAttachment = (WeaponAttachmentDefinition)target;
        var allWeapons = WeaponEditorUtilities.FindAllProjectWeapons();
        if (allWeapons.Count > 0 && _previewWeapon == null)
        {
            for (var i = 0; i < allWeapons.Count; i++)
            {
                if (_targetAttachment.IsCompatibleWith(allWeapons[i].Category))
                {
                    _previewWeapon = allWeapons[i];
                    break;
                }
            }
            if (_previewWeapon == null) _previewWeapon = allWeapons[0];
        }
    }

    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.style.paddingTop = 4;
        root.style.paddingBottom = 8;

        // Simple native info header
        _headerHelpBox = new HelpBox($"Slot: {_targetAttachment.Slot}  |  Categories: {_targetAttachment.CompatibleCategories}", HelpBoxMessageType.None);
        _headerHelpBox.style.marginBottom = 8;
        root.Add(_headerHelpBox);

        // Core Configuration Properties
        var prop = serializedObject.GetIterator();
        if (prop.NextVisible(true))
        {
            do
            {
                if (prop.name is "m_Script") continue;

                var field = new PropertyField(prop);
                field.Bind(serializedObject);

                field.RegisterValueChangeCallback(_ =>
                {
                    serializedObject.ApplyModifiedProperties();
                    _headerHelpBox.text = $"Slot: {_targetAttachment.Slot}  |  Categories: {_targetAttachment.CompatibleCategories}";
                    UpdatePreviewResults();
                });

                root.Add(field);
            }
            while (prop.NextVisible(false));
        }

        AddSeparator(root);

        // Preview On Weapon Foldout
        var previewFoldout = new Foldout { text = "Preview on Weapon", value = true };
        previewFoldout.style.marginBottom = 6;

        var previewBox = new Box();
        previewBox.style.paddingLeft = 8;
        previewBox.style.paddingRight = 8;
        previewBox.style.paddingTop = 8;
        previewBox.style.paddingBottom = 8;
        previewBox.style.marginBottom = 6;

        var allWeapons = WeaponEditorUtilities.FindAllProjectWeapons();
        var weaponChoices = new List<string>();
        var weaponMap = new List<WeaponDefinition>();

        var selectedIndex = 0;
        for (var i = 0; i < allWeapons.Count; i++)
        {
            var w = allWeapons[i];
            weaponChoices.Add($"{w.Name} ({w.Category})");
            weaponMap.Add(w);
            if (w == _previewWeapon) selectedIndex = i;
        }

        if (weaponChoices.Count > 0)
        {
            var weaponDropdown = new PopupField<string>("Select Weapon", weaponChoices, selectedIndex);
            weaponDropdown.style.marginBottom = 6;
            weaponDropdown.RegisterValueChangedCallback(evt =>
            {
                var idx = weaponChoices.IndexOf(evt.newValue);
                if (idx >= 0 && idx < weaponMap.Count)
                {
                    _previewWeapon = weaponMap[idx];
                    UpdatePreviewResults();
                }
            });
            previewBox.Add(weaponDropdown);
        }

        _previewCompatHelpBox = new HelpBox("", HelpBoxMessageType.Info);
        _previewCompatHelpBox.style.marginBottom = 6;
        previewBox.Add(_previewCompatHelpBox);

        _previewTable = new VisualElement();
        BuildPreviewTable(_previewTable);
        previewBox.Add(_previewTable);

        var syncAllBtn = new Button(WeaponEditorUtilities.SyncAllWeaponsInProject) { text = "Sync All Weapons in Project", tooltip = "Applies compatibility changes to all weapons across the project." };
        syncAllBtn.style.height = 24;
        syncAllBtn.style.marginTop = 6;
        previewBox.Add(syncAllBtn);

        previewFoldout.Add(previewBox);
        root.Add(previewFoldout);

        UpdatePreviewResults();

        return root;
    }

    private void BuildPreviewTable(VisualElement table)
    {
        table.Clear();
        var headerRow = CreateTableRow("Stat", "Base", "With Mod", "Delta", true);
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

    private void UpdatePreviewResults()
    {
        if (_previewWeapon == null || _targetAttachment == null) return;

        var isCompat = _targetAttachment.IsCompatibleWith(_previewWeapon.Category);
        if (_previewCompatHelpBox != null)
        {
            if (isCompat)
            {
                _previewCompatHelpBox.text = $"Compatible with {_previewWeapon.Name} ({_previewWeapon.Category})";
                _previewCompatHelpBox.messageType = HelpBoxMessageType.Info;
            }
            else
            {
                _previewCompatHelpBox.text = $"Incompatible with {_previewWeapon.Name} ({_previewWeapon.Category} is not in allowed mask)";
                _previewCompatHelpBox.messageType = HelpBoxMessageType.Warning;
            }
        }

        var resolved = CalculatePreviewStats(_previewWeapon, _targetAttachment);

        // Damage
        FormatDelta(_damageValueLabel, _damageDeltaLabel, _previewWeapon.BaseDamage, resolved.Damage, false, "");

        // Magazine Size
        FormatDelta(_magValueLabel, _magDeltaLabel, _previewWeapon.BaseMagazineSize, resolved.MagazineSize, false, " rds");

        // Reload Duration (lower is better!)
        FormatDeltaFloat(_reloadValueLabel, _reloadDeltaLabel, _previewWeapon.BaseReloadDuration, resolved.ReloadDuration, true, "s");

        // Fire Rate
        FormatDelta(_fireRateValueLabel, _fireRateDeltaLabel, _previewWeapon.BaseFireRate, resolved.FireRate, false, " RPM");

        // DPS
        var baseDps = _previewWeapon.BaseDamage * (_previewWeapon.BaseFireRate / 60f);
        var resolvedDps = resolved.Damage * (resolved.FireRate / 60f);
        FormatDeltaFloat(_dpsValueLabel, _dpsDeltaLabel, baseDps, resolvedDps, false, "");

        // Range
        FormatDeltaFloat(_rangeValueLabel, _rangeDeltaLabel, _previewWeapon.BaseRange, resolved.Range, false, "m");
    }

    private WeaponResolvedStats CalculatePreviewStats(WeaponDefinition weapon, WeaponAttachmentDefinition attachment)
    {
        var damage = Math.Clamp((weapon.BaseDamage + attachment.Damage.Add) * (1 + attachment.Damage.Percent), 0, int.MaxValue);
        var mag = Math.Clamp((weapon.BaseMagazineSize + attachment.MagazineSize.Add) * (1 + attachment.MagazineSize.Percent), 1, int.MaxValue);
        var reload = Math.Clamp((weapon.BaseReloadDuration + attachment.ReloadDuration.Add) * (1 + attachment.ReloadDuration.Percent), 0.01, float.MaxValue);
        var fireRate = Math.Clamp((weapon.BaseFireRate + attachment.FireRate.Add) * (1 + attachment.FireRate.Percent), 1, int.MaxValue);
        var range = Math.Clamp((weapon.BaseRange + attachment.Range.Add) * (1 + attachment.Range.Percent), 0.01, float.MaxValue);

        return new WeaponResolvedStats(
            (int)Math.Round(damage, MidpointRounding.AwayFromZero),
            (int)Math.Round(mag, MidpointRounding.AwayFromZero),
            (float)reload,
            (int)Math.Round(fireRate, MidpointRounding.AwayFromZero),
            (float)range);
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
