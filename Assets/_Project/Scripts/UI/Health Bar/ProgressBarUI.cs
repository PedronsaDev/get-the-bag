using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.ProgressBar
{
public class ProgressBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image _detailFillImage;
    [SerializeField] private Image _fillImage;
    
    public void SetFillAmount(float fillAmount)
    {
        _fillImage.fillAmount = fillAmount;
    }
}
}