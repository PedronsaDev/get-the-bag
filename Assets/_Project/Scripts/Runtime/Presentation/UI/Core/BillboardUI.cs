using Project.Core;
using UnityEngine;

namespace Project.UI.Core
{
public class BillboardUI : MonoBehaviour
{
    private Transform _mainCameraTransform;

    void Start()
    {
        _mainCameraTransform = CameraRig.Instance.Camera.transform;
    }

    void LateUpdate()
    {
        if (_mainCameraTransform)
        {
            transform.rotation = _mainCameraTransform.rotation;
        }
    }
}
}