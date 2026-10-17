using LevelManagement;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace PixelShooter.Editor
{
    [CustomEditor(typeof(LevelData))]
    public class LevelDataEditor : OdinEditor
    {
        private static readonly string[] tabLabels = new string[]
        {
            "Level Settings",
            "Pixel & Palette",
            "Grid & Shooters"
        };

        private static readonly string[] levelSettingsProperties = new string[]
        {
            "#Base Level Data",
            "#Slot",
            "#Plate"
        };

        private static readonly string[] pixelPaletteProperties = new string[]
        {
            "#Image Pixel"
        };

        private static readonly string[] gridShooterProperties = new string[]
        {
            "#Grid",
            "#Grid Helper"
        };

        private const string SelectedTabSessionKey = "PixelShooter.LevelDataEditor.SelectedTab";
        private const string LevelDatabasePath = "Assets/_Project/Data/LevelDatabase.asset";

        public override void OnInspectorGUI()
        {
            LevelData levelData = (LevelData)this.target;
            DrawLevelNavigation(levelData);

            int selectedTab = SessionState.GetInt(SelectedTabSessionKey, 0);
            if (selectedTab < 0 || selectedTab >= tabLabels.Length)
            {
                selectedTab = 0;
            }

            selectedTab = GUILayout.Toolbar(selectedTab, tabLabels);
            SessionState.SetInt(SelectedTabSessionKey, selectedTab);
            EditorGUILayout.Space(6f);

            levelData.BeginEditorDraw();

            try
            {
                InspectorUtilities.BeginDrawPropertyTree(this.Tree, true);
                levelData.BeginEditorDraw();

                switch (selectedTab)
                {
                    case 0:
                    {
                        DrawProperties(levelSettingsProperties);
                        break;
                    }

                    case 1:
                    {
                        DrawProperties(pixelPaletteProperties);
                        break;
                    }

                    case 2:
                    {
                        DrawProperties(gridShooterProperties);
                        break;
                    }
                }

                DrawRootProperty("SyncPalettePixelsInInspector");

                InspectorUtilities.EndDrawPropertyTree(this.Tree);
            }
            finally
            {
                levelData.EndEditorDraw();
            }
        }

        private void DrawLevelNavigation(LevelData levelData)
        {
            LevelData[] levels = LoadOrderedLevels();
            int index = FindLevelIndex(levels, levelData);
            string currentLabel = levelData.name;

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(index <= 0))
            {
                if (GUILayout.Button("Previous Level", GUILayout.Height(24f)))
                {
                    SelectLevel(levels[index - 1]);
                }
            }

            if (GUILayout.Button(currentLabel, GUILayout.Height(24f)))
            {
                SelectLevel(levelData);
            }

            using (new EditorGUI.DisabledScope(index < 0 || index >= levels.Length - 1))
            {
                if (GUILayout.Button("Next Level", GUILayout.Height(24f)))
                {
                    SelectLevel(levels[index + 1]);
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6f);
        }

        private static void SelectLevel(LevelData levelData)
        {
            if (levelData == null)
            {
                return;
            }

            Selection.activeObject = levelData;
            EditorGUIUtility.PingObject(levelData);
            GUIUtility.ExitGUI();
        }

        private static LevelData[] LoadOrderedLevels()
        {
            LevelDatabase database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(LevelDatabasePath);
            if (database == null)
            {
                return new LevelData[0];
            }

            SerializedObject serializedDatabase = new SerializedObject(database);
            SerializedProperty levelsProperty = serializedDatabase.FindProperty("levels");
            if (levelsProperty == null || !levelsProperty.isArray)
            {
                return new LevelData[0];
            }

            int count = levelsProperty.arraySize;
            LevelData[] levels = new LevelData[count];
            int writeIndex = 0;
            for (int i = 0; i < count; i++)
            {
                LevelData level = levelsProperty.GetArrayElementAtIndex(i).objectReferenceValue as LevelData;
                if (level == null)
                {
                    continue;
                }

                levels[writeIndex] = level;
                writeIndex++;
            }

            if (writeIndex == count)
            {
                return levels;
            }

            LevelData[] compactLevels = new LevelData[writeIndex];
            for (int i = 0; i < writeIndex; i++)
            {
                compactLevels[i] = levels[i];
            }

            return compactLevels;
        }

        private static int FindLevelIndex(LevelData[] levels, LevelData levelData)
        {
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] == levelData)
                {
                    return i;
                }
            }

            return -1;
        }

        private void DrawProperties(string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                DrawRootProperty(propertyNames[i]);
            }
        }

        private void DrawRootProperty(string propertyName)
        {
            InspectorProperty property = FindRootProperty(propertyName);
            if (property == null)
            {
                EditorGUILayout.HelpBox("Missing inspector section: " + propertyName, MessageType.Warning);
                return;
            }

            property.Draw();
        }

        private InspectorProperty FindRootProperty(string propertyName)
        {
            InspectorProperty direct = this.Tree.GetPropertyAtPath(propertyName);
            if (direct != null)
            {
                return direct;
            }

            string groupName = propertyName.StartsWith("#") ? propertyName.Substring(1) : propertyName;
            foreach (InspectorProperty property in this.Tree.EnumerateTree(false, false))
            {
                if (property.Name == propertyName || property.Path == propertyName || property.Name == groupName)
                {
                    return property;
                }
            }

            return null;
        }
    }
}
