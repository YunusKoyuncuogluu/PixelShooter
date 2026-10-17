using System;
using System.IO;
using System.Text;
using UnityEngine;
using Zenject;

namespace PixelShooter.Save
{
    public class SaveManager : IInitializable, IDisposable
    {
        private const string FileName = "player_data.json";
        private const string TempFileName = "player_data.json.tmp";
        private const string BackupFileName = "player_data.json.bak";

        private readonly string filePath;
        private readonly string tempFilePath;
        private readonly string backupFilePath;
        private readonly UTF8Encoding utf8Encoding;

        private readonly bool canUseDisk;
        private bool isListening;
        private bool isCommitInProgress;

        public PlayerData Data { get; private set; }

        public SaveManager()
        {
            string directory = Application.persistentDataPath;
            filePath = Path.Combine(directory, FileName);
            tempFilePath = Path.Combine(directory, TempFileName);
            backupFilePath = Path.Combine(directory, BackupFileName);
            utf8Encoding = new UTF8Encoding(false);
            canUseDisk = Application.isPlaying;

            if (!canUseDisk)
            {
                Data = CreateDefault();
                return;
            }

            LoadFromDisk();
        }

        public void Initialize()
        {
            if (isListening)
            {
                return;
            }

            Application.quitting += OnQuitting;
            Application.focusChanged += OnFocusChanged;
            isListening = true;
        }

        public void Dispose()
        {
            if (isListening)
            {
                Application.quitting -= OnQuitting;
                Application.focusChanged -= OnFocusChanged;
                isListening = false;
            }

            CommitToDisk();
        }

        public void CommitToDisk()
        {
            if (!canUseDisk || isCommitInProgress)
            {
                return;
            }

            if (Data == null)
            {
                Data = CreateDefault();
            }

            Sanitize(Data);
            isCommitInProgress = true;

            try
            {
                string json = JsonUtility.ToJson(Data);
                WriteTempFile(json);
                PromoteTempFile();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Player data could not be committed. The in-memory copy is unchanged. {exception}");
            }
            finally
            {
                isCommitInProgress = false;
            }
        }

        public void Delete()
        {
            Data = CreateDefault();

            try
            {
                DeleteIfExists(filePath);
                DeleteIfExists(tempFilePath);
                DeleteIfExists(backupFilePath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Player data files could not be deleted. {exception}");
            }
        }

        private void OnQuitting()
        {
            CommitToDisk();
        }

        private void OnFocusChanged(bool hasFocus)
        {
            if (hasFocus)
            {
                return;
            }

            CommitToDisk();
        }

        private void LoadFromDisk()
        {
            if (TryReadFile(filePath, out PlayerData primary))
            {
                Data = primary;
                Sanitize(Data);
                return;
            }

            if (TryReadFile(backupFilePath, out PlayerData backup))
            {
                Debug.LogWarning("Primary player data was missing or invalid. Loaded the backup save.");
                Data = backup;
                Sanitize(Data);
                return;
            }

            Debug.Log("No player data file was found. Using default data.");
            Data = CreateDefault();
        }

        private bool TryReadFile(string path, out PlayerData data)
        {
            data = null;

            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                PlayerData loaded = JsonUtility.FromJson<PlayerData>(json);
                if (loaded == null)
                {
                    Debug.LogError($"Player data at '{path}' is not valid JSON.");
                    return false;
                }

                data = loaded;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Player data at '{path}' could not be read. {exception}");
                return false;
            }
        }

        private void WriteTempFile(string json)
        {
            using (FileStream stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (StreamWriter writer = new StreamWriter(stream, utf8Encoding, 1024, true))
                {
                    writer.Write(json);
                    writer.Flush();
                }

                stream.Flush(true);
            }
        }

        private void PromoteTempFile()
        {
            if (!File.Exists(filePath))
            {
                File.Move(tempFilePath, filePath);
                return;
            }

            DeleteIfExists(backupFilePath);
            File.Move(filePath, backupFilePath);

            try
            {
                File.Move(tempFilePath, filePath);
            }
            catch (Exception)
            {
                RestorePrimaryFromBackup();
                throw;
            }
        }

        private void RestorePrimaryFromBackup()
        {
            if (File.Exists(filePath) || !File.Exists(backupFilePath))
            {
                return;
            }

            try
            {
                File.Move(backupFilePath, filePath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Player data backup could not be restored after a failed commit. {exception}");
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void Sanitize(PlayerData data)
        {
            if (data.highestUnlockedLevel < 1)
            {
                data.highestUnlockedLevel = 1;
            }

            if (data.coins < 0)
            {
                data.coins = 0;
            }

            if (data.settingsVersion < 1)
            {
                data.soundEnabled = true;
                data.vibrationEnabled = true;
                data.settingsVersion = 1;
            }
        }

        private static PlayerData CreateDefault()
        {
            return new PlayerData();
        }
    }
}
