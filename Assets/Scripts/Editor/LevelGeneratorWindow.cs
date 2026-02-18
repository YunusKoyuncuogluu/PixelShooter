using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Grids;
using LevelManagement;
using UnityEditor;
using UnityEngine;

namespace RopeWrap.Editor
{
    /// <summary>
    /// Üst açık, alt kapalı. Stitch grid: hep renk, duvar yok; karışık ama puzzle uyumlu. Normal grid 5x5'ten 10x10'a.
    /// Ara duvarlar (L, T) ile zorlaştırma. Makara*3 = ip.
    /// </summary>
    public class LevelGeneratorWindow : EditorWindow
    {
        private const string LevelsFolder = "Assets/Resources/Levels";
        private const int StitchWidth = 3;
        private const int StitchesPerSpool = 3;
        private const int MinGridSize = 5;
        private const int MaxGridSize = 10;

        [MenuItem("Tools/Generate 100 Levels")]
        public static void Open()
        {
            GetWindow<LevelGeneratorWindow>("Level Generator").Show();
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Generate 100 Levels", GUILayout.Height(32)))
            {
                GenerateAll();
            }
        }

        private static void GenerateAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Levels"))
                AssetDatabase.CreateFolder("Assets/Resources", "Levels");

            for (int n = 1; n <= 100; n++)
            {
                LevelData level = GenerateLevel(n);
                string path = $"{LevelsFolder}/Level_{n}.asset";
                AssetDatabase.CreateAsset(level, path);
                EditorUtility.DisplayProgressBar("Generate Levels", $"Level {n}/100", n / 100f);
            }
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Generated 100 levels in {LevelsFolder}");
        }

        private static int GetGridSizeForLevel(int levelIndex)
        {
            if (levelIndex <= 20) return 5;
            if (levelIndex <= 40) return 6;
            if (levelIndex <= 60) return 7;
            if (levelIndex <= 80) return 8;
            if (levelIndex <= 95) return 9;
            return 10;
        }

        private static LevelData GenerateLevel(int levelIndex)
        {
            int gridW = GetGridSizeForLevel(levelIndex);
            int gridH = gridW;

            GetDifficultyParams(levelIndex, out int numColors, out int numSpools, out int numTunnels, out int numQuestions, out int numInternalWalls);
            numSpools = Math.Max(numSpools, numColors);
            int totalStitches = numSpools * StitchesPerSpool;
            int[] stitchesPerColor = DistributeStitchesByColor(numSpools, numColors, out int[] spoolCountPerColor);

            int[] chosenLevels = GetChosenFacadeLevels(numColors);
            if (chosenLevels == null || chosenLevels.Length != numColors)
                chosenLevels = FallbackLevels(numColors);

            int stitchHeight = Math.Max(1, totalStitches / StitchWidth);
            int[,] stitchGrid = BuildStitchGridW3(stitchHeight, stitchesPerColor, chosenLevels, totalStitches);

            LevelData level = ScriptableObject.CreateInstance<LevelData>();
            SetLevelDataViaReflection(level, levelIndex, gridW, gridH, stitchGrid, stitchHeight,
                spoolCountPerColor, chosenLevels, numSpools, numTunnels, numQuestions, numInternalWalls);
            SyncFacadeProperties(level);
            return level;
        }

        /// <summary> Zorluk: ilk leveller daha az makara ve basit; sonra giderek artar. </summary>
        private static void GetDifficultyParams(int levelIndex, out int numColors, out int numSpools, out int numTunnels, out int numQuestions, out int numInternalWalls)
        {
            if (levelIndex <= 5)
            {
                numColors = 2;
                numSpools = UnityEngine.Random.Range(2, 4);
                numTunnels = 0;
                numQuestions = 0;
                numInternalWalls = 0;
            }
            else if (levelIndex <= 10)
            {
                numColors = 2;
                numSpools = UnityEngine.Random.Range(2, 5);
                numTunnels = UnityEngine.Random.Range(0, 2);
                numQuestions = UnityEngine.Random.Range(0, 2);
                numInternalWalls = 0;
            }
            else if (levelIndex <= 40)
            {
                numColors = 3;
                numSpools = UnityEngine.Random.Range(3, 6);
                numTunnels = UnityEngine.Random.Range(1, 3);
                numQuestions = UnityEngine.Random.Range(0, 3);
                numInternalWalls = UnityEngine.Random.Range(0, 2);
            }
            else if (levelIndex <= 80)
            {
                numColors = 4;
                numSpools = UnityEngine.Random.Range(4, 6);
                numTunnels = UnityEngine.Random.Range(1, 4);
                numQuestions = UnityEngine.Random.Range(1, 4);
                numInternalWalls = UnityEngine.Random.Range(1, 3);
            }
            else
            {
                numColors = 5;
                numSpools = UnityEngine.Random.Range(5, 6);
                numTunnels = UnityEngine.Random.Range(2, 5);
                numQuestions = UnityEngine.Random.Range(1, 5);
                numInternalWalls = UnityEngine.Random.Range(1, 4);
            }
        }

