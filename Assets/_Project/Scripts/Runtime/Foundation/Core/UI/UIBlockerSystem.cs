using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.UI
{
    public static class UIBlockerSystem
    {
        private static readonly List<IInputBlocker> _activeBlockers = new();

        /// <summary>
        /// Delegate defining what cursor state should be restored when NO UI blockers require an unlocked cursor.
        /// Configured by LocalPlayerInputProvider (e.g. lock cursor if player alive, leave unlocked if dead/lobby).
        /// </summary>
        public static Action CursorLockPolicy { get; set; }

        public static event Action<IInputBlocker> OnBlockerRegistered;
        public static event Action<IInputBlocker> OnBlockerUnregistered;
        public static event Action<bool> OnBlockerStateChanged;
        public static event Action OnBlockerStackChanged;

        public static IReadOnlyList<IInputBlocker> ActiveBlockers
        {
            get
            {
                PruneDeadBlockers();
                return _activeBlockers;
            }
        }

        public static int ActiveBlockerCount
        {
            get
            {
                PruneDeadBlockers();
                return _activeBlockers.Count;
            }
        }

        public static bool HasActiveBlocker
        {
            get
            {
                PruneDeadBlockers();
                return _activeBlockers.Count > 0;
            }
        }

        public static IInputBlocker TopBlocker
        {
            get
            {
                PruneDeadBlockers();
                return _activeBlockers.Count > 0 ? _activeBlockers[^1] : null;
            }
        }

        public static bool IsMovementBlocked
        {
            get
            {
                PruneDeadBlockers();
                for (int i = 0; i < _activeBlockers.Count; i++)
                {
                    if (_activeBlockers[i].BlocksMovement) return true;
                }
                return false;
            }
        }

        public static bool IsLookBlocked
        {
            get
            {
                PruneDeadBlockers();
                for (int i = 0; i < _activeBlockers.Count; i++)
                {
                    if (_activeBlockers[i].BlocksLook) return true;
                }
                return false;
            }
        }

        public static bool IsActionsBlocked
        {
            get
            {
                PruneDeadBlockers();
                for (int i = 0; i < _activeBlockers.Count; i++)
                {
                    if (_activeBlockers[i].BlocksActions) return true;
                }
                return false;
            }
        }

        public static bool IsCursorUnlocked
        {
            get
            {
                PruneDeadBlockers();
                for (int i = 0; i < _activeBlockers.Count; i++)
                {
                    if (_activeBlockers[i].UnlocksCursor) return true;
                }
                return false;
            }
        }

        public static bool IsInputBlocked => IsMovementBlocked || IsLookBlocked || IsActionsBlocked;

        /// <summary>
        /// Registers an input blocker and activates input suppression / cursor unlock.
        /// If already present, moves the blocker to the top of the LIFO stack.
        /// </summary>
        public static void Register(IInputBlocker blocker)
        {
            if (IsDead(blocker)) return;

            bool wasBlockedBefore = HasActiveBlocker;

            _activeBlockers.Remove(blocker);
            _activeBlockers.Add(blocker);

            RefreshCursorState();

            OnBlockerRegistered?.Invoke(blocker);
            OnBlockerStackChanged?.Invoke();

            if (!wasBlockedBefore && HasActiveBlocker)
            {
                OnBlockerStateChanged?.Invoke(true);
            }
        }

        /// <summary>
        /// Unregisters an input blocker. If no blockers remain, restores gameplay input and cursor lock.
        /// </summary>
        public static void Unregister(IInputBlocker blocker)
        {
            if (blocker == null) return;

            bool wasBlockedBefore = HasActiveBlocker;

            if (_activeBlockers.Remove(blocker))
            {
                RefreshCursorState();

                OnBlockerUnregistered?.Invoke(blocker);
                OnBlockerStackChanged?.Invoke();

                if (wasBlockedBefore && !HasActiveBlocker)
                {
                    OnBlockerStateChanged?.Invoke(false);
                }
            }
        }

        /// <summary>
        /// Attempts to close the topmost blocker on the stack (LIFO order).
        /// Respects the blocker's ClosesOnCancel policy.
        /// Returns true if a blocker was closed.
        /// </summary>
        public static bool TryCloseTopBlocker()
        {
            PruneDeadBlockers();

            for (int i = _activeBlockers.Count - 1; i >= 0; i--)
            {
                var candidate = _activeBlockers[i];
                if (IsDead(candidate)) continue;

                if (candidate.ClosesOnCancel)
                {
                    candidate.Close();
                    return true;
                }

                // If the top blocker explicitly forbids closing on cancel (e.g. mandatory modal),
                // do not pass the Cancel event to blockers below it.
                return false;
            }

            return false;
        }

        public static void ClearAll()
        {
            bool wasBlocked = HasActiveBlocker;
            _activeBlockers.Clear();

            RefreshCursorState();

            OnBlockerStackChanged?.Invoke();
            if (wasBlocked)
            {
                OnBlockerStateChanged?.Invoke(false);
            }
        }

        public static void RefreshCursorState()
        {
            if (IsCursorUnlocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                if (CursorLockPolicy != null)
                {
                    CursorLockPolicy.Invoke();
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        private static void PruneDeadBlockers()
        {
            bool removedAny = false;
            for (int i = _activeBlockers.Count - 1; i >= 0; i--)
            {
                if (IsDead(_activeBlockers[i]))
                {
                    _activeBlockers.RemoveAt(i);
                    removedAny = true;
                }
            }

            if (removedAny)
            {
                RefreshCursorState();
                OnBlockerStackChanged?.Invoke();
                if (_activeBlockers.Count == 0)
                {
                    OnBlockerStateChanged?.Invoke(false);
                }
            }
        }

        private static bool IsDead(IInputBlocker blocker)
        {
            if (blocker == null) return true;
            if (blocker is UnityEngine.Object unityObj && !unityObj) return true;
            return false;
        }
    }
}
