using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.UI.Core
{
[DisallowMultipleComponent]
public class UIRoot : MonoBehaviour
{
    public static UIRoot Instance { get; private set; }

    [Header("Layered Canvases")] 
    [SerializeField] private Canvas _worldSpaceCanvas;
    [SerializeField] private Canvas _hudCanvas;
    [SerializeField] private Canvas _windowsCanvas;
    [SerializeField] private Canvas _overlayCanvas;

    [Header("Event System")] 
    [SerializeField] private EventSystem _eventSystem;

    public Canvas WorldSpaceCanvas => _worldSpaceCanvas;
    public Canvas HUDCanvas => _hudCanvas;
    public Canvas WindowsCanvas => _windowsCanvas;
    public Canvas OverlayCanvas => _overlayCanvas;
    public EventSystem EventSystem => _eventSystem;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance)
            return;

        var prefab = Resources.Load<GameObject>("UI/UIRoot");
        if (prefab)
        {
            var instance = Instantiate(prefab);
            instance.name = "[UIRoot]";
            DontDestroyOnLoad(instance);
        }
        else
        {
            Debug.LogWarning(
                "[UIRoot] Resources/UI/UIRoot prefab not found. UIRoot will not auto-bootstrap until the prefab is created in Resources/UI/.");
        }
    }

    private void Awake()
    {
        if (!Instance)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSingleEventSystem();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void EnsureSingleEventSystem()
    {
        var eventSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        for (int i = 0; i < eventSystems.Length; i++)
        {
            if (_eventSystem && eventSystems[i] != _eventSystem)
            {
                eventSystems[i].enabled = false;
                if (eventSystems[i].TryGetComponent(out BaseInputModule inputModule))
                {
                    inputModule.enabled = false;
                    Destroy(inputModule);
                }

                Destroy(eventSystems[i]);
            }
        }
    }
}
}