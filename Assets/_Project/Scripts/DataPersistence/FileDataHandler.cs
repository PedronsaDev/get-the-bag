using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Persistence;
using UnityEngine;
using Console = PedronsaDev.Console.Console;

namespace Project.Persistence
{
    /// <summary>
    /// Handles low-level file I/O operations, disk storage paths, XOR encryption,
    /// backup generation, and corrupted file rollback.
    /// </summary>
    public class FileDataHandler
    {
        private readonly string _dataDirPath;
        private readonly string _dataFileName;
        private readonly bool _useEncryption;
        private readonly string _encryptionCodeWord = "word";
        private readonly string _backupExtension = ".bak";

        public FileDataHandler(string dataDirPath, string dataFileName, bool useEncryption)
        {
            _dataDirPath = dataDirPath;
            _dataFileName = dataFileName;
            _useEncryption = useEncryption;
        }

        public GameData Load(string profileId, bool allowRestoreFromBackup = true)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return null;
            }

            string fullPath = Path.Combine(_dataDirPath, profileId, _dataFileName);
            GameData loadedData = null;
            if (File.Exists(fullPath))
            {
                try
                {
                    string dataToLoad = File.ReadAllText(fullPath);
                    if (_useEncryption)
                    {
                        dataToLoad = EncryptDecrypt(dataToLoad);
                    }

                    loadedData = JsonUtility.FromJson<GameData>(dataToLoad);
                }
                catch (Exception e)
                {
                    if (allowRestoreFromBackup)
                    {
                        Debug.LogWarning($"[FileDataHandler] Failed to load data file. Attempting rollback.\n{e}");
                        bool rollbackSuccess = AttemptRollback(fullPath);
                        if (rollbackSuccess)
                        {
                            loadedData = Load(profileId, false);
                        }
                    }
                    else
                    {
                        Debug.LogError($"[FileDataHandler] Error loading file at path: {fullPath} and backup restore failed.\n{e}");
                    }
                }
            }

