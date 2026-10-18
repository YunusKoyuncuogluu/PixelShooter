using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace PixelShooter.Save
{
    public class SaveManager : IDisposable
    {
        private const string FileName = "player_data.json";
        private const string TempFileName = "player_data.json.tmp";
        private const string BackupFileName = "player_data.json.bak";

        private readonly string filePath;
        private readonly string tempFilePath;
        private readonly string backupFilePath;
        private readonly UTF8Encoding utf8Encoding;

        public PlayerData Data { get; private set; }

        public SaveManager()
        {
            string directory = Application.persistentDataPath;
            filePath = Path.Combine(directory, FileName);
            tempFilePath = Path.Combine(directory, TempFileName);
            backupFilePath = Path.Combine(directory, BackupFileName);
            utf8Encoding = new UTF8Encoding(false);

            LoadFromDisk();
        }

        public void Dispose() => SaveToDisk();

        public void SaveToDisk()
        {
            if (Data == null)
                Data = CreateDefault();

            Sanitize(Data);
            string json = JsonUtility.ToJson(Data);
            WriteTempFile(json);
            PromoteTempFile();
        }

        public void Delete()
        {
            Data = CreateDefault();
            DeleteIfExists(filePath);
            DeleteIfExists(tempFilePath);
            DeleteIfExists(backupFilePath);
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
                Data = backup;
                Sanitize(Data);
                return;
            }

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
                    return false;
                }

                data = loaded;
                return true;
            }
            catch (Exception)
            {
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
                return;

            File.Move(backupFilePath, filePath);
        }

        private void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private void Sanitize(PlayerData data)
        {
            if (data.highestUnlockedLevel < 1)
                data.highestUnlockedLevel = 1;

            if (data.coins < 0)
                data.coins = 0;

            if (data.settingsVersion < 1)
            {
                data.soundEnabled = true;
                data.vibrationEnabled = true;
                data.settingsVersion = 1;
            }
        }

        private PlayerData CreateDefault()
        {
            return new PlayerData();
        }
    }
}
