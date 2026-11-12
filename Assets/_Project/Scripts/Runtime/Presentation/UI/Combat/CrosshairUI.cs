using PrimeTween;
using Project.Core;
using UnityEngine;

namespace Project.UI.Combat
{
public class CrosshairUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerWeaponEventChannel _weaponEventChannel;
    [SerializeField] private CanvasGroup _canvasGroup;

    private void OnEnable()
    {
        _weaponEventChannel.OnADSStarted += HandleADSStarted;
        _weaponEventChannel.OnADSEnded += HandleADSEnded;
    }

    // The channel is a ScriptableObject that outlives this scene, so it must drop the handlers or it keeps calling a destroyed UI.
    private void OnDisable()
    {
        _weaponEventChannel.OnADSStarted -= HandleADSStarted;
        _weaponEventChannel.OnADSEnded -= HandleADSEnded;
    }

    private void HandleADSStarted()
    {
        Tween.Alpha(_canvasGroup, 0f, 0.1f);
    }
    
    private void HandleADSEnded()
    {
        Tween.Alpha(_canvasGroup, 1f, 0.1f);
    }
}
}