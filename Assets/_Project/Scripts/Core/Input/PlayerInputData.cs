using UnityEngine;

namespace Project.Core.Input
{
    /// <summary>
    /// Pure, unmanaged snapshot of player input for a single frame/tick.
    /// Completely decoupled from Unity's hardware input devices.
    /// </summary>
    public struct PlayerInputData
    {
        public Vector2 MoveVector;
        public Vector2 LookVector;
        
        public bool JumpPressed;
        public bool JumpHeld;
        public bool SprintHeld;
        public bool CrouchHeld;
        
        public bool PrimaryFirePressed;
        public bool PrimaryFireHeld;
        public bool SecondaryFirePressed;
        public bool SecondaryFireHeld;
        
        public bool ReloadPressed;
        public bool InteractPressed;
        public bool GrenadePressed;
        public bool SkillPressed;
        public bool MeleePressed;
        public bool PingPressed;
        
        /// <summary>
        /// 0 = None, 1 = Slot 1, 2 = Slot 2, 3 = Slot 3, 4 = Slot 4
        /// </summary>
        public int WeaponSlotPressed;

        public static PlayerInputData Empty => default;
    }
}
