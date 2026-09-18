using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
public class ProgressBarUI : MonoBehaviour
{
    [Header("Settings")] 
    [SerializeField] private float _fillDelayDuration = 0.5f;
    [SerializeField] private float _fillAnimationDuration = 0.5f;
    
    [Header("References")]
    [SerializeField] private Image _detailFillImage;
    [SerializeField] private Image _fillImage;
    
    public void SetFillAmount(float fillAmount)
    {
        _detailFillImage.fillAmount = fillAmount;
        _fillImage.fillAmount = fillAmount;
    }
}
}