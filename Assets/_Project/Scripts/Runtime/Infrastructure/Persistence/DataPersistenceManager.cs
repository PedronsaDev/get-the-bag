using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Persistence;
using Project.Logging;
using UnityEngine;

namespace Project.Persistence
{
/// <summary>
/// Gerenciador central para carregar e salvar perfis de jogo e despachar estado para os
/// ouvintes registrados de IDataPersistence no jogo.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class DataPersistenceManager : MonoBehaviour
{
    private static DataPersistenceManager _instance;
    private static bool _isQuitting;

    public static bool HasInstance => _instance && !_isQuitting;

    public static DataPersistenceManager Instance
    {
        get
        {
            if (_isQuitting)
            {
                return null;
            }

            if (!_instance)
            {
                _instance = FindAnyObjectByType<DataPersistenceManager>();
                if (!_instance)
                {
                    if (!Application.isPlaying)
                    {
                        return null;
                    }

                    var go = new GameObject("[DataPersistenceManager]");
                    _instance = go.AddComponent<DataPersistenceManager>();
                    DontDestroyOnLoad(go);
                }
            }

            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Debugging")] 
    [SerializeField] private bool _disableDataPersistence;
    [SerializeField] private bool _initializeDataIfNull = true;
    [SerializeField] private bool _overrideSelectedProfileId;
    [SerializeField] private string _testSelectedProfileId = "dev";

    [Header("File Storage Config")] 
    [SerializeField] private string _fileName = "game.sav";
    [SerializeField] private bool _useEncryption;

    private readonly SemaphoreSlim _saveSemaphore = new(1, 1);
    private readonly HashSet<IDataPersistence> _persistenceObjects = new();

    private GameData _gameData;
    private FileDataHandler _dataHandler;
    private string _selectedProfileId = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _isQuitting = false;
    }

    private void Awake()
    {
        if (_instance && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        Application.quitting -= HandleApplicationQuitting;
        Application.quitting += HandleApplicationQuitting;

        if (_disableDataPersistence)
        {
            AppLog.LogWarning(LogChannel.Persistence, "Data Persistence is currently disabled!");
        }

        _dataHandler = new FileDataHandler(Application.persistentDataPath + "/saves", _fileName, _useEncryption);
        InitializeSelectedProfileId();
    }

    private void Start()
    {
        //LoadGame();
    }

    /// <summary>
    /// Registra um componente para receber chamadas LoadData e fornecer dados durante as chamadas SaveData.
    /// Se os dados do jogo já estiverem carregados na memória, LoadData é chamado instantaneamente.
    /// </summary>
    public void Register(IDataPersistence persistenceObj)
    {
        if (persistenceObj == null) return;

        if (_persistenceObjects.Add(persistenceObj))
        {
            if (_gameData != null)
            {
                persistenceObj.LoadData(_gameData);
            }
        }
    }

    /// <summary>
    /// Unregisters a component from the persistence lifecycle.
    /// </summary>
    public void Unregister(IDataPersistence persistenceObj)
    {
        if (persistenceObj != null)
        {
            _persistenceObjects.Remove(persistenceObj);
        }
    }

    public void ChangeSelectedProfileId(string newProfileId)
    {
        _selectedProfileId = newProfileId;
        LoadGame();
    }

    public void DeleteProfileData(string profileId)
    {
        _dataHandler.Delete(profileId);
        InitializeSelectedProfileId();
        LoadGame();
    }

    private void InitializeSelectedProfileId()
    {
        _selectedProfileId = _dataHandler.GetMostRecentlyUpdatedProfileId();

        if (string.IsNullOrEmpty(_selectedProfileId))
        {
            _selectedProfileId = "default";
        }

        if (_overrideSelectedProfileId)
        {
            _selectedProfileId = _testSelectedProfileId;
            AppLog.LogWarning(LogChannel.Persistence,
                $"Overrode selected profile id with test id: {_testSelectedProfileId}");
        }
    }

    public void NewGame()
    {
        _gameData = new GameData();
    }

    public void LoadGame()
    {
        if (_disableDataPersistence)
        {
            return;
        }

        _gameData = _dataHandler.Load(_selectedProfileId);

        if (_gameData == null && _initializeDataIfNull)
        {
            NewGame();
        }

        if (_gameData == null)
        {
            AppLog.LogWarning(LogChannel.Persistence, "[DataPersistenceManager] No data found. Start a new game before data can be loaded.");
            return;
        }

        // Dispatch loaded data to all registered listeners
        foreach (var persistenceObj in _persistenceObjects)
        {
            persistenceObj.LoadData(_gameData);
        }
        
        AppLog.Log(LogChannel.Persistence, $"Loaded profile '{_selectedProfileId}' successfully.");
    }

    public void SaveGame()
    {
        SaveGameAsync().Forget(Debug.LogException);
    }

    public async UniTask SaveGameAsync(CancellationToken cancellationToken = default)
    {
        if (_disableDataPersistence)
        {
            return;
        }

        if (_gameData == null)
        {
            AppLog.LogWarning(LogChannel.Persistence, "[DataPersistenceManager] No data to save. Initialize data before saving.");
            return;
        }

        await _saveSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Gather data from all registered listeners
            foreach (var persistenceObj in _persistenceObjects)
            {
                persistenceObj.SaveData(_gameData);
            }

            _gameData.LastUpdated = DateTime.UtcNow.ToBinary();
            await _dataHandler.SaveAsync(_gameData, _selectedProfileId, cancellationToken);
        }
        finally
        {
            _saveSemaphore.Release();
            AppLog.Log(LogChannel.Persistence, $"Saved profile '{_selectedProfileId}' successfully.");
        }
    }

    private static void HandleApplicationQuitting()
    {
        _isQuitting = true;
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;
        SaveGameBlocking();
    }

    private void OnDestroy()
    {
        Application.quitting -= HandleApplicationQuitting;
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public bool HasGameData()
    {
        return _gameData != null;
    }

    public GameData GetCurrentGameData()
    {
        return _gameData;
    }

    public Dictionary<string, GameData> GetAllProfilesGameData()
    {
        return _dataHandler.LoadAllProfiles();
    }

    private void SaveGameBlocking()
    {
        if (_disableDataPersistence || _gameData == null)
        {
            return;
        }

        foreach (var persistenceObj in _persistenceObjects)
        {
            persistenceObj.SaveData(_gameData);
        }

        _gameData.LastUpdated = DateTime.UtcNow.ToBinary();
        _dataHandler.Save(_gameData, _selectedProfileId);
    }
}
}