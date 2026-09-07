namespace Project.Core.UI
{
    /// <summary>
    /// Represents any UI panel, modal, menu, or overlay that intercepts and blocks player inputs.
    /// Can be implemented by MonoBehaviours, UI Toolkit controllers, or state machines.
    /// </summary>
    public interface IInputBlocker
    {
        /// <summary>
        /// Human-readable identifier for debug logging and stack inspection.
        /// </summary>
        string BlockerId { get; }

        /// <summary>
        /// If true, disables player locomotion (WASD, sprinting, crouching, jumping).
        /// </summary>
        bool BlocksMovement { get; }

        /// <summary>
        /// If true, disables camera rotation (mouse look).
        /// </summary>
        bool BlocksLook { get; }

        /// <summary>
        /// If true, disables primary/secondary fire, abilities, reload, weapon switching, and interactions.
        /// </summary>
        bool BlocksActions { get; }

        /// <summary>
        /// If true, unlocks the hardware mouse cursor and makes it visible.
        /// </summary>
        bool UnlocksCursor { get; }

        /// <summary>
        /// If true, pressing Cancel (Escape / Controller B) will close this blocker.
        /// Set to false for non-dismissible or mandatory confirmation modals.
        /// </summary>
        bool ClosesOnCancel { get; }

        /// <summary>
        /// Requests this blocker to close itself.
        /// </summary>
        void Close();
    }
}
