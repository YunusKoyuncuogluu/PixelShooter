using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Grids;
using LevelManagement;
using UnityEditor;
using UnityEngine;

namespace PixelShooter.Editor
{
    public class LevelGeneratorWindow : EditorWindow
    {
        private const string LevelsFolder = "Assets/_Project/Data/Levels";
        private const int MinGridSize = 5;
        private const int MaxGridSize = 10;
        private const int MinShootersPerTunnel = 2;

        private const int ShooterMaxCapacity = 10;

        [MenuItem("Tools/Generate 100 Levels")]
        public static void Open()
        {
            GetWindow<LevelGeneratorWindow>("Level Generator").Show();
        }

        [MenuItem("Tools/PixelShooter/Generate Level 1 (Balanced)")]
        public static void GenerateLevel1Menu()
        {
            LevelGeneratorWindow tool = CreateInstance<LevelGeneratorWindow>();
            try
            {
                tool.GenerateAndSaveLevel1();
            }
            finally
            {
                DestroyImmediate(tool);
            }
        }

        public static void GenerateLevel1Batch()
        {
            LevelGeneratorWindow tool = null;
            try
            {
                tool = CreateInstance<LevelGeneratorWindow>();
                tool.GenerateAndSaveLevel1();
            }
            catch (Exception)
            {
                if (tool != null)
                    DestroyImmediate(tool);

                EditorApplication.Exit(1);
                return;
            }

            DestroyImmediate(tool);
            EditorApplication.Exit(0);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Level 1: pixel mekanigine gore renk basina kapasite dengeli uretilir " +
                $"(opaquePixels == shooters * {ShooterMaxCapacity}). Remaining levels keep the previous grid layout.",
                MessageType.Info);

            if (GUILayout.Button("Generate Level 1 Only (Balanced)", GUILayout.Height(28)))
                GenerateAndSaveLevel1();

            if (GUILayout.Button("Generate 100 Levels", GUILayout.Height(32)))
                GenerateAll();
        }

        private void GenerateAndSaveLevel1()
        {
            EnsureLevelsFolder();
            string path = $"{LevelsFolder}/Level_1.asset";
            LevelData generated = GenerateEasyBalancedLevel1();
            LevelData existing = AssetDatabase.LoadAssetAtPath<LevelData>(path);

            if (existing != null)
            {

                CopyLevelDataFields(generated, existing);
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(generated);
                generated = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(generated, path);
            }

            RegisterInDatabase(generated);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(generated);
        }

