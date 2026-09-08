using Project.Core.UI;
using UnityEngine;

namespace Project.UI
{
public class InventoryUI : InputBlockerUI
{
   [Header("References")]
   [SerializeField] private CanvasGroup _canvasGroup;
   
   
   public override void Open()
   {
      base.Open();
      Show();
   }

   public override void Close()
   {
      base.Close();
      Hide();
   }

   private void Show()
   {
      _canvasGroup.alpha = 1f;
      _canvasGroup.interactable = true;
      _canvasGroup.blocksRaycasts = true;
   }
   
   private void Hide()
   {
      _canvasGroup.alpha = 0f;
      _canvasGroup.interactable = false;
      _canvasGroup.blocksRaycasts = false;  
   }
}
}