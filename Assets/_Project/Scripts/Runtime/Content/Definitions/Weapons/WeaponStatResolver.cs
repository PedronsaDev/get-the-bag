using System;
using System.Collections.Generic;
using Project.Logging;

namespace Project.Combat.Data
{
public static class WeaponStatResolver
{
    public static WeaponResolvedStats Resolve(WeaponDefinition weapon, IReadOnlyList<string> attachmentIds = null)
        => Resolve(weapon, attachmentIds, out _, false);

    public static WeaponResolvedStats Resolve(WeaponDefinition weapon, IReadOnlyList<string> attachmentIds,
        out string[] canonicalIds)
        => Resolve(weapon, attachmentIds, out canonicalIds, false);

    public static WeaponResolvedStats Resolve(WeaponDefinition weapon, IReadOnlyList<string> attachmentIds,
        out string[] canonicalIds, bool strictValidation)
    {
        ValidateBaseStats(weapon);
        var compatible = new Dictionary<string, WeaponAttachmentDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var attachment in weapon.CompatibleAttachments)
        {
            if (!attachment || string.IsNullOrWhiteSpace(attachment.Id))
            {
                if (strictValidation)
                    throw new ArgumentException($"Weapon '{weapon.Id}' has an empty compatible attachment.");
                AppLog.LogWarning(LogChannel.Combat, $"Weapon '{weapon.Id}' has an unassigned compatible attachment entry.", weapon);
                continue;
            }

            if (!weapon.IsAttachmentCompatible(attachment)) continue;

            if (!compatible.TryAdd(attachment.Id, attachment))
            {
                if (strictValidation)
                    throw new ArgumentException($"Weapon '{weapon.Id}' lists duplicate attachment ID '{attachment.Id}'.");
                AppLog.LogWarning(LogChannel.Combat, $"Weapon '{weapon.Id}' lists duplicate attachment ID '{attachment.Id}'.", weapon);
            }
        }

        var selected = new List<WeaponAttachmentDefinition>();
        var slots = new HashSet<WeaponAttachmentSlot>();
        if (attachmentIds != null)
        {
            foreach (var id in attachmentIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    if (strictValidation) throw new ArgumentException("Attachment IDs cannot be empty.");
                    continue;
                }

                if (!compatible.TryGetValue(id, out var attachment))
                {
                    if (WeaponDefinition.AttachmentLookup != null)
                    {
                        var lookedUp = WeaponDefinition.AttachmentLookup(id);
                        if (lookedUp && string.Equals(lookedUp.Id, id, StringComparison.OrdinalIgnoreCase) &&
                            weapon.IsAttachmentCompatible(lookedUp))
                        {
                            attachment = lookedUp;
                            compatible[id] = attachment;
                        }
                    }
                }

                if (!attachment)
                {
                    if (strictValidation)
                        throw new ArgumentException($"Attachment '{id}' is not compatible with weapon '{weapon.Id}'.");
                    AppLog.LogWarning(LogChannel.Combat, $"Attachment '{id}' is not compatible with weapon '{weapon.Id}'. Ignoring.", weapon);
                    continue;
                }

                if (!Enum.IsDefined(typeof(WeaponAttachmentSlot), attachment.Slot))
                {
                    if (strictValidation)
                        throw new ArgumentException($"Attachment '{id}' has an invalid slot.");
                    AppLog.LogWarning(LogChannel.Combat, $"Attachment '{id}' has an invalid slot. Ignoring.", weapon);
                    continue;
                }

                if (!slots.Add(attachment.Slot))
                {
                    if (strictValidation)
                        throw new ArgumentException($"Only one attachment may occupy slot '{attachment.Slot}'.");
                    AppLog.LogWarning(LogChannel.Combat, $"Duplicate attachment for slot '{attachment.Slot}' on weapon '{weapon.Id}'. Ignoring.", weapon);
                    continue;
                }

                selected.Add(attachment);
            }
        }

        selected.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        canonicalIds = new string[selected.Count];
        for (var i = 0; i < selected.Count; i++) canonicalIds[i] = selected[i].Id;

        return new WeaponResolvedStats(
            Round(ResolveValue(weapon.BaseDamage, selected, a => a.Damage, 0, int.MaxValue)),
            Round(ResolveValue(weapon.BaseMagazineSize, selected, a => a.MagazineSize, 1, int.MaxValue)),
            (float)ResolveValue(weapon.BaseReloadDuration, selected, a => a.ReloadDuration, 0.01, float.MaxValue),
            Round(ResolveValue(weapon.BaseFireRate, selected, a => a.FireRate, 1, int.MaxValue)),
            (float)ResolveValue(weapon.BaseRange, selected, a => a.Range, 0.01, float.MaxValue));
    }

    private static double ResolveValue(double baseValue, List<WeaponAttachmentDefinition> attachments,
        Func<WeaponAttachmentDefinition, WeaponStatModifier> select, double minimum, double maximum)
    {
        double add = 0, percent = 0;
        foreach (var attachment in attachments)
        {
            var modifier = select(attachment);
            if (!float.IsFinite(modifier.Add) || !float.IsFinite(modifier.Percent))
                throw new ArgumentException($"Attachment '{attachment.Id}' has a nonfinite modifier.");
            add += modifier.Add;
            percent += modifier.Percent;
        }
        return Math.Clamp((baseValue + add) * (1 + percent), minimum, maximum);
    }

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static void ValidateBaseStats(WeaponDefinition weapon)
    {
        if (!weapon) throw new ArgumentNullException(nameof(weapon));
        if (string.IsNullOrWhiteSpace(weapon.Id) || weapon.BaseDamage < 0 || weapon.BaseMagazineSize < 1 ||
            weapon.BaseFireRate < 1 || !float.IsFinite(weapon.BaseReloadDuration) || weapon.BaseReloadDuration <= 0 ||
            !float.IsFinite(weapon.BaseRange) || weapon.BaseRange <= 0)
            throw new ArgumentException($"Weapon '{weapon.Id}' has invalid base stats.", nameof(weapon));
    }
}
}
