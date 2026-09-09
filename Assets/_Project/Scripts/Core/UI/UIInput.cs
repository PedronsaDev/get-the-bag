using System;
using UnityEngine;

namespace Project.Core.UI
{
    /// <summary>
    /// Global registry and event dispatcher for UI and meta inputs (Cancel, ToggleInventory, DropItem, FavoriteItem, TrashItem, DropAllTrash).
    /// Always remains active and responsive regardless of UIBlockerSystem state.
    /// </summary>
    public static class UIInput
    {
        public static event Action OnCancel;
        public static event Action OnToggleInventory;
        public static event Action OnDropItem;
        public static event Action OnFavoriteItem;
        public static event Action OnTrashItem;
        public static event Action OnDropAllTrash;

        public static bool CancelPressed { get; set; }
        public static bool ToggleInventoryPressed { get; set; }
        public static bool DropItemPressed { get; set; }
        public static bool FavoriteItemPressed { get; set; }
        public static bool TrashItemPressed { get; set; }
        public static bool DropAllTrashPressed { get; set; }

        public static void SetStates(
            bool cancel,
            bool toggleInventory,
            bool dropItem,
            bool favoriteItem,
            bool trashItem,
            bool dropAllTrash)
        {
            CancelPressed = cancel;
            ToggleInventoryPressed = toggleInventory;
            DropItemPressed = dropItem;
            FavoriteItemPressed = favoriteItem;
            TrashItemPressed = trashItem;
            DropAllTrashPressed = dropAllTrash;
        }

        public static void TriggerCancel() => OnCancel?.Invoke();
        public static void TriggerToggleInventory() => OnToggleInventory?.Invoke();
        public static void TriggerDropItem() => OnDropItem?.Invoke();
        public static void TriggerFavoriteItem() => OnFavoriteItem?.Invoke();
        public static void TriggerTrashItem() => OnTrashItem?.Invoke();
        public static void TriggerDropAllTrash() => OnDropAllTrash?.Invoke();
    }
}
