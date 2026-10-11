using LevelManagement;
using UnityEditor;
using UnityEngine;

namespace RopeWrap.Editor
{

    public class PlateCountBatchTool
    {
        private const int DefaultPlateCount = 5;

        [MenuItem("Tools/RopeWrap/Set Default Plate Count On All Levels")]
        private static void SetDefaultPlateCountOnAllLevels()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/Resources/Levels" });
            int updated = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level == null)
                    continue;

                SerializedObject so = new SerializedObject(level);
                SerializedProperty plateProp = so.FindProperty("plateCount");
                if (plateProp == null)
                    continue;

                if (plateProp.intValue > 0)
                    continue;

                plateProp.intValue = DefaultPlateCount;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(level);
                updated++;
            }

            AssetDatabase.SaveAssets();
        }
    }
}