        private void CopyLevelDataFields(LevelData source, LevelData target)
        {
            Type levelType = typeof(LevelData);
            Type baseType = typeof(ScriptableLevelBase);

            SetField(levelType, target, "width", source.Width);
            SetField(levelType, target, "height", source.Height);
            SetField(levelType, target, "cellData", source.CellData);
            SetField(levelType, target, "tunnelShooterData", source.TunnelShooterData ?? new List<TunnelData>());
            SetField(levelType, target, "questionShooterData", source.QuestionShooterData ?? new List<ShooterData>());
            SetField(levelType, target, "slotData", source.SlotData);
            SetField(levelType, target, "plateCount", source.PlateCount);

            SetField(baseType, target, "isUnlocked", true);
            SetField(baseType, target, "moveCount", 0);
            SetField(baseType, target, "time", 0f);
            SetField(baseType, target, "isBossLevel", false);
            SetField(baseType, target, "isBonusLevel", false);

            List<PixelData> pixels = new List<PixelData>(40);
            PixelCellData[,] grid = source.PixelGrid;
            if (grid != null)
            {
                int w = grid.GetLength(0);
                int h = grid.GetLength(1);
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        PixelCellData cell = grid[x, y];
                        if (cell == null || !cell.HasPixel)
                            continue;
                        pixels.Add(new PixelData
                        {
                            position = new Vector2Int(x, y),
                            color = cell.Color,
                            paletteId = cell.PaletteId
                        });
                    }
                }
            }

            target.ApplyPixelBake(pixels, source.PixelSize);
            SyncFacadeProperties(target);
        }

        private void EnsureLevelsFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            }

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Levels"))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Levels");
            }
        }

        private static void RegisterInDatabase(LevelData level)
        {
            if (level == null)
            {
                return;
            }

            LevelDatabase database = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/_Project/Data/LevelDatabase.asset");
            if (database == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(database);
            SerializedProperty levels = serialized.FindProperty("levels");
            if (levels == null || !levels.isArray)
            {
                return;
            }

            for (int i = 0; i < levels.arraySize; i++)
            {
                if (levels.GetArrayElementAtIndex(i).objectReferenceValue == level)
                {
                    return;
                }
            }

            int index = levels.arraySize;
            levels.arraySize = index + 1;
            levels.GetArrayElementAtIndex(index).objectReferenceValue = level;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        private void GenerateAll()
        {
            EnsureLevelsFolder();

            for (int n = 1; n <= 100; n++)
            {
                LevelData level = GenerateLevel(n);
                string path = $"{LevelsFolder}/Level_{n}.asset";
                AssetDatabase.CreateAsset(level, path);
                RegisterInDatabase(level);
                EditorUtility.DisplayProgressBar("Generate Levels", $"Level {n}/100", n / 100f);
            }
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private int GetGridSizeForLevel(int levelIndex)
        {
            if (levelIndex <= 20)
                return 5;
            if (levelIndex <= 40)
                return 6;
            if (levelIndex <= 60)
                return 7;
            if (levelIndex <= 80)
                return 8;
            if (levelIndex <= 95)
                return 9;
            return 10;
        }

        private LevelData GenerateLevel(int levelIndex)
        {
            if (levelIndex == 1)
                return GenerateEasyBalancedLevel1();

            LevelData fallback = null;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                LevelData level = BuildRandomLevel(levelIndex, attempt >= 24);
                if (IsLayoutSolvable(level))
                {
                    if (fallback != null)
                        UnityEngine.Object.DestroyImmediate(fallback);
                    return level;
                }

                if (fallback != null)
                    UnityEngine.Object.DestroyImmediate(fallback);
                fallback = level;
            }

            return fallback;
        }

        private LevelData BuildRandomLevel(int levelIndex, bool dropWalls)
        {
            int gridW = GetGridSizeForLevel(levelIndex);
            int gridH = gridW;

            GetDifficultyParams(levelIndex, out int numColors, out int shooterCount, out int numTunnels, out int numQuestions, out int numInternalWalls);
            if (dropWalls)
                numInternalWalls = 0;
            shooterCount = Math.Max(shooterCount, numColors);

            int[] chosenLevels = GetChosenFacadeLevels(numColors);
            if (chosenLevels == null || chosenLevels.Length != numColors)
                chosenLevels = FallbackLevels(numColors);

            int[] tunnelColorIndices = new int[numTunnels];
            int[] questionColorIndices = new int[numQuestions];
            for (int i = 0; i < numTunnels; i++)
                tunnelColorIndices[i] = UnityEngine.Random.Range(0, numColors);
            for (int i = 0; i < numQuestions; i++)
                questionColorIndices[i] = UnityEngine.Random.Range(0, numColors);

            int[] shooterCountPerColor = BuildShooterCountPerColor(
                shooterCount, numColors, numTunnels, numQuestions, tunnelColorIndices, questionColorIndices);

            LevelData level = ScriptableObject.CreateInstance<LevelData>();
            SetLevelDataViaReflection(level, levelIndex, gridW, gridH,
                shooterCountPerColor, chosenLevels, shooterCount, numTunnels, numQuestions, numInternalWalls,
                tunnelColorIndices, questionColorIndices);
            SyncFacadeProperties(level);
            return level;
        }

        private LevelData GenerateEasyBalancedLevel1()
        {
            const int gridW = 5;
            const int gridH = 5;
            const int redLevel = (int)GeneralColor.Red;
            const int yellowLevel = (int)GeneralColor.Yellow;
            const int redShooters = 2;
            const int yellowShooters = 2;

            LevelData level = ScriptableObject.CreateInstance<LevelData>();
            Type levelType = typeof(LevelData);
            Type baseType = typeof(ScriptableLevelBase);

            SetField(levelType, level, "width", gridW);
            SetField(levelType, level, "height", gridH);

            CellData[,] cellData = new CellData[gridW, gridH];
            for (int x = 0; x < gridW; x++)
                for (int y = 0; y < gridH; y++)
                    cellData[x, y] = new CellData();

            cellData[1, 4].ColorLevel = redLevel;
            cellData[3, 4].ColorLevel = redLevel;
            cellData[2, 4].ColorLevel = yellowLevel;
            cellData[2, 3].ColorLevel = yellowLevel;

            SetField(levelType, level, "cellData", cellData);
            SetField(levelType, level, "tunnelShooterData", new List<TunnelData>());
            SetField(levelType, level, "questionShooterData", new List<ShooterData>());

            SlotData slotData = new SlotData();
            SetField(typeof(SlotData), slotData, "count", SlotCount.Five);
            SetField(typeof(SlotData), slotData, "lockedByIndex", new List<bool> { false, false, false, false, false });
            SetField(levelType, level, "slotData", slotData);

            int plateCount = redShooters + yellowShooters;
            SetField(levelType, level, "plateCount", plateCount);

            SetField(baseType, level, "isUnlocked", true);
            SetField(baseType, level, "moveCount", 0);
            SetField(baseType, level, "time", 0f);
            SetField(baseType, level, "isBossLevel", false);
            SetField(baseType, level, "isBonusLevel", false);

            SyncFacadeProperties(level);

            List<PixelData> pixels = BuildBalancedPixelPattern(
                redLevel,
                yellowLevel,
                redShooters * ShooterMaxCapacity,
                yellowShooters * ShooterMaxCapacity,
                out Vector2Int pixelSize);
            level.ApplyPixelBake(pixels, pixelSize);

            ValidateColorCapacityBalance(level, redLevel, redShooters * ShooterMaxCapacity);
            ValidateColorCapacityBalance(level, yellowLevel, yellowShooters * ShooterMaxCapacity);
            return level;
        }

        private List<PixelData> BuildBalancedPixelPattern(
            int redLevel,
            int yellowLevel,
            int redPixelCount,
            int yellowPixelCount,
            out Vector2Int pixelSize)
        {
            const int width = 8;
            const int height = 5;
            pixelSize = new Vector2Int(width, height);

            if (redPixelCount != width / 2 * height || yellowPixelCount != width / 2 * height)
            {
                throw new InvalidOperationException(
                    $"Level 1 pixel deseni {width / 2}x{height} bekliyor " +
                    $"(red={redPixelCount}, yellow={yellowPixelCount}).");
            }

            Color32 redColor = ResolveFacadeColor32(redLevel);
            Color32 yellowColor = ResolveFacadeColor32(yellowLevel);
            List<PixelData> pixels = new List<PixelData>(redPixelCount + yellowPixelCount);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isRed = x < width / 2;
                    pixels.Add(new PixelData
                    {
                        position = new Vector2Int(x, y),
                        color = isRed ? redColor : yellowColor,
                        paletteId = (short)(isRed ? redLevel : yellowLevel)
                    });
                }
            }

            return pixels;
        }

        private Color32 ResolveFacadeColor32(int facadeLevel)
        {
            ProjectSettings settings = ProjectSettings.Load();
            if (settings != null)
            {
                List<FacadeProperty> facades = settings.GetOriginalFacadeProperties();
                if (facades != null)
                {
                    for (int i = 0; i < facades.Count; i++)
                    {
                        FacadeProperty facade = facades[i];
                        if (facade == null || facade.Level != facadeLevel)
                            continue;

                        Color c = facade.Color;
                        return new Color32(
                            (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
                            255);
                    }
                }
            }

            if (facadeLevel == (int)GeneralColor.Yellow)
                return new Color32(202, 180, 78, 255);
            return new Color32(230, 49, 72, 255);
        }

        private void ValidateColorCapacityBalance(LevelData level, int paletteId, int expectedCapacity)
        {
            PixelCellData[,] grid = level.PixelGrid;
            if (grid == null)
                throw new InvalidOperationException("Level 1 PixelGrid bos.");

            int count = 0;
            int w = grid.GetLength(0);
            int h = grid.GetLength(1);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    PixelCellData cell = grid[x, y];
                    if (cell != null && cell.HasPixel && cell.PaletteId == paletteId)
                        count++;
                }
            }

            if (count != expectedCapacity)
            {
                throw new InvalidOperationException(
                    $"Level 1 dengesizlik: paletteId={paletteId} pixels={count} expected={expectedCapacity}.");
            }
        }

        private void GetDifficultyParams(int levelIndex, out int numColors, out int shooterCount, out int numTunnels, out int numQuestions, out int numInternalWalls)
        {
            if (levelIndex <= 5)
            {
                numColors = 2;
                shooterCount = UnityEngine.Random.Range(2, 4);
                numTunnels = 0;
                numQuestions = 0;
                numInternalWalls = 0;
            }
            else if (levelIndex <= 10)
            {
                numColors = 2;
                shooterCount = UnityEngine.Random.Range(2, 5);
                numTunnels = UnityEngine.Random.Range(0, 2);
                numQuestions = UnityEngine.Random.Range(0, 2);
                numInternalWalls = 0;
            }
            else if (levelIndex <= 40)
            {
                numColors = 3;
                shooterCount = UnityEngine.Random.Range(3, 6);
                numTunnels = UnityEngine.Random.Range(1, 3);
                numQuestions = UnityEngine.Random.Range(0, 3);
                numInternalWalls = UnityEngine.Random.Range(0, 2);
            }
            else if (levelIndex <= 80)
            {
                numColors = 4;
                shooterCount = UnityEngine.Random.Range(4, 6);
                numTunnels = UnityEngine.Random.Range(1, 4);
                numQuestions = UnityEngine.Random.Range(1, 4);
                numInternalWalls = UnityEngine.Random.Range(1, 3);
            }
            else
            {
                numColors = 5;
                shooterCount = UnityEngine.Random.Range(5, 6);
                numTunnels = UnityEngine.Random.Range(2, 5);
                numQuestions = UnityEngine.Random.Range(1, 5);
                numInternalWalls = UnityEngine.Random.Range(1, 4);
            }
        }

        private int[] BuildShooterCountPerColor(
            int shooterCount, int numColors, int numTunnels, int numQuestions,
            int[] tunnelColorIndices, int[] questionColorIndices)
        {
            int[] gridShooterCount = new int[numColors + 1];
            int remainder = shooterCount;
            for (int c = 1; c < numColors; c++)
            {
                int n = UnityEngine.Random.Range(1, Math.Max(2, remainder - (numColors - c)));
                gridShooterCount[c] = n;
                remainder -= n;
            }
            gridShooterCount[numColors] = remainder;

            int[] tunnelCountPerColorIndex = new int[numColors];
            int[] questionCountPerColorIndex = new int[numColors];
            for (int i = 0; i < numTunnels; i++)
                tunnelCountPerColorIndex[tunnelColorIndices[i]] += MinShootersPerTunnel;
            for (int i = 0; i < numQuestions; i++)
                questionCountPerColorIndex[questionColorIndices[i]]++;

            for (int c = 1; c <= numColors; c++)
            {
                int idx = c - 1;
                int shootersInPuzzle = tunnelCountPerColorIndex[idx] + questionCountPerColorIndex[idx];
                if (shootersInPuzzle > 0 && gridShooterCount[c] < 1)
                {
                    for (int d = 1; d <= numColors; d++)
                    {
                        if (d != c && gridShooterCount[d] > 1)
                        {
                            gridShooterCount[d]--;
                            gridShooterCount[c] = 1;
                            break;
                        }
                    }
                }
            }

            return gridShooterCount;
        }

        private int[] GetChosenFacadeLevels(int numColors)
        {
            var list = ProjectSettings.Load()?.GetOriginalFacadeProperties();
            if (list == null || list.Count == 0)
                return null;
            var levelSet = new HashSet<int>();
            foreach (var f in list)
            {
                if (f != null && f.Level >= 1)
                    levelSet.Add(f.Level);
            }
            var levels = new List<int>(levelSet);
            if (levels.Count < numColors)
                return null;
            Shuffle(levels);
            var chosen = new int[numColors];
            for (int i = 0; i < numColors; i++)
                chosen[i] = levels[i];
            return chosen;
        }

        private int[] FallbackLevels(int numColors)
        {
            var a = new int[numColors];
            for (int i = 0; i < numColors; i++)
                a[i] = i + 1;
            return a;
        }

        private List<bool> GetLockedSlotsForDifficulty(int levelIndex, SlotCount slotCount)
        {
            int n = slotCount == SlotCount.Five ? 5 : (slotCount == SlotCount.Six ? 6 : 7);
            var locked = new List<bool>(n);
            for (int i = 0; i < n; i++)
                locked.Add(false);

            if (levelIndex <= 8)
                return locked;
            if (levelIndex <= 20)
            {
                locked[levelIndex % 2 == 0 ? 0 : n - 1] = true;
                return locked;
            }
            if (levelIndex <= 45)
            {
                locked[0] = true;
                locked[n - 1] = true;
                return locked;
            }
            int combo = levelIndex % 4;
            switch (combo)
            {
                case 0: locked[0] = true; locked[n - 1] = true; break;
                case 1: locked[0] = true; if (n > 1) locked[1] = true; break;
                case 2: locked[n - 1] = true; if (n > 1) locked[n - 2] = true; break;
                default: locked[0] = true; locked[n - 1] = true; break;
            }
            return locked;
        }

        private void SetLevelDataViaReflection(LevelData level, int levelIndex, int gridW, int gridH,
            int[] shooterCountPerColor, int[] chosenLevels, int totalShooters, int numTunnels, int numQuestions, int numInternalWalls,
            int[] tunnelColorIndices, int[] questionColorIndices)
        {
            int numColors = chosenLevels.Length;
            Type levelType = typeof(LevelData);
            Type baseType = typeof(ScriptableLevelBase);

            SetField(levelType, level, "width", gridW);
            SetField(levelType, level, "height", gridH);

            List<Vector2Int> interior = new List<Vector2Int>(gridW * gridH);
            for (int x = 0; x < gridW; x++)
                for (int y = 0; y < gridH; y++)
                    interior.Add(new Vector2Int(x, y));
            Shuffle(interior);

            CellData[,] cellData = new CellData[gridW, gridH];
            for (int x = 0; x < gridW; x++)
                for (int y = 0; y < gridH; y++)
                    cellData[x, y] = new CellData();

            AddInternalWalls(cellData, gridW, gridH, interior, numInternalWalls);

            var accessibleOrdered = interior.Where(p => !cellData[p.x, p.y].IsLocked)
                .OrderByDescending(p => p.y)
                .ThenBy(p => UnityEngine.Random.value)
                .ToList();
            int numReserved = Math.Min(numColors, accessibleOrdered.Count);
            var reservedForOnePerColor = accessibleOrdered.Take(numReserved).ToList();

            var shootersForTopRow = new List<int>();
            for (int c = 0; c < numColors; c++)
                shootersForTopRow.Add(chosenLevels[c]);

            var shootersForRest = new List<int>();
            for (int c = 0; c < numColors; c++)
            {
                int reservedForThisColor = c < numReserved ? 1 : 0;
                for (int k = 0; k < shooterCountPerColor[c + 1] - reservedForThisColor; k++)
                    shootersForRest.Add(chosenLevels[c]);
            }
            Shuffle(shootersForRest);

            for (int i = 0; i < reservedForOnePerColor.Count; i++)
            {
                Vector2Int p = reservedForOnePerColor[i];
                int colorId = i < shootersForTopRow.Count ? shootersForTopRow[i] : shootersForTopRow[shootersForTopRow.Count - 1];
                cellData[p.x, p.y].ColorLevel = colorId;
            }

            var interiorWithoutReserved = new HashSet<Vector2Int>(interior);
            foreach (var p in reservedForOnePerColor)
                interiorWithoutReserved.Remove(p);
            var restInterior = interiorWithoutReserved.Where(p => !cellData[p.x, p.y].IsLocked).ToList();
            Shuffle(restInterior);

            int used = 0;
            for (int i = 0; i < restInterior.Count && used < shootersForRest.Count; i++)
            {
                Vector2Int p = restInterior[i];
                cellData[p.x, p.y].ColorLevel = shootersForRest[used];
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
                if (t < numTunnels)
                {
                    tunnelCells.Add(freeForTunnelOrQuestion[i]);
                    t++;
                }
                else if (q < numQuestions)
                {
                    questionCells.Add(freeForTunnelOrQuestion[i]);
                    q++;
                }
            }

            var tunnelCellSet = new HashSet<Vector2Int>(tunnelCells);
            Direction[] allDirs = { Direction.Left, Direction.Right, Direction.Up, Direction.Down };
            var tunnelPairs = new List<(Vector2Int pos, TunnelData data)>();
            int tunnelIdx = 0;
            foreach (Vector2Int p in tunnelCells)
            {
                var dirs = allDirs.ToList();
                Shuffle(dirs);
                bool placed = false;
                foreach (Direction dir in dirs)
                {
                    if (!TryGetTunnelExit(p, dir, gridW, gridH, out Vector2Int exitData, out bool boardExit))
                        continue;

                    bool exitOpen = boardExit
                        || (exitData.x >= 0 && exitData.x < gridW && exitData.y >= 0 && exitData.y < gridH
                            && !usedPos.Contains(exitData)
                            && !tunnelCellSet.Contains(exitData)
                            && !cellData[exitData.x, exitData.y].IsLocked);
                    if (exitOpen)
                    {
                        cellData[p.x, p.y].SetTunnelShooter(true);
                        cellData[p.x, p.y].ColorLevel = 0;
                        cellData[p.x, p.y].DirectionLevel = (int)dir;
                        cellData[p.x, p.y].OnDirectionLevelChanged();
                        int colorIdx = tunnelColorIndices != null && tunnelIdx < tunnelColorIndices.Length ? tunnelColorIndices[tunnelIdx] : UnityEngine.Random.Range(0, numColors);
                        int colorId = chosenLevels[colorIdx];
                        var shooterList = new List<ShooterData>();
                        for (int m = 0; m < MinShootersPerTunnel; m++)
                            shooterList.Add(new ShooterData((GeneralColor)colorId, false, exitData, Vector3.zero));
                        var td = new TunnelData();
                        SetField(typeof(TunnelData), td, "shooters", shooterList);
                        tunnelPairs.Add((p, td));
                        tunnelIdx++;
                        placed = true;
                        break;
                    }
                }
            }

            int GridOrderKey(Vector2Int p) => (gridH - 1 - p.y) * gridW + p.x;
            var tunnelDataList = tunnelPairs.OrderBy(x => GridOrderKey(x.pos)).Select(x => x.data).ToList();
            SetField(levelType, level, "tunnelShooterData", tunnelDataList);

            var questionShooterDataList = new List<ShooterData>();
            int questionIdx = 0;
            foreach (Vector2Int p in questionCells)
            {
                cellData[p.x, p.y].SetQuestionShooter(true);
                cellData[p.x, p.y].ColorLevel = 0;
                int colorIdx = questionColorIndices != null && questionIdx < questionColorIndices.Length ? questionColorIndices[questionIdx] : UnityEngine.Random.Range(0, numColors);
                int colorId = chosenLevels[colorIdx];
                questionShooterDataList.Add(new ShooterData((GeneralColor)colorId, true, p, Vector3.zero));
                questionIdx++;
            }
            questionShooterDataList = questionShooterDataList.Zip(questionCells, (data, pos) => (pos, data)).OrderBy(t => GridOrderKey(t.pos)).Select(t => t.data).ToList();
            SetField(levelType, level, "questionShooterData", questionShooterDataList);

            SetField(levelType, level, "cellData", cellData);

            SlotCount slotCount = gridW <= 5 ? SlotCount.Five : (gridW <= 6 ? SlotCount.Six : SlotCount.Seven);
            SlotData slotData = new SlotData();
            SetField(typeof(SlotData), slotData, "count", slotCount);
            List<bool> lockedSlots = GetLockedSlotsForDifficulty(levelIndex, slotCount);
            SetField(typeof(SlotData), slotData, "lockedByIndex", lockedSlots);
            SetField(levelType, level, "slotData", slotData);

            int plateCount = ResolvePlateCountForLevel(levelIndex, slotCount);
            SetField(levelType, level, "plateCount", plateCount);

            SetField(baseType, level, "isUnlocked", levelIndex == 1);
            SetField(baseType, level, "moveCount", 0);
            SetField(baseType, level, "time", 0f);
            SetField(baseType, level, "isBossLevel", false);
            SetField(baseType, level, "isBonusLevel", false);
        }

        private int ResolvePlateCountForLevel(int levelIndex, SlotCount slotCount)
        {
            int openSlots = slotCount switch
            {
                SlotCount.Five => 3,
                SlotCount.Six => 5,
                SlotCount.Seven => 7,
                _ => 5
            };

            int baseCount = Mathf.Max(3, openSlots);
            int bonus = levelIndex / 25;
            return Mathf.Clamp(baseCount + bonus, 3, 12);
        }

        private void AddInternalWalls(CellData[,] cellData, int gridW, int gridH, List<Vector2Int> interior, int count)
        {
            bool IsInCenter(int x, int y)
            {
                int cx = gridW / 2;
                int cy = gridH / 2;
                int dx = Mathf.Abs(x - cx);
                int dy = Mathf.Abs(y - cy);
                return (gridW % 2 == 1 && dx <= 1 && dy <= 1) || (gridW % 2 == 0 && dx <= 1 && dy <= 1);
            }

            var free = new HashSet<Vector2Int>(interior);
            for (int x = 0; x < gridW; x++)
                for (int y = 0; y < gridH; y++)
                    if (cellData[x, y].IsLocked)
                        free.Remove(new Vector2Int(x, y));

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
                    if (freeList.Count == 0)
                        break;

                    int idx = UnityEngine.Random.Range(0, freeList.Count);
                    Vector2Int root = freeList[idx];
                    bool canPlace = true;
                    foreach (var d in shape)
                    {
                        int nx = root.x + d[0], ny = root.y + d[1];
                        if (nx < 0 || nx >= gridW || ny < 0 || ny >= gridH || !free.Contains(new Vector2Int(nx, ny))
                            || IsInCenter(nx, ny))
                        { canPlace = false; break; }
                    }
                    if (!canPlace)
                        continue;

                    List<Vector2Int> stamped = new List<Vector2Int>(shape.Length);
                    foreach (var d in shape)
                    {
                        int nx = root.x + d[0];
                        int ny = root.y + d[1];
                        Vector2Int p = new Vector2Int(nx, ny);
                        cellData[nx, ny].ChangeLocked(true);
                        stamped.Add(p);
                    }

                    if (!EveryOpenCellReachesExit(cellData, gridW, gridH))
                    {
                        for (int s = 0; s < stamped.Count; s++)
                            cellData[stamped[s].x, stamped[s].y].ChangeLocked(false);
                        continue;
                    }

                    for (int s = 0; s < stamped.Count; s++)
                        free.Remove(stamped[s]);
                    placed++;
                    break;
                }
            }
        }

        private CellData CellAtGame(CellData[,] cellData, int x, int gameY, int height)
        {
            return cellData[x, height - 1 - gameY];
        }

        private bool TryGetTunnelExit(Vector2Int arrayPos, Direction dir, int width, int height, out Vector2Int arrayExit, out bool boardExit)
        {
            int gameY = height - 1 - arrayPos.y;
            int exitX = arrayPos.x;
            int exitGameY = gameY;
            if (dir == Direction.Left)
                exitX--;
            else if (dir == Direction.Right)
                exitX++;
            else if (dir == Direction.Up)
                exitGameY++;
            else if (dir == Direction.Down)
                exitGameY--;

            boardExit = dir == Direction.Up && exitGameY >= height;
            arrayExit = default;
            if (boardExit)
                return true;

            if (exitX < 0 || exitGameY < 0 || exitX >= width || exitGameY >= height)
                return false;

            arrayExit = new Vector2Int(exitX, height - 1 - exitGameY);
            return true;
        }

        private bool EveryOpenCellReachesExit(CellData[,] cellData, int width, int height)
        {
            bool[,] reached = new bool[width, height];
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            for (int x = 0; x < width; x++)
            {
                CellData top = CellAtGame(cellData, x, height - 1, height);
                if (top == null || top.IsLocked)
                    continue;

                reached[x, height - 1] = true;
                queue.Enqueue(new Vector2Int(x, height - 1));
            }

            int[] stepX = { 1, -1, 0, 0 };
            int[] stepY = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int nx = current.x + stepX[i];
                    int ny = current.y + stepY[i];
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height || reached[nx, ny])
                        continue;

                    CellData cell = CellAtGame(cellData, nx, ny, height);
                    if (cell == null || cell.IsLocked)
                        continue;

                    reached[nx, ny] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }

            for (int gameY = 0; gameY < height; gameY++)
            {
                for (int x = 0; x < width; x++)
                {
                    CellData cell = CellAtGame(cellData, x, gameY, height);
                    if (cell != null && !cell.IsLocked && !reached[x, gameY])
                        return false;
                }
            }

            return true;
        }

        private bool IsLayoutSolvable(LevelData level)
        {
            if (level == null || level.CellData == null)
                return false;

            int width = level.Width;
            int height = level.Height;
            CellData[,] cellData = level.CellData;
            if (width <= 0 || height <= 0)
                return false;

            if (!EveryOpenCellReachesExit(cellData, width, height))
                return false;

            bool[,] occupied = new bool[width, height];
            bool[,] tunnelCell = new bool[width, height];
            bool[,] wall = new bool[width, height];
            List<Vector2Int> tunnelSpots = new List<Vector2Int>();
            for (int arrayY = 0; arrayY < height; arrayY++)
            {
                for (int x = 0; x < width; x++)
                {
                    CellData cell = cellData[x, arrayY];
                    if (cell == null)
                        continue;

                    int gameY = height - 1 - arrayY;
                    if (cell.IsLocked)
                    {
                        wall[x, gameY] = true;
                        continue;
                    }
                    if (cell.IsTunnel)
                    {
                        tunnelCell[x, gameY] = true;
                        tunnelSpots.Add(new Vector2Int(x, arrayY));
                        continue;
                    }

                    if (cell.IsFull || cell.IsQuestionShooter)
                        occupied[x, gameY] = true;
                }
            }

            tunnelSpots.Sort((a, b) => ((height - 1 - a.y) * width + a.x).CompareTo((height - 1 - b.y) * width + b.x));
            int[] pending = new int[tunnelSpots.Count];
            List<TunnelData> tunnelData = level.TunnelShooterData;
            for (int i = 0; i < tunnelSpots.Count; i++)
            {
                int count = MinShootersPerTunnel;
                if (tunnelData != null && i < tunnelData.Count && tunnelData[i] != null && tunnelData[i].Shooters != null)
                    count = tunnelData[i].Shooters.Count;
                pending[i] = count;
            }

            bool progressed = true;
            while (progressed)
            {
                progressed = false;
                for (int gameY = 0; gameY < height; gameY++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (!occupied[x, gameY])
                            continue;
                        if (!CanEscape(occupied, tunnelCell, wall, width, height, x, gameY))
                            continue;

                        occupied[x, gameY] = false;
                        progressed = true;
                    }
                }

                for (int i = 0; i < tunnelSpots.Count; i++)
                {
                    if (pending[i] <= 0)
                        continue;

                    CellData cell = cellData[tunnelSpots[i].x, tunnelSpots[i].y];
                    if (cell == null)
                        continue;

                    if (!TryGetTunnelExit(tunnelSpots[i], cell.Direction, width, height, out Vector2Int arrayExit, out bool boardExit))
                        continue;

                    if (boardExit)
                    {
                        pending[i] = 0;
                        progressed = true;
                        continue;
                    }

                    int exitGameY = height - 1 - arrayExit.y;
                    if (occupied[arrayExit.x, exitGameY] || tunnelCell[arrayExit.x, exitGameY])
                        continue;

                    CellData exitCell = cellData[arrayExit.x, arrayExit.y];
                    if (exitCell != null && exitCell.IsLocked)
                        continue;

                    occupied[arrayExit.x, exitGameY] = true;
                    pending[i]--;
                    progressed = true;
                }
            }

            for (int gameY = 0; gameY < height; gameY++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (occupied[x, gameY])
                        return false;
                }
            }

            for (int i = 0; i < pending.Length; i++)
            {
                if (pending[i] > 0)
                    return false;
            }

            return true;
        }

        private bool CanEscape(bool[,] occupied, bool[,] tunnelCell, bool[,] wall, int width, int height, int startX, int startY)
        {
            if (startY == height - 1)
                return true;

            bool[,] visited = new bool[width, height];
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            visited[startX, startY] = true;
            queue.Enqueue(new Vector2Int(startX, startY));
            int[] stepX = { 1, -1, 0, 0 };
            int[] stepY = { 0, 0, 1, -1 };

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int nx = current.x + stepX[i];
                    int ny = current.y + stepY[i];
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height || visited[nx, ny])
                        continue;
                    if (wall[nx, ny] || tunnelCell[nx, ny] || occupied[nx, ny])
                        continue;

                    if (ny == height - 1)
                        return true;

                    visited[nx, ny] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }

            return false;
        }

        private void SetField(Type type, object target, string fieldName, object value)
        {
            FieldInfo f = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null && type.BaseType != null)
                f = type.BaseType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            f?.SetValue(target, value);
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void SyncFacadeProperties(LevelData level)
        {
            var cellData = level.CellData;
            if (cellData != null)
                for (int x = 0; x < cellData.GetLength(0); x++)
                    for (int y = 0; y < cellData.GetLength(1); y++)
                        cellData[x, y]?.OnColorLevelChanged();
        }
    }
}
