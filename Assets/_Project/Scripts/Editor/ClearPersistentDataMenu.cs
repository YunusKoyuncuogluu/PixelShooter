using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelShooter.Editor
{
    public static class ClearPersistentDataMenu
    {
        [MenuItem("PixelShooter/Open Persistent Data Path")]
        public static void OpenPersistentDataPath()
        {
            string path = Application.persistentDataPath;

            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("Open Persistent Data Path", "Persistent data folder was not found.", "OK");
                return;
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("PixelShooter/Clear Persistent Data")]
        public static void ClearPersistentData()
        {
            string path = Application.persistentDataPath;

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                EditorUtility.DisplayDialog("Clear Persistent Data", "Persistent data folder was not found.", "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Persistent Data",
                "Delete every file in persistent data?\n\n" + path,
                "Delete",
                "Cancel");

            if (!confirmed)
            {
                return;
            }

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }

            int deletedCount = 0;

            string[] files = Directory.GetFiles(path);
            for (int i = 0; i < files.Length; i++)
            {
                File.SetAttributes(files[i], FileAttributes.Normal);
                File.Delete(files[i]);
                deletedCount++;
            }

            string[] directories = Directory.GetDirectories(path);
            for (int i = 0; i < directories.Length; i++)
            {
                Directory.Delete(directories[i], true);
                deletedCount++;
            }

            Debug.Log("Persistent data cleared (" + deletedCount + " entries): " + path);
            EditorUtility.DisplayDialog("Clear Persistent Data", "Persistent data cleared.\n\n" + path, "OK");
        }
    }
}
