using UnityEngine;

namespace Project.Core
{
public class CameraRig : MonoBehaviour
{
    public static CameraRig Instance { get; private set; }
    
    [Header("References")]
    [SerializeField] private Camera _camera;
    
    private Transform _target;
    public Camera Camera => _camera;

    private void Awake()
    {
        if (!Instance)
        {
            Instance = this;
            return;
        }

        Debug.LogWarning($"CameraRig instance already exists. Destroying duplicate on {gameObject.name}");
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) 
            Instance = null;
    }

    private void LateUpdate()
    {
        if (_target) 
            transform.SetPositionAndRotation(_target.position, _target.rotation);
    }

    public void Follow(Transform target) => _target = target;

    public void StopFollowing() => _target = null;
}
}