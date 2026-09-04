using System;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Represents a weapon/item manufacturer brand.
    /// Brands impart visual color themes, inherent stat tendencies, and signature gameplay traits.
    /// </summary>
    [CreateAssetMenu(fileName = "Manufacturer_New", menuName = "Combat/Items/Manufacturer")]
    public class ManufacturerDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _manufacturerId = "Brand_A";
        [SerializeField] private string _displayName = "Titan Arms";
        [SerializeField, TextArea(2, 3)] private string _slogan = "Unmatched raw firepower.";
        [SerializeField, TextArea(2, 3)] private string _brandTraitDescription = "Heavy kinetic impact with higher base damage.";

        [Header("Visual Branding")]
        [SerializeField] private Sprite _brandIcon;
        [SerializeField] private Color _primaryColor = Color.gray;
        [SerializeField] private Color _accentColor = Color.red;
        [SerializeField] private Material _brandPaletteMaterial;

        public string ManufacturerId => _manufacturerId;
        public string DisplayName => _displayName;
        public string Slogan => _slogan;
        public string BrandTraitDescription => _brandTraitDescription;
        public Sprite BrandIcon => _brandIcon;
        public Color PrimaryColor => _primaryColor;
        public Color AccentColor => _accentColor;
        public Material BrandPaletteMaterial => _brandPaletteMaterial;
    }
}
