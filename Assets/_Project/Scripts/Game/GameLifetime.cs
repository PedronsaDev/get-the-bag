using System;
using Cysharp.Threading.Tasks;
using Project.Game;
using Project.Logging;
using UnityEngine;

public class GameLifetime : MonoBehaviour
{
    public static GameLifetime Instance { get; private set; }
    private UniTaskCompletionSource _quitCompletionSource;
    private Func<UniTask> _stopSession;
    public bool IsStopping => _quitCompletionSource != null;
    public void ConfigureSessionShutdown(Func<UniTask> stopSession) => _stopSession = stopSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public UniTask QuitAsync()
    {
        if (_quitCompletionSource == null)
        {
            _quitCompletionSource = new UniTaskCompletionSource();
            QuitApplicationAsync().Forget();
        }
        return _quitCompletionSource.Task;
    }

    public void Quit() => QuitAsync().Forget(exception =>
        AppLog.LogException(LogChannel.General, exception, this));

    private async UniTask QuitApplicationAsync()
    {
        try
        {
            if (_stopSession != null)
                await _stopSession();
            await GameContent.ShutdownAsync();
            _quitCompletionSource.TrySetResult();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        catch (Exception exception)
        {
            _quitCompletionSource.TrySetException(exception);
        }
    }

    private void OnApplicationQuit()
    {
        if (Instance == this)
            GameContent.RequestShutdown();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;
        Instance = null;
        GameContent.RequestShutdown();
    }
}