        private static int[] DistributeStitchesByColor(int totalSpools, int numColors, out int[] spoolCountPerColor)
        {
            spoolCountPerColor = new int[numColors + 1];
            int remainder = totalSpools;
            for (int c = 1; c < numColors; c++)
            {
                int n = UnityEngine.Random.Range(1, Math.Max(2, remainder - (numColors - c)));
                spoolCountPerColor[c] = n;
                remainder -= n;
            }
            spoolCountPerColor[numColors] = remainder;

            int[] stitchesPerColor = new int[numColors + 1];
            for (int c = 1; c <= numColors; c++)
                stitchesPerColor[c] = spoolCountPerColor[c] * StitchesPerSpool;
            return stitchesPerColor;
        }

        /// <summary> ProjectSettings FacadeProperties'ten numColors kadar rastgele Level seçer (1-14 arası, daha çok kombinasyon). </summary>
        private static int[] GetChosenFacadeLevels(int numColors)
        {
            var list = FacadeProperty.GetOriginalFacadeProperties();
            if (list == null || list.Count == 0) return null;
            var levels = new List<int>();
            foreach (var f in list)
            {
                if (f != null && f.Level >= 1)
                    levels.Add(f.Level);
            }
            if (levels.Count < numColors) return null;
            Shuffle(levels);
            var chosen = new int[numColors];
            for (int i = 0; i < numColors; i++)
                chosen[i] = levels[i];
            return chosen;
        }

        private static int[] FallbackLevels(int numColors)
        {
            var a = new int[numColors];
            for (int i = 0; i < numColors; i++) a[i] = i + 1;
            return a;
        }

