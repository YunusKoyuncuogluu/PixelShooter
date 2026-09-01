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
    /// Environment cercevesi harici; grid tam oynanabilir.
    /// Ara duvarlar (L, T IsLocked) ile zorlastirma. Normal grid 5x5'ten 10x10'a.
    /// Pixel mekanigi: renk basina opaquePixels == threadCount * SpoolMaxCapacity.
    /// Simdilik sadece Level 1 pixel dengeli uretilir.
    /// </summary>
    public class LevelGeneratorWindow : EditorWindow
    {
        private const string LevelsFolder = "Assets/Resources/Levels";
        private const int MinGridSize = 5;
        private const int MaxGridSize = 10;
        private const int MinThreadsPerTunnel = 2;

        /// <summary> Thread.prefab SpoolController.maxCapacity ile ayni tek kaynak. </summary>
        private const int SpoolMaxCapacity = 10;

        [MenuItem("Tools/Generate 100 Levels")]
        public static void Open()
        {
            GetWindow<LevelGeneratorWindow>("Level Generator").Show();
        }

        [MenuItem("Tools/Pixel Flow/Generate Level 1 (Balanced)")]
        public static void GenerateLevel1Menu()
        {
            GenerateAndSaveLevel1();
        }

        /// <summary> Unity batchmode: -executeMethod RopeWrap.Editor.LevelGeneratorWindow.GenerateLevel1Batch </summary>
        public static void GenerateLevel1Batch()
        {
            try
            {
                GenerateAndSaveLevel1();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LevelGenerator] Level 1 batch failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Level 1: pixel mekanigine gore renk basina kapasite dengeli uretilir " +
                $"(opaquePixels == threads * {SpoolMaxCapacity}). Diger leveller simdilik eski grid mantigi.",
                MessageType.Info);

            if (GUILayout.Button("Generate Level 1 Only (Balanced)", GUILayout.Height(28)))
                GenerateAndSaveLevel1();

            if (GUILayout.Button("Generate 100 Levels", GUILayout.Height(32)))
                GenerateAll();
        }

        private static void GenerateAndSaveLevel1()
        {
            EnsureLevelsFolder();
            string path = $"{LevelsFolder}/Level_1.asset";
            LevelData generated = GenerateEasyBalancedLevel1();
            LevelData existing = AssetDatabase.LoadAssetAtPath<LevelData>(path);

            if (existing != null)
            {
                // GUID koru: Odin alanlarini mevcut asset uzerine yaz.
                CopyLevelDataFields(generated, existing);
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(generated);
                generated = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(generated, path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(generated);
            Debug.Log(
                "[LevelGenerator] Level 1 hazir: 2 Red + 2 Yellow spool, " +
                $"20+20 piksel, kapasite={4 * SpoolMaxCapacity}, plate=4.");
        }

        /// <summary> Generated instance alanlarini hedef LevelData'ya tasir (GUID korunur). </summary>
        private static void CopyLevelDataFields(LevelData source, LevelData target)
        {
            Type levelType = typeof(LevelData);
            Type baseType = typeof(ScriptableLevelBase);

            SetField(levelType, target, "m_Width", source.Width);
            SetField(levelType, target, "m_Height", source.Height);
            SetField(levelType, target, "m_CellData", source.CellData);
            SetField(levelType, target, "m_TunnelThreadData", source.TunnelThreadData ?? new List<TunnelData>());
            SetField(levelType, target, "m_QuestionThreadData", source.QuestionThreadData ?? new List<ThreadData>());
            SetField(levelType, target, "m_SlotData", source.SlotData);
            SetField(levelType, target, "m_PlateCount", source.PlateCount);

            SetField(baseType, target, "m_IsUnlocked", true);
            SetField(baseType, target, "m_MoveCount", 0);
            SetField(baseType, target, "m_Time", 0f);
            SetField(baseType, target, "m_IsBossLevel", false);
            SetField(baseType, target, "m_IsBonusLevel", false);

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

        private static void EnsureLevelsFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Levels"))
                AssetDatabase.CreateFolder("Assets/Resources", "Levels");
        }

        private static void GenerateAll()
        {
            EnsureLevelsFolder();

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
            // Simdilik sadece Level 1 yeni pixel mekanigine gore dengeli.
            if (levelIndex == 1)
                return GenerateEasyBalancedLevel1();

            int gridW = GetGridSizeForLevel(levelIndex);
            int gridH = gridW;

            GetDifficultyParams(levelIndex, out int numColors, out int numSpools, out int numTunnels, out int numQuestions, out int numInternalWalls);
            numSpools = Math.Max(numSpools, numColors);

            int[] chosenLevels = GetChosenFacadeLevels(numColors);
            if (chosenLevels == null || chosenLevels.Length != numColors)
                chosenLevels = FallbackLevels(numColors);

            int[] tunnelColorIndices = new int[numTunnels];
            int[] questionColorIndices = new int[numQuestions];
            for (int i = 0; i < numTunnels; i++)
                tunnelColorIndices[i] = UnityEngine.Random.Range(0, numColors);
            for (int i = 0; i < numQuestions; i++)
                questionColorIndices[i] = UnityEngine.Random.Range(0, numColors);

            int[] spoolCountPerColor = BuildSpoolCountPerColorIncludingTunnelAndQuestion(
                numSpools, numColors, numTunnels, numQuestions, tunnelColorIndices, questionColorIndices);

            LevelData level = ScriptableObject.CreateInstance<LevelData>();
            SetLevelDataViaReflection(level, levelIndex, gridW, gridH,
                spoolCountPerColor, chosenLevels, numSpools, numTunnels, numQuestions, numInternalWalls,
                tunnelColorIndices, questionColorIndices);
            SyncFacadeProperties(level);
            return level;
        }

        /// <summary>
        /// Kolay tutorial Level 1:
        /// 2 Red + 2 Yellow grid makara, tunnel/question/duvar yok.
        /// Renk basina 20 piksel = 2 spool * 10 kapasite. Toplam 40 = 40.
        /// </summary>
        private static LevelData GenerateEasyBalancedLevel1()
        {
            const int gridW = 5;
            const int gridH = 5;
            const int redLevel = (int)GeneralColor.Red;
            const int yellowLevel = (int)GeneralColor.Yellow;
            const int redSpools = 2;
            const int yellowSpools = 2;

            LevelData level = ScriptableObject.CreateInstance<LevelData>();
            Type levelType = typeof(LevelData);
            Type baseType = typeof(ScriptableLevelBase);

            SetField(levelType, level, "m_Width", gridW);
            SetField(levelType, level, "m_Height", gridH);

            CellData[,] cellData = new CellData[gridW, gridH];
            for (int x = 0; x < gridW; x++)
                for (int y = 0; y < gridH; y++)
                    cellData[x, y] = new CellData();

            // Ust satira yakin, kolay erisilebilir sabit yerlesim.
            cellData[1, 4].ColorLevel = redLevel;
            cellData[3, 4].ColorLevel = redLevel;
            cellData[2, 4].ColorLevel = yellowLevel;
            cellData[2, 3].ColorLevel = yellowLevel;

            SetField(levelType, level, "m_CellData", cellData);
            SetField(levelType, level, "m_TunnelThreadData", new List<TunnelData>());
            SetField(levelType, level, "m_QuestionThreadData", new List<ThreadData>());

            SlotData slotData = new SlotData();
            SetField(typeof(SlotData), slotData, "m_Count", SlotCount.Five);
            SetField(typeof(SlotData), slotData, "m_LockedByIndex", new List<bool> { false, false, false, false, false });
            SetField(levelType, level, "m_SlotData", slotData);

            // Tum makaralar ayni anda binebilsin.
            int plateCount = redSpools + yellowSpools;
            SetField(levelType, level, "m_PlateCount", plateCount);

            SetField(baseType, level, "m_IsUnlocked", true);
            SetField(baseType, level, "m_MoveCount", 0);
            SetField(baseType, level, "m_Time", 0f);
            SetField(baseType, level, "m_IsBossLevel", false);
            SetField(baseType, level, "m_IsBonusLevel", false);

            SyncFacadeProperties(level);

            List<PixelData> pixels = BuildBalancedPixelPattern(
                redLevel,
                yellowLevel,
                redSpools * SpoolMaxCapacity,
                yellowSpools * SpoolMaxCapacity,
                out Vector2Int pixelSize);
            level.ApplyPixelBake(pixels, pixelSize);

            ValidateColorCapacityBalance(level, redLevel, redSpools * SpoolMaxCapacity);
            ValidateColorCapacityBalance(level, yellowLevel, yellowSpools * SpoolMaxCapacity);
            return level;
        }

        /// <summary>
        /// 8x5 fabric: sol 4 kolon Red, sag 4 kolon Yellow.
        /// Her renk SpoolMaxCapacity * spoolCount kadar opak piksel.
        /// </summary>
        private static List<PixelData> BuildBalancedPixelPattern(
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

        private static Color32 ResolveFacadeColor32(int facadeLevel)
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

            // Fallback: bilinen Red / Yellow tonlari.
            if (facadeLevel == (int)GeneralColor.Yellow)
                return new Color32(202, 180, 78, 255);
            return new Color32(230, 49, 72, 255);
        }

        private static void ValidateColorCapacityBalance(LevelData level, int paletteId, int expectedCapacity)
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

        private static int[] BuildSpoolCountPerColorIncludingTunnelAndQuestion(
            int numSpools, int numColors, int numTunnels, int numQuestions,
            int[] tunnelColorIndices, int[] questionColorIndices)
        {
            int[] gridSpoolCount = new int[numColors + 1];
            int remainder = numSpools;
            for (int c = 1; c < numColors; c++)
            {
                int n = UnityEngine.Random.Range(1, Math.Max(2, remainder - (numColors - c)));
                gridSpoolCount[c] = n;
                remainder -= n;
            }
            gridSpoolCount[numColors] = remainder;

            int[] tunnelCountPerColorIndex = new int[numColors];
            int[] questionCountPerColorIndex = new int[numColors];
            for (int i = 0; i < numTunnels; i++)
                tunnelCountPerColorIndex[tunnelColorIndices[i]] += MinThreadsPerTunnel;
            for (int i = 0; i < numQuestions; i++)
                questionCountPerColorIndex[questionColorIndices[i]]++;

            // Puzzle'da gorunen her renkte en az 1 grid makara olmali.
            for (int c = 1; c <= numColors; c++)
            {
                int idx = c - 1;
                int threadsInPuzzle = tunnelCountPerColorIndex[idx] + questionCountPerColorIndex[idx];
                if (threadsInPuzzle > 0 && gridSpoolCount[c] < 1)
                {
                    for (int d = 1; d <= numColors; d++)
                    {
                        if (d != c && gridSpoolCount[d] > 1)
                        {
                            gridSpoolCount[d]--;
                            gridSpoolCount[c] = 1;
                            break;
                        }
                    }
                }
            }

            return gridSpoolCount;
        }

        /// <summary> ProjectSettings FacadeProperties'ten numColors kadar rastgele Level secer (1-14 arasi). Her renk yalnizca bir kez secilir. </summary>
        private static int[] GetChosenFacadeLevels(int numColors)
        {
            var list = ProjectSettings.Load()?.GetOriginalFacadeProperties();
            if (list == null || list.Count == 0) return null;
            var levelSet = new HashSet<int>();
            foreach (var f in list)
            {
                if (f != null && f.Level >= 1)
                    levelSet.Add(f.Level);
            }
            var levels = new List<int>(levelSet);
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

        /// <summary> Zorluğa göre slot kilitleri: erken leveller açık, ilerledikçe farklı kilit kombinasyonları. </summary>
        private static List<bool> GetLockedSlotsForDifficulty(int levelIndex, SlotCount slotCount)
        {
            int n = slotCount == SlotCount.Five ? 5 : (slotCount == SlotCount.Six ? 6 : 7);
            var locked = new List<bool>(n);
            for (int i = 0; i < n; i++) locked.Add(false);

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

        /// <summary>
        /// U kenar IsLocked yok: cerceve EnvironmentFrameBuilder ile gelir.
        /// IsLocked sadece zorluk icin ic engellerde (AddInternalWalls) kullanilir.
        /// chosenLevels = FacadeProperty.Level degerleri (1-14).
        /// </summary>
        private static void SetLevelDataViaReflection(LevelData level, int levelIndex, int gridW, int gridH,
            int[] spoolCountPerColor, int[] chosenLevels, int totalSpools, int numTunnels, int numQuestions, int numInternalWalls,
            int[] tunnelColorIndices, int[] questionColorIndices)
        {
            int numColors = chosenLevels.Length;
            Type levelType = typeof(LevelData);
            Type baseType = typeof(ScriptableLevelBase);

            SetField(levelType, level, "m_Width", gridW);
            SetField(levelType, level, "m_Height", gridH);

            // Tum grid oynanabilir alan; cerceve Environment visual. IsLocked sadece ic engel icin.
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

            // Her renkten en az bir makarayı çıkışa en yakın hücrelere yerleştir; böylece hiçbir renk "sıkışmış" olmaz ve level çözülebilir kalır.
            var accessibleOrdered = interior.Where(p => !cellData[p.x, p.y].IsLocked)
                .OrderByDescending(p => p.y)
                .ThenBy(p => UnityEngine.Random.value)
                .ToList();
            int numReserved = Math.Min(numColors, accessibleOrdered.Count);
            var reservedForOnePerColor = accessibleOrdered.Take(numReserved).ToList();

            var spoolsForTopRow = new List<int>();
            for (int c = 0; c < numColors; c++)
                spoolsForTopRow.Add(chosenLevels[c]);

            // Rezerve hücrelere birer makara yerleştiriyoruz; kalan makaralar spoolsForRest ile doldurulur.
            var spoolsForRest = new List<int>();
            for (int c = 0; c < numColors; c++)
            {
                int reservedForThisColor = c < numReserved ? 1 : 0;
                for (int k = 0; k < spoolCountPerColor[c + 1] - reservedForThisColor; k++)
                    spoolsForRest.Add(chosenLevels[c]);
            }
            Shuffle(spoolsForRest);

            for (int i = 0; i < reservedForOnePerColor.Count; i++)
            {
                Vector2Int p = reservedForOnePerColor[i];
                int colorId = i < spoolsForTopRow.Count ? spoolsForTopRow[i] : spoolsForTopRow[spoolsForTopRow.Count - 1];
                cellData[p.x, p.y].ColorLevel = colorId;
            }

            var interiorWithoutReserved = new HashSet<Vector2Int>(interior);
            foreach (var p in reservedForOnePerColor)
                interiorWithoutReserved.Remove(p);
            var restInterior = interiorWithoutReserved.Where(p => !cellData[p.x, p.y].IsLocked).ToList();
            Shuffle(restInterior);

            int used = 0;
            for (int i = 0; i < restInterior.Count && used < spoolsForRest.Count; i++)
            {
                Vector2Int p = restInterior[i];
                cellData[p.x, p.y].ColorLevel = spoolsForRest[used];
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
            int tunnelIdx = 0;
            foreach (Vector2Int p in tunnelCells)
            {
                var dirs = allDirs.ToList();
                Shuffle(dirs);
                bool placed = false;
                foreach (Direction dir in dirs)
                {
                    Vector2Int exitData = GetExitDataCellForValidation(p, dir);
                    if (exitData.x >= 0 && exitData.x < gridW && exitData.y >= 0 && exitData.y < gridH
                        && !usedPos.Contains(exitData)
                        && !tunnelCellSet.Contains(exitData)
                        && !cellData[exitData.x, exitData.y].IsLocked)
                    {
                        cellData[p.x, p.y].ChangeTunnelThread(true);
                        cellData[p.x, p.y].ColorLevel = 0;
                        cellData[p.x, p.y].DirectionLevel = (int)dir;
                        cellData[p.x, p.y].OnDirectionLevelChanged();
                        int colorIdx = tunnelColorIndices != null && tunnelIdx < tunnelColorIndices.Length ? tunnelColorIndices[tunnelIdx] : UnityEngine.Random.Range(0, numColors);
                        int colorId = chosenLevels[colorIdx];
                        var threadList = new List<ThreadData>();
                        for (int m = 0; m < MinThreadsPerTunnel; m++)
                            threadList.Add(new ThreadData((GeneralColor)colorId, false, exitData, Vector3.zero));
                        var td = new TunnelData();
                        SetField(typeof(TunnelData), td, "m_Threads", threadList);
                        tunnelPairs.Add((p, td));
                        tunnelIdx++;
                        placed = true;
                        break;
                    }
                }
            }

            int GridOrderKey(Vector2Int p) => (gridH - 1 - p.y) * gridW + p.x;
            var tunnelDataList = tunnelPairs.OrderBy(x => GridOrderKey(x.pos)).Select(x => x.data).ToList();
            SetField(levelType, level, "m_TunnelThreadData", tunnelDataList);

            var questionThreadDataList = new List<ThreadData>();
            int questionIdx = 0;
            foreach (Vector2Int p in questionCells)
            {
                cellData[p.x, p.y].ChangeQuestionThread(true);
                cellData[p.x, p.y].ColorLevel = 0;
                int colorIdx = questionColorIndices != null && questionIdx < questionColorIndices.Length ? questionColorIndices[questionIdx] : UnityEngine.Random.Range(0, numColors);
                int colorId = chosenLevels[colorIdx];
                questionThreadDataList.Add(new ThreadData((GeneralColor)colorId, true, p, Vector3.zero));
                questionIdx++;
            }
            questionThreadDataList = questionThreadDataList.Zip(questionCells, (data, pos) => (pos, data)).OrderBy(t => GridOrderKey(t.pos)).Select(t => t.data).ToList();
            SetField(levelType, level, "m_QuestionThreadData", questionThreadDataList);

            SetField(levelType, level, "m_CellData", cellData);

            SlotCount slotCount = gridW <= 5 ? SlotCount.Five : (gridW <= 6 ? SlotCount.Six : SlotCount.Seven);
            SlotData slotData = new SlotData();
            SetField(typeof(SlotData), slotData, "m_Count", slotCount);
            List<bool> lockedSlots = GetLockedSlotsForDifficulty(levelIndex, slotCount);
            SetField(typeof(SlotData), slotData, "m_LockedByIndex", lockedSlots);
            SetField(levelType, level, "m_SlotData", slotData);

            int plateCount = ResolvePlateCountForLevel(levelIndex, slotCount);
            SetField(levelType, level, "m_PlateCount", plateCount);

            SetField(baseType, level, "m_IsUnlocked", levelIndex == 1);
            SetField(baseType, level, "m_MoveCount", 0);
            SetField(baseType, level, "m_Time", 0f);
            SetField(baseType, level, "m_IsBossLevel", false);
            SetField(baseType, level, "m_IsBonusLevel", false);
        }

        /// <summary> Erken levellerde daha az, ileride slot kapasitesine yakin plate. </summary>
        private static int ResolvePlateCountForLevel(int levelIndex, SlotCount slotCount)
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

        /// <summary> L veya T ic engel sekilleri: kenarlara yakin yerlestirir; grid ortasina duvar konmaz. IsLocked zorluk icin. </summary>
        private static void AddInternalWalls(CellData[,] cellData, int gridW, int gridH, List<Vector2Int> interior, int count)
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
        }
    }
}
