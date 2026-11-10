namespace Project.Core.UI
{
    public interface IInputBlocker
    {
        string BlockerId { get; }

        bool BlocksMovement { get; }

        bool BlocksLook { get; }

        bool BlocksActions { get; }

        bool UnlocksCursor { get; }

        /// <summary>
        /// If true, pressing Cancel (Escape / Controller B) will close this blocker.
        /// Set to false for non-dismissible or mandatory confirmation modals.
        /// </summary>
        bool ClosesOnCancel { get; }

        void Close();
    }
}