            return loadedData;
        }

        public async UniTask<GameData> LoadAsync(string profileId, bool allowRestoreFromBackup = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return null;
            }

            string fullPath = Path.Combine(_dataDirPath, profileId, _dataFileName);
            GameData loadedData = null;
            if (File.Exists(fullPath))
            {
                try
                {
                    string dataToLoad = await ReadAllTextAsync(fullPath, cancellationToken);
                    if (_useEncryption)
                    {
                        dataToLoad = EncryptDecrypt(dataToLoad);
                    }

                    loadedData = JsonUtility.FromJson<GameData>(dataToLoad);
                }
                catch (Exception e)
                {
                    if (allowRestoreFromBackup)
                    {
                        Debug.LogWarning($"[FileDataHandler] Failed to load data file async. Attempting rollback.\n{e}");
                        bool rollbackSuccess = AttemptRollback(fullPath);
                        if (rollbackSuccess)
                        {
                            loadedData = await LoadAsync(profileId, false, cancellationToken);
                        }
                    }
                    else
                    {
                        Debug.LogError($"[FileDataHandler] Error loading file async at path: {fullPath} and backup restore failed.\n{e}");
                    }
                }
            }

            return loadedData;
        }

        public void Save(GameData data, string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                Debug.LogWarning("[FileDataHandler] Tried to save data with an invalid profile id.");
                return;
            }

            string fullPath = Path.Combine(_dataDirPath, profileId, _dataFileName);
            string backupFilePath = fullPath + _backupExtension;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? "");

                string dataToStore = JsonUtility.ToJson(data, true);
                if (_useEncryption)
                {
                    dataToStore = EncryptDecrypt(dataToStore);
                }

                File.WriteAllText(fullPath, dataToStore);

                GameData verifiedGameData = Load(profileId, true);
                if (verifiedGameData != null)
                {
                    File.Copy(fullPath, backupFilePath, true);
                }
                else
                {
                    throw new Exception("Save file could not be verified and backup could not be created.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileDataHandler] Error saving data to file: {fullPath}\n{e}");
            }
        }

        public async UniTask SaveAsync(GameData data, string profileId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                Debug.LogWarning("[FileDataHandler] Tried to save data with an invalid profile id.");
                return;
            }

            string fullPath = Path.Combine(_dataDirPath, profileId, _dataFileName);
            string backupFilePath = fullPath + _backupExtension;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? "");

                string dataToStore = JsonUtility.ToJson(data, true);
                if (_useEncryption)
                {
                    dataToStore = EncryptDecrypt(dataToStore);
                }

                await WriteAllTextAsync(fullPath, dataToStore, cancellationToken);

                GameData verifiedGameData = await LoadAsync(profileId, true, cancellationToken);
                if (verifiedGameData != null)
                {
                    File.Copy(fullPath, backupFilePath, true);
                }
                else
                {
                    throw new Exception("Save file could not be verified and backup could not be created.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileDataHandler] Error saving data async to file: {fullPath}\n{e}");
            }
        }

        public void Delete(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return;
            }

            string fullPath = Path.Combine(_dataDirPath, profileId, _dataFileName);
            try
            {
                if (File.Exists(fullPath))
                {
                    Directory.Delete(Path.GetDirectoryName(fullPath) ?? "", true);
                }
                else
                {
                    Console.LogWarning($"[FileDataHandler] Tried to delete profile data, but data was not found at path: {fullPath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileDataHandler] Failed to delete profile data for profileId: {profileId} at path: {fullPath}\n{e}");
            }
        }

        public Dictionary<string, GameData> LoadAllProfiles()
        {
            Dictionary<string, GameData> profileDictionary = new Dictionary<string, GameData>();
            if (!Directory.Exists(_dataDirPath))
            {
                return profileDictionary;
            }

            IEnumerable<DirectoryInfo> dirInfos = new DirectoryInfo(_dataDirPath).EnumerateDirectories();
            foreach (DirectoryInfo dirInfo in dirInfos)
            {
                string profileId = dirInfo.Name;
                string fullPath = Path.Combine(_dataDirPath, profileId, _dataFileName);
                if (!File.Exists(fullPath))
                {
                    Debug.LogWarning($"[FileDataHandler] Skipping directory when loading all profiles because it does not contain data: {profileId}");
                    continue;
                }

                GameData profileData = Load(profileId);
                if (profileData != null)
                {
                    profileDictionary.Add(profileId, profileData);
                }
                else
                {
                    Debug.LogError($"[FileDataHandler] Tried to load profile but something went wrong. ProfileId: {profileId}");
                }
            }

            return profileDictionary;
        }

        public string GetMostRecentlyUpdatedProfileId()
        {
            string mostRecentProfileId = null;

            Dictionary<string, GameData> profilesGameData = LoadAllProfiles();
            foreach (KeyValuePair<string, GameData> pair in profilesGameData)
            {
                string profileId = pair.Key;
                GameData gameData = pair.Value;

                if (gameData == null)
                {
                    continue;
                }

                if (mostRecentProfileId == null)
                {
                    mostRecentProfileId = profileId;
                }
                else
                {
                    DateTime mostRecentDateTime = DateTime.FromBinary(profilesGameData[mostRecentProfileId].LastUpdated);
                    DateTime newDateTime = DateTime.FromBinary(gameData.LastUpdated);
                    if (newDateTime > mostRecentDateTime)
                    {
                        mostRecentProfileId = profileId;
                    }
                }
            }

            return mostRecentProfileId;
        }

        private string EncryptDecrypt(string data)
        {
            StringBuilder modifiedData = new StringBuilder(data.Length);
            for (int i = 0; i < data.Length; i++)
            {
                modifiedData.Append((char)(data[i] ^ _encryptionCodeWord[i % _encryptionCodeWord.Length]));
            }

            return modifiedData.ToString();
        }

        private bool AttemptRollback(string fullPath)
        {
            bool success = false;
            string backupFilePath = fullPath + _backupExtension;
            try
            {
                if (File.Exists(backupFilePath))
                {
                    File.Copy(backupFilePath, fullPath, true);
                    success = true;
                    Debug.LogWarning($"[FileDataHandler] Rolled back to backup file at: {backupFilePath}");
                }
                else
                {
                    throw new Exception("Tried to roll back, but no backup file exists.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileDataHandler] Error rolling back to backup file at: {backupFilePath}\n{e}");
            }

            return success;
        }

        private static async UniTask<string> ReadAllTextAsync(string fullPath, CancellationToken cancellationToken)
        {
            using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (StreamReader reader = new StreamReader(stream))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await reader.ReadToEndAsync().AsUniTask();
            }
        }

        private static async UniTask WriteAllTextAsync(string fullPath, string dataToStore, CancellationToken cancellationToken)
        {
            using (FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            using (StreamWriter writer = new StreamWriter(stream))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await writer.WriteAsync(dataToStore).AsUniTask();
            }
        }
    }
}