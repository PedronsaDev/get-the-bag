namespace Project.Combat
{
public enum GunsmithSwapResult : byte
{
    Success = 0,
    WeaponNull = 1,
    SameWeapon = 2,
    SlotEmptyOnSource = 3,
    TargetSlotLocked = 4,
    SourceSlotLocked = 5,
    IncompatibleArchetype = 6,
    SlotNotSupported = 7
}

/// <summary>
/// Pure logic service that executes part transfers and swaps between two weapons at a Workbench Station.
/// Strictly validates signature locks, slot support on the weapon bodies, and weapon archetype compatibility.
/// </summary>
public static class GunsmithBenchService
{
    /// <summary>
    /// Validates whether a part in the given slot can be transferred from sourceWeapon to targetWeapon.
    /// If targetWeapon already has a part in that slot, it evaluates a mutual two-way swap.
    /// </summary>
    public static GunsmithSwapResult CanTransferOrSwap(WeaponInstance sourceWeapon, WeaponInstance targetWeapon,
        WeaponPartType slot)
    {
        if (sourceWeapon == null || targetWeapon == null)
            return GunsmithSwapResult.WeaponNull;

        if (ReferenceEquals(sourceWeapon, targetWeapon))
            return GunsmithSwapResult.SameWeapon;

        if (!sourceWeapon.TryGetPart(slot, out var sourcePart) || !sourcePart.IsValid)
            return GunsmithSwapResult.SlotEmptyOnSource;

        // Check if source part is locked (signature legendary part)
        if (sourcePart.IsLocked)
            return GunsmithSwapResult.SourceSlotLocked;

        // Check if target weapon supports this physical slot
        if (!targetWeapon.Core || !targetWeapon.Core.SupportsPartSlot(slot))
            return GunsmithSwapResult.SlotNotSupported;

        // Check archetype compatibility for the source part on target weapon
        if (!sourcePart.IsCompatibleWith(targetWeapon.Core.WeaponType))
            return GunsmithSwapResult.IncompatibleArchetype;

        // If target already has a part in this slot, check if it can be detached and fits on source
        if (targetWeapon.TryGetPart(slot, out var targetPart) && targetPart.IsValid)
        {
            if (targetPart.IsLocked)
                return GunsmithSwapResult.TargetSlotLocked;

            if (!targetPart.IsCompatibleWith(sourceWeapon.Core.WeaponType))
                return GunsmithSwapResult.IncompatibleArchetype;
        }

        return GunsmithSwapResult.Success;
    }

    /// <summary>
    /// Transfers or swaps the part in the specified slot between sourceWeapon and targetWeapon.
    /// Automatically recompiles stats and triggers assembly events on both weapons.
    /// </summary>
    public static GunsmithSwapResult TryTransferOrSwap(WeaponInstance sourceWeapon, WeaponInstance targetWeapon,
        WeaponPartType slot)
    {
        var check = CanTransferOrSwap(sourceWeapon, targetWeapon, slot);
        if (check != GunsmithSwapResult.Success)
            return check;

        sourceWeapon.TryGetPart(slot, out var sourcePart);
        bool targetHasPart = targetWeapon.TryGetPart(slot, out var targetPart);

        // Execute atomic transfer
        if (targetHasPart && targetPart.IsValid)
        {
            // Mutual swap
            sourceWeapon.TrySwapPart(slot, targetPart, out _);
            targetWeapon.TrySwapPart(slot, sourcePart, out _);
        }
        else
        {
            // Unilateral move from source to target
            sourceWeapon.TryDetachPart(slot, out _);
            targetWeapon.TrySwapPart(slot, sourcePart, out _);
        }

        return GunsmithSwapResult.Success;
    }
}
}