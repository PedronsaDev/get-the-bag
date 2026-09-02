using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PedronsaDev.Console;
using Project.Core.Persistence;
using UnityEngine;
using Console = PedronsaDev.Console.Console;

namespace Project.Persistence
{
    /// <summary>
    /// Central manager for loading and saving game profiles and dispatching state
    /// to registered IDataPersistence listeners across the game.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class DataPersistenceManager : MonoBehaviour
    {
        public static DataPersistenceManager Instance { get; private set; }

        [Header("Debugging")]
        [SerializeField] private bool _disableDataPersistence;
        [SerializeField] private bool _initializeDataIfNull = true;
        [SerializeField] private bool _overrideSelectedProfileId;
        [SerializeField] private string _testSelectedProfileId = "dev";

        [Header("File Storage Config")]
        [SerializeField] private string _fileName = "save.data";
        [SerializeField] private bool _useEncryption;

        private readonly SemaphoreSlim _saveSemaphore = new(1, 1);
        private readonly HashSet<IDataPersistence> _persistenceObjects = new();

        private GameData _gameData;
        private FileDataHandler _dataHandler;
        private string _selectedProfileId = "";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_disableDataPersistence)
            {
                Debug.LogWarning("[DataPersistenceManager] Data Persistence is currently disabled!");
            }

            _dataHandler = new FileDataHandler(Application.persistentDataPath + "/saves", _fileName, _useEncryption);
            InitializeSelectedProfileId();
        }

        private void Start()
        {
            LoadGame();
        }

        /// <summary>
        /// Registers a component to receive LoadData calls and supply data during SaveData calls.
        /// If game data is already loaded in memory, LoadData is triggered immediately.
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
                Debug.LogWarning($"[DataPersistenceManager] Overrode selected profile id with test id: {_testSelectedProfileId}");
            }
        }

        public void NewGame()
        {
            _gameData = new GameData();
        }

        [Command("load_game", "Loads the game data from the active profile")]
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
                Debug.LogWarning("[DataPersistenceManager] No data found. Start a new game before data can be loaded.");
                return;
            }

            // Dispatch loaded data to all registered listeners
            foreach (var persistenceObj in _persistenceObjects)
            {
                persistenceObj.LoadData(_gameData);
            }

            Console.LogCustom("DataPersistenceManager", $"Loaded profile '{_selectedProfileId}' successfully.", new Color(0.2f, 1f, 0.2f));
        }

        [Command("save_game", "Saves the game data to the active profile")]
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
                Debug.LogWarning("[DataPersistenceManager] No data to save. Initialize data before saving.");
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
                Console.LogCustom("DataPersistenceManager", $"Saved profile '{_selectedProfileId}' successfully.", Color.green);
            }
        }

        private void OnApplicationQuit()
        {
            SaveGameBlocking();
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
