using UnityEngine;

namespace Project.Combat
{
    [CreateAssetMenu(fileName = "Perk_AmmoRefund", menuName = "Combat/Perks/Ammo Refund (The Butcher)")]
    public class AmmoRefundPerkDefinition : WeaponPerkDefinition
    {
        [Header("Refund Parameters")]
        [Range(0.05f, 1.0f)]
        [Tooltip("Probability of refunding ammunition instead of consuming it from the magazine.")]
        [SerializeField] private float _refundChance = 0.35f;

        public float RefundChance => _refundChance;

        public override bool OnPreFire(WeaponInstance weapon, WeaponContext context)
        {
            if (Random.value < _refundChance)
            {
                // Refund successful! Return true to tell PlayerWeaponController not to decrement magazine
                return true;
            }

            return false;
        }
    }
}
