using System.Collections.Generic;
using LevelManagement;
using UnityEditor;
using UnityEngine;

namespace PixelShooter.Editor
{

    public class LevelBakerEditorWindow : EditorWindow
    {
        private Texture2D sourceTexture;
        private LevelData targetLevel;
        private LevelPixelData targetPixelData;
        private ProjectSettings projectSettings;
        private bool useDownsampling = true;
        private Vector2Int targetResolution = new Vector2Int(32, 32);
        private bool applyPlateCount;
        private int plateCountOverride = 5;

        private readonly PixelBakeUtility bakeUtility = new PixelBakeUtility();
        private readonly FacadePaletteMapper paletteMapper = new FacadePaletteMapper();

        [MenuItem("Tools/PixelShooter/Level Baker")]
        public static void Open()
        {
            LevelBakerEditorWindow window = GetWindow<LevelBakerEditorWindow>("Level Baker");
            window.minSize = new Vector2(400f, 400f);
            window.Show();
        }

        private void OnEnable()
        {
            if (projectSettings == null)
                projectSettings = ProjectSettings.Load();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("PixelShooter Level Baker (paletteId bake)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bake: yakin tonlar ayni facade Level (id) alir (kirmizi tonlari → Red, pembe → Pink). " +
                "paletteId = FacadeProperty.Level. Runtime sarma sadece bu int id ile eslesir; RGB yok.",
                MessageType.Info);
            EditorGUILayout.Space(4f);

            projectSettings = (ProjectSettings)EditorGUILayout.ObjectField(
                "Project Settings",
                projectSettings,
                typeof(ProjectSettings),
                false);

            sourceTexture = (Texture2D)EditorGUILayout.ObjectField(
                "Source Texture",
                sourceTexture,
                typeof(Texture2D),
                false);

            targetLevel = (LevelData)EditorGUILayout.ObjectField(
                "Target LevelData (optional)",
                targetLevel,
                typeof(LevelData),
                false);

            targetPixelData = (LevelPixelData)EditorGUILayout.ObjectField(
                "Target LevelPixelData (optional)",
                targetPixelData,
                typeof(LevelPixelData),
                false);

            EditorGUILayout.Space(8f);
            useDownsampling = EditorGUILayout.Toggle("Use Downsampling", useDownsampling);

            using (new EditorGUI.DisabledScope(!useDownsampling))
            {
                targetResolution = EditorGUILayout.Vector2IntField("Target Resolution", targetResolution);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Plate (LevelData)", EditorStyles.boldLabel);
            applyPlateCount = EditorGUILayout.Toggle("Set Plate Count On Bake", applyPlateCount);
            using (new EditorGUI.DisabledScope(!applyPlateCount || targetLevel == null))
            {
                plateCountOverride = EditorGUILayout.IntSlider("Plate Count", plateCountOverride, 1, 20);
            }

            EditorGUILayout.Space(12f);

            bool hasTarget = targetLevel != null || targetPixelData != null;
            bool canBake = sourceTexture != null
                           && projectSettings != null
                           && hasTarget
                           && (!useDownsampling || (targetResolution.x > 0 && targetResolution.y > 0));

            using (new EditorGUI.DisabledScope(!canBake))
            {
                if (GUILayout.Button("Bake (paletteId + Color32)", GUILayout.Height(32f)))
                    Bake();
            }

            using (new EditorGUI.DisabledScope(targetLevel == null || !applyPlateCount))
            {
                if (GUILayout.Button("Apply Plate Count Only", GUILayout.Height(24f)))
                    ApplyPlateCountToTargetLevel();
            }
        }

        private void ApplyPlateCountToTargetLevel()
        {
            if (targetLevel == null)
                return;

            SerializedObject so = new SerializedObject(targetLevel);
            SerializedProperty plateProp = so.FindProperty("plateCount");
            if (plateProp == null)
                return;

            plateProp.intValue = Mathf.Max(1, plateCountOverride);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetLevel);
            AssetDatabase.SaveAssets();
        }

        private void Bake()
        {
            if (sourceTexture == null || projectSettings == null)
            {
                EditorUtility.DisplayDialog("Bake Failed", "Texture ve ProjectSettings zorunlu.", "OK");
                return;
            }

            if (targetLevel == null && targetPixelData == null)
            {
                EditorUtility.DisplayDialog(
                    "Bake Failed",
                    "En az bir hedef secin: LevelData ve/veya LevelPixelData.",
                    "OK");
                return;
            }

            if (!sourceTexture.isReadable)
            {
                EditorUtility.DisplayDialog(
                    "Bake Failed",
                    "Texture Read/Write kapali. Import Settings'ten ac.",
                    "OK");
                return;
            }

            if (useDownsampling && (targetResolution.x <= 0 || targetResolution.y <= 0))
            {
                EditorUtility.DisplayDialog("Bake Failed", "Target Resolution > 0 olmali.", "OK");
                return;
            }

            paletteMapper.BuildFromFacades(projectSettings.GetOriginalFacadeProperties());
            if (paletteMapper.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Bake Failed",
                    "Facade listesi bos veya gecerli Level (>0) yok.",
                    "OK");
                return;
            }

            Color32[] rawPixels = sourceTexture.GetPixels32();
            int width = sourceTexture.width;
            int height = sourceTexture.height;

            List<PixelData> bakedPixels;
            Vector2Int savedSize;

            if (useDownsampling)
            {
                bakedPixels = bakeUtility.BakeDownsampled(rawPixels, width, height, targetResolution);
                savedSize = targetResolution;
            }
            else
            {
                bakedPixels = bakeUtility.BakeFullResolution(rawPixels, width, height);
                savedSize = new Vector2Int(width, height);
            }

            bakeUtility.CollapseToDominantColors(bakedPixels);
            paletteMapper.AssignPaletteIds(bakedPixels);
            rawPixels = null;

            if (targetPixelData != null)
            {
                targetPixelData.SetPixels(bakedPixels, savedSize);
                EditorUtility.SetDirty(targetPixelData);
            }

            if (targetLevel != null)
            {
                targetLevel.ApplyPixelBake(bakedPixels, savedSize);
                if (applyPlateCount)
                {
                    SerializedObject so = new SerializedObject(targetLevel);
                    SerializedProperty plateProp = so.FindProperty("plateCount");
                    if (plateProp != null)
                    {
                        plateProp.intValue = Mathf.Max(1, plateCountOverride);
                        so.ApplyModifiedProperties();
                    }
                }

                EditorUtility.SetDirty(targetLevel);
            }

            AssetDatabase.SaveAssets();
            if (targetLevel != null)
                EditorGUIUtility.PingObject(targetLevel);
            else
                EditorGUIUtility.PingObject(targetPixelData);

            string modeLabel = useDownsampling ? $"downsampled {savedSize.x}x{savedSize.y}" : "full resolution";
        }
    }
}
