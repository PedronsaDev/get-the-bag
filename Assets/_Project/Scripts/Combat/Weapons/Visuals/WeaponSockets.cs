using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Standardized socket transform identifiers for modular 3D weapon meshes.
    /// </summary>
    public static class WeaponSocketNames
    {
        public const string Barrel = "socket_barrel";
        public const string Grip = "socket_grip";
        public const string Stock = "socket_stock";
        public const string Sight = "socket_sight";
        public const string Magazine = "socket_mag";
        public const string Accessory = "socket_accessory";
        public const string Muzzle = "socket_muzzle";
        public const string MuzzleAttachment = "socket_muzzle_attachment";
        public const string Silencer = "socket_silencer";
        public const string FirePoint = "fire_point";
        public const string Ejector = "socket_ejector";
        public const string Underbarrel = "socket_underbarrel_attachment";
        public const string SideRail = "socket_side_rail";

        /// <summary>
        /// Common socket and child transform names designating the barrel tip / projectile spawn point.
        /// </summary>
        public static readonly string[] MuzzleAliases =
        {
            "socket_muzzle",
            "socket_muzzle_attachment",
            "socket_silencer",
            "fire_point",
            "firepoint",
            "muzzle",
            "muzzle_socket",
            "barrel_tip"
        };
    }
}