        /// <summary> Stitch grid: her zaman 3 genişlik, duvar yok; tüm hücreler renk (chosenLevels), karışık dizilim, puzzle uyumlu. </summary>
        private static int[,] BuildStitchGridW3(int height, int[] stitchesPerColor, int[] chosenLevels, int totalStitches)
        {
            const int w = StitchWidth;
            int numColors = chosenLevels.Length;
            var colorList = new List<int>();
            for (int c = 0; c < numColors; c++)
                for (int k = 0; k < stitchesPerColor[c + 1]; k++)
                    colorList.Add(chosenLevels[c]);
            Shuffle(colorList);

            var grid = new int[w, height];
            int idx = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    grid[x, y] = idx < colorList.Count ? colorList[idx++] : chosenLevels[UnityEngine.Random.Range(0, numColors)];
                }
            }
            return grid;
        }

        /// <summary> Grid: alt (y=0), sol (x=0), sağ (x=w-1) kapalı. Üst sıra (y=h-1) açık. chosenLevels = FacadeProperty.Level değerleri (1-14). </summary>
        private static void SetLevelDataViaReflection(LevelData level, int levelIndex, int gridW, int gridH,
            int[,] stitchGrid, int stitchHeight,
            int[] spoolCountPerColor, int[] chosenLevels, int totalSpools, int numTunnels, int numQuestions, int numInternalWalls)
        {
            int numColors = chosenLevels.Length;
            Type levelType = typeof(LevelData);
            Type baseType = typeof(ScriptableLevelBase);

            SetField(levelType, level, "m_Width", gridW);
            SetField(levelType, level, "m_Height", gridH);
            SetField(levelType, level, "m_StitchWidth", StitchWidth);
            SetField(levelType, level, "m_StitchHeight", stitchHeight);

            // Oyun grid'i ters çevirerek çiziyor: veride y=0 ekranda alt (kapalı), y=height-1 ekranda üst (açık). Üst açık kalmalı.
            bool IsWall(int x, int y) => y == 0 || x == 0 || x == gridW - 1;
            List<Vector2Int> interior = new List<Vector2Int>();
            for (int x = 1; x < gridW - 1; x++)
                for (int y = 1; y < gridH; y++)
                    interior.Add(new Vector2Int(x, y));
            Shuffle(interior);

            CellData[,] cellData = new CellData[gridW, gridH];
            for (int x = 0; x < gridW; x++)
                for (int y = 0; y < gridH; y++)
                    cellData[x, y] = new CellData();

            for (int x = 0; x < gridW; x++)
                for (int y = 0; y < gridH; y++)
                    if (IsWall(x, y))
                        cellData[x, y].ChangeLocked(true);

            AddInternalWalls(cellData, gridW, gridH, interior, IsWall, numInternalWalls);

            List<int> spoolColors = new List<int>();
            for (int c = 0; c < numColors; c++)
                for (int k = 0; k < spoolCountPerColor[c + 1]; k++)
                    spoolColors.Add(chosenLevels[c]);
            Shuffle(spoolColors);

            int used = 0;
            for (int i = 0; i < interior.Count && used < totalSpools; i++)
            {
                Vector2Int p = interior[i];
                if (cellData[p.x, p.y].IsLocked) continue;
                cellData[p.x, p.y].ColorLevel = spoolColors[used];
                used++;
            }

            HashSet<Vector2Int> usedPos = new HashSet<Vector2Int>();
            for (int x = 0; x < gridW; x++)
                for (int y = 0; y < gridH; y++)
                    if (cellData[x, y].ColorLevel != 0)
                        usedPos.Add(new Vector2Int(x, y));

            List<Vector2Int> freeForTunnelOrQuestion = interior.Where(p => !usedPos.Contains(p) && !cellData[p.x, p.y].IsLocked).ToList();
            Shuffle(freeForTunnelOrQuestion);

            var tunnelCells = new List<Vector2Int>();
            var questionCells = new List<Vector2Int>();
            int t = 0, q = 0;
            for (int i = 0; i < freeForTunnelOrQuestion.Count; i++)
            {
                if (t < numTunnels) { tunnelCells.Add(freeForTunnelOrQuestion[i]); t++; }
                else if (q < numQuestions) { questionCells.Add(freeForTunnelOrQuestion[i]); q++; }
            }

            var tunnelCellSet = new HashSet<Vector2Int>(tunnelCells);
            Direction[] allDirs = { Direction.Left, Direction.Right, Direction.Up, Direction.Down };
            var tunnelPairs = new List<(Vector2Int pos, TunnelData data)>();
            foreach (Vector2Int p in tunnelCells)
            {
                var dirs = allDirs.ToList();
                Shuffle(dirs);
                bool placed = false;
                foreach (Direction dir in dirs)
                {
                    Vector2Int exitData = GetExitDataCellForValidation(p, dir);
                    if (exitData.x >= 0 && exitData.x < gridW && exitData.y >= 0 && exitData.y < gridH
                        && !IsWall(exitData.x, exitData.y)
                        && !usedPos.Contains(exitData)
                        && !tunnelCellSet.Contains(exitData)
                        && !cellData[exitData.x, exitData.y].IsLocked)
                    {
                        cellData[p.x, p.y].ChangeTunnelThread(true);
                        cellData[p.x, p.y].ColorLevel = 0;
                        cellData[p.x, p.y].DirectionLevel = (int)dir;
                        cellData[p.x, p.y].OnDirectionLevelChanged();
                    int colorId = chosenLevels[UnityEngine.Random.Range(0, numColors)];
                    var threadList = new List<ThreadData> { new ThreadData((GeneralColor)colorId, false, exitData, Vector3.zero) };
                        var td = new TunnelData();
                        SetField(typeof(TunnelData), td, "m_Threads", threadList);
                        tunnelPairs.Add((p, td));
                        placed = true;
                        break;
                    }
                }
            }

            int GridOrderKey(Vector2Int p) => (gridH - 1 - p.y) * gridW + p.x;
            var tunnelDataList = tunnelPairs.OrderBy(x => GridOrderKey(x.pos)).Select(x => x.data).ToList();
            SetField(levelType, level, "m_TunnelThreadData", tunnelDataList);

            var questionThreadDataList = new List<ThreadData>();
            foreach (Vector2Int p in questionCells)
            {
                cellData[p.x, p.y].ChangeQuestionThread(true);
                cellData[p.x, p.y].ColorLevel = 0;
                int colorId = chosenLevels[UnityEngine.Random.Range(0, numColors)];
                questionThreadDataList.Add(new ThreadData((GeneralColor)colorId, true, p, Vector3.zero));
            }
            questionThreadDataList = questionThreadDataList.Zip(questionCells, (data, pos) => (pos, data)).OrderBy(t => GridOrderKey(t.pos)).Select(t => t.data).ToList();
            SetField(levelType, level, "m_QuestionThreadData", questionThreadDataList);

            StitchCellData[,] stitchData = new StitchCellData[StitchWidth, stitchHeight];
            for (int x = 0; x < StitchWidth; x++)
                for (int y = 0; y < stitchHeight; y++)
                {
                    var sc = new StitchCellData();
                    sc.ColorLevel = stitchGrid[x, y];
                    stitchData[x, y] = sc;
                }

            SetField(levelType, level, "m_CellData", cellData);
            SetField(levelType, level, "m_StitchData", stitchData);

            var knitColors = new List<GeneralColor>();
            for (int c = 0; c < numColors; c++)
                knitColors.Add((GeneralColor)chosenLevels[c]);
            SetField(levelType, level, "m_KnitColor", knitColors);

            SlotCount slotCount = gridW <= 5 ? SlotCount.Five : (gridW <= 6 ? SlotCount.Six : SlotCount.Seven);
            SlotData slotData = new SlotData();
            SetField(typeof(SlotData), slotData, "m_Count", slotCount);
            SetField(levelType, level, "m_SlotData", slotData);

            SetField(baseType, level, "m_IsUnlocked", levelIndex == 1);
            SetField(baseType, level, "m_MoveCount", 0);
            SetField(baseType, level, "m_Time", 0f);
            SetField(baseType, level, "m_IsBossLevel", false);
            SetField(baseType, level, "m_IsBonusLevel", false);
        }

        /// <summary> L veya T duvar şekilleri: aralara duvar sıkıştırır. </summary>
        private static void AddInternalWalls(CellData[,] cellData, int gridW, int gridH, List<Vector2Int> interior, Func<int, int, bool> IsWall, int count)
        {
            var free = new HashSet<Vector2Int>(interior);
            for (int x = 0; x < gridW; x++)
                for (int y = 0; y < gridH; y++)
                    if (cellData[x, y].IsLocked) free.Remove(new Vector2Int(x, y));

            List<Vector2Int> freeList = free.ToList();
            Shuffle(freeList);

            int[][] lShape = { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 0, 1 } };
            int[][] tShape = { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 1, 1 } };

            int placed = 0;
            for (int n = 0; n < count && placed < count; n++)
            {
                int[][] shape = UnityEngine.Random.value > 0.5f ? lShape : tShape;
                for (int attempt = 0; attempt < 20 && placed < count; attempt++)
                {
                    int idx = UnityEngine.Random.Range(0, freeList.Count);
                    Vector2Int root = freeList[idx];
                    bool canPlace = true;
                    foreach (var d in shape)
                    {
                        int nx = root.x + d[0], ny = root.y + d[1];
                        if (nx < 0 || nx >= gridW || ny < 0 || ny >= gridH || IsWall(nx, ny) || !free.Contains(new Vector2Int(nx, ny)))
                        { canPlace = false; break; }
                    }
                    if (!canPlace) continue;
                    foreach (var d in shape)
                    {
                        int nx = root.x + d[0], ny = root.y + d[1];
                        var p = new Vector2Int(nx, ny);
                        cellData[nx, ny].ChangeLocked(true);
                        free.Remove(p);
                    }
                    placed++;
                    break;
                }
            }
        }

        /// <summary> Veri uzayında çıkış hücresi: yukarı = +y, aşağı = -y. Oyun artık grid'i ters çevirmiyor. </summary>
        private static Vector2Int GetExitDataCellForValidation(Vector2Int p, Direction dir)
        {
            int dx = dir == Direction.Left ? -1 : (dir == Direction.Right ? 1 : 0);
            int dy = dir == Direction.Up ? 1 : (dir == Direction.Down ? -1 : 0);
            return new Vector2Int(p.x + dx, p.y + dy);
        }

        private static void SetField(Type type, object target, string fieldName, object value)
        {
            FieldInfo f = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null && type.BaseType != null)
                f = type.BaseType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            f?.SetValue(target, value);
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static void SyncFacadeProperties(LevelData level)
        {
            var cellData = level.CellData;
            if (cellData != null)
                for (int x = 0; x < cellData.GetLength(0); x++)
                    for (int y = 0; y < cellData.GetLength(1); y++)
                        cellData[x, y]?.OnColorLevelChanged();
            var stitchData = level.StitchData;
            if (stitchData != null)
                for (int c = 0; c < stitchData.GetLength(0); c++)
                    for (int r = 0; r < stitchData.GetLength(1); r++)
                        if (stitchData[c, r].ColorLevel > 0)
                            stitchData[c, r].OnColorLevelChanged();
        }
    }
}
