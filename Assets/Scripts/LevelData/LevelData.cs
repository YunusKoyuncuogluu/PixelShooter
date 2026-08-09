using System;
using System.Collections.Generic;
using Grids;
using Sirenix.OdinInspector;
using UnityEngine;
using Direction = Grids.Direction;

#if UNITY_EDITOR
using Sirenix.Utilities;
using UnityEditor;
#endif

namespace LevelManagement
{
    [CreateAssetMenu(fileName = nameof(LevelData) + " 001", menuName = "RopeWrap" + "/" + nameof(LevelData))]
    public class LevelData : ScriptableLevelBase
    {
#if UNITY_EDITOR
        /// <summary>
        /// Odin TableMatrix DrawElementMethod static olmak zorunda;
        /// editor cizimi icin gecici baglam (runtime game state degil).
        /// </summary>
        private static LevelData editorDrawContext;
#endif

        #region Grid

        [FoldoutGroup("Grid")] [OnValueChanged(nameof(OnWidthChanged))] [SerializeField]
        private int m_Width = 5;

        [FoldoutGroup("Grid")] [OnValueChanged(nameof(OnHeightChanged))] [SerializeField]
        private int m_Height = 5;

        public int Width => m_Width;
        public int Height => m_Height;

        public CellData[,] CellData => m_CellData;

        /// <summary> Oyun Y -> dizi satiri (ust satir = yuksek gameY). Pure math. </summary>
        public static int GameYToArrayRow(int gameY, int height) => height - 1 - gameY;

        [FoldoutGroup("Grid")]
        [SerializeField]
#if UNITY_EDITOR
        [TableMatrix(HideColumnIndices = false, HideRowIndices = true, SquareCells = true, DrawElementMethod = nameof(DrawCell))]
#endif
        private CellData[,] m_CellData = new CellData[5, 5];

        [FoldoutGroup("Grid")] [SerializeField]
        private CellData m_CellDataDistance = null;

        [SerializeField, HideInInspector]
        private bool m_GridYMigrated;

        /// <summary>
        /// Onceki (yanlis) top-left migrate uygulandiysa true.
        /// Runtime'da bir kez geri cevrilir; yeni bake'ler false kalir.
        /// </summary>
        [SerializeField, HideInInspector]
        private bool m_PixelYMigrated;

#if UNITY_EDITOR
        private GUIStyle m_LevelLabelStyle;

        private static CellData DrawCell(Rect rect, ref CellData element)
        {
            if (Event.current.type == EventType.KeyDown && rect.Contains(Event.current.mousePosition) && editorDrawContext.m_CellDataDistance != null)
            {
                editorDrawContext.m_CellDataDistance.ColorLevel = Event.current.keyCode switch
                {
                    KeyCode.Alpha1 or KeyCode.Keypad1 => Event.current.shift ? 11 : 1,
                    KeyCode.Alpha2 or KeyCode.Keypad2 => Event.current.shift ? 12 : 2,
                    KeyCode.Alpha3 or KeyCode.Keypad3 => Event.current.shift ? 13 : 3,
                    KeyCode.Alpha4 or KeyCode.Keypad4 => Event.current.shift ? 14 : 4,
                    KeyCode.Alpha5 or KeyCode.Keypad5 => Event.current.shift ? 15 : 5,
                    KeyCode.Alpha6 or KeyCode.Keypad6 => Event.current.shift ? 16 : 6,
                    KeyCode.Alpha7 or KeyCode.Keypad7 => Event.current.shift ? 17 : 7,
                    KeyCode.Alpha8 or KeyCode.Keypad8 => Event.current.shift ? 18 : 8,
                    KeyCode.Alpha9 or KeyCode.Keypad9 => Event.current.shift ? 19 : 9,
                    _ => editorDrawContext.m_CellDataDistance.ColorLevel
                };

                editorDrawContext.m_CellDataDistance.OnColorLevelChanged();
            }

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                GUI.changed = true;
                Event.current.Use();
                editorDrawContext.m_CellDataDistance = element;
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.T &&
                rect.Contains(Event.current.mousePosition))
            {
                element.ColorLevel = 0;
                element.QuestionIndex = 0;
                element.ChangeLocked(false);
                element.ChangeQuestionThread(false);
                element.OnColorLevelChanged();
                element.ChangeTunnelThread(!element.IsTunnel);
            }

            if (Event.current.type == EventType.KeyDown &&
                rect.Contains(Event.current.mousePosition) && editorDrawContext.m_CellDataDistance.IsTunnel)
            {
                int selectDirectionLevel =
                    Event.current.keyCode switch
                    {
                        KeyCode.LeftArrow => 1,
                        KeyCode.RightArrow => 2,
                        KeyCode.UpArrow => 3,
                        KeyCode.DownArrow => 4,
                        _ => editorDrawContext.m_CellDataDistance.DirectionLevel
                    };

                if (editorDrawContext.m_CellDataDistance.DirectionLevel != selectDirectionLevel)
                {
                    editorDrawContext.m_CellDataDistance.DirectionLevel = selectDirectionLevel;
                    editorDrawContext.m_CellDataDistance.OnDirectionLevelChanged();
                }
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Q &&
                rect.Contains(Event.current.mousePosition))
            {
                element.ColorLevel = 0;
                element.TunnelIndex = 0;
                element.DirectionLevel = 0;
                element.Direction = Direction.None;
                element.ChangeTunnelThread(false);
                element.ChangeLocked(false);
                element.OnColorLevelChanged();
                element.ChangeQuestionThread(!element.IsQuestionThread);
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.L &&
                rect.Contains(Event.current.mousePosition))
            {
                element.ColorLevel = 0;
                element.TunnelIndex = 0;
                element.DirectionLevel = 0;
                element.Direction = Direction.None;
                element.ChangeTunnelThread(false);
                element.ChangeQuestionThread(false);
                element.OnColorLevelChanged();
                element.ChangeLocked(!element.IsLocked);
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space &&
                rect.Contains(Event.current.mousePosition))
            {
                ResetAllCell();
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.R &&
                rect.Contains(Event.current.mousePosition))
            {
                element.ChangeLocked(false);
                ResetCell(element);
            }

            if (element.ColorLevel == 0)
            {
                if (element.IsLocked)
                    EditorGUI.DrawRect(rect.Padding(1), Color.black);

                if (ReferenceEquals(element, editorDrawContext.m_CellDataDistance))
                    DrawOutline(rect, Color.magenta, 4);

                if (element.IsTunnel)
                {
                    editorDrawContext.NumberTunnels();

                    string directionText;
                    switch (element.Direction)
                    {
                        case Direction.Left:
                            directionText = "←" + " T" + element.TunnelIndex;
                            break;
                        case Direction.Right:
                            directionText = "→" + " T" + element.TunnelIndex;
                            break;
                        case Direction.Up:
                            directionText = "↑" + " T" + element.TunnelIndex;
                            break;
                        case Direction.Down:
                            directionText = "↓" + " T" + element.TunnelIndex;
                            break;
                        default:
                            directionText = "NULL" + " T" + element.TunnelIndex;
                            break;
                    }

                    GUIStyle style = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Normal,
                        fontSize = 20,
                    };

                    GUI.Label(rect.Padding(1), directionText, style);
                }

                if (element.IsQuestionThread)
                {
                    editorDrawContext.NumberQuestionThread();
                    string questionText = "Q" + element.QuestionIndex;
                    GUI.Label(rect.Padding(1), questionText, editorDrawContext.m_LevelLabelStyle);
                }

                return element;
            }

            element.ChangeLocked(false);
            element.ChangeQuestionThread(false);
            element.ChangeTunnelThread(false);
            element.QuestionIndex = 0;
            element.Direction = Direction.None;
            element.DirectionLevel = 0;

            EditorGUI.DrawRect(rect.Padding(1), element.FacadeProperty.Color);
            if (ReferenceEquals(element, editorDrawContext.m_CellDataDistance))
                DrawOutline(rect, Color.magenta, 4);
            SetTextProperties();

            return element;
        }

        private static void SetTextProperties()
        {
            if (editorDrawContext.m_LevelLabelStyle == null)
            {
                editorDrawContext.m_LevelLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }

            editorDrawContext.m_LevelLabelStyle.fontSize = 50;
        }

        private static void ResetCell(CellData element)
        {
            element.ColorLevel = 0;
            element.QuestionIndex = 0;
            element.TunnelIndex = 0;
            element.DirectionLevel = 0;
            element.Direction = Direction.None;
            element.ChangeTunnelThread(false);
            element.ChangeLocked(false);
            element.ChangeQuestionThread(false);
            element.OnColorLevelChanged();
        }

        private static void ResetAllCell()
        {
            foreach (var cell in editorDrawContext.m_CellData)
                ResetCell(cell);
        }
#endif

        private void NumberQuestionThread()
        {
            int questionIndex = 1;

            for (int i = 0; i < m_Height; i++)
            {
                for (int j = 0; j < m_Width; j++)
                {
                    var cell = m_CellData[j, i];
                    if (cell.IsQuestionThread)
                    {
                        cell.QuestionIndex = questionIndex;
                        questionIndex++;
                    }
                    else
                    {
                        cell.QuestionIndex = 0;
                    }
                }
            }
        }

        private void NumberTunnels()
        {
            int tunnelIndex = 1;

            for (int i = 0; i < m_Height; i++)
            {
                for (int j = 0; j < m_Width; j++)
                {
                    var cell = m_CellData[j, i];
                    if (cell.IsTunnel)
                    {
                        cell.TunnelIndex = tunnelIndex;
                        tunnelIndex++;
                    }
                    else
                    {
                        cell.TunnelIndex = 0;
                    }
                }
            }
        }

        private void OnWidthChanged()
        {
            if (m_CellData.GetLength(0) != m_Width)
            {
                CellData[,] newArray = new CellData[m_Width, m_Height];
                int minRows = Math.Min(m_Width, m_CellData.GetLength(0));
                int minCols = Math.Min(m_Height, m_CellData.GetLength(1));
                for (int h = 0; h < m_Height; h++)
                for (int w = 0; w < m_Width; w++)
                    newArray[w, h] = new CellData();

                for (int i = 0; i < minRows; i++)
                for (int j = 0; j < minCols; j++)
                    newArray[i, j] = m_CellData[i, j];

                m_CellData = newArray;
            }
        }

        private void OnHeightChanged()
        {
            if (m_CellData.GetLength(1) != m_Height)
            {
                CellData[,] newArray = new CellData[m_Width, m_Height];
                int minRows = Math.Min(m_Width, m_CellData.GetLength(0));
                int minCols = Math.Min(m_Height, m_CellData.GetLength(1));

                for (int h = 0; h < m_Height; h++)
                for (int w = 0; w < m_Width; w++)
                    newArray[w, h] = new CellData();

                for (int i = 0; i < minRows; i++)
                for (int j = 0; j < minCols; j++)
                    newArray[i, j] = m_CellData[i, j];

                m_CellData = newArray;
            }
        }

        /// <summary> Eski level'larda grid Y eksenini çevirir: Inspector üst satır = oyun üstü olacak. </summary>
        public void MigrateGridYIfNeeded()
        {
            if (m_GridYMigrated || m_CellData == null) return;
            int w = m_CellData.GetLength(0), h = m_CellData.GetLength(1);
            var flipped = new CellData[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    flipped[x, h - 1 - y] = m_CellData[x, y];
            m_CellData = flipped;
            m_GridYMigrated = true;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Yanlis top-left migrate uygulanmissa grid'i geri cevirir (y=0 = texture alti).
        /// </summary>
        public void UndoPixelYMigrateIfNeeded()
        {
            if (!m_PixelYMigrated || m_PixelGrid == null)
                return;

            int w = m_PixelGrid.GetLength(0);
            int h = m_PixelGrid.GetLength(1);
            PixelCellData[,] flipped = new PixelCellData[w, h];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                    flipped[x, h - 1 - y] = m_PixelGrid[x, y];
            }

            m_PixelGrid = flipped;
            m_PixelYMigrated = false;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            RefreshPaletteIdMapPreview();
#endif
        }

        [OnInspectorInit]
        private void OnInspectorInit()
        {
#if UNITY_EDITOR
            editorDrawContext = this;
#endif
            MigrateGridYIfNeeded();
            EnsurePixelGrid();
            UndoPixelYMigrateIfNeeded();

            for (int i = 0; i < m_Width; i++)
            for (int j = 0; j < m_Height; j++)
            {
                if (m_CellData[i, j] == null)
                    m_CellData[i, j] = new CellData();
            }

#if UNITY_EDITOR
            RefreshPaletteIdMapPreview();
#endif
        }

        [OnInspectorDispose]
        private void OnInspectorDispose()
        {
#if UNITY_EDITOR
            editorDrawContext = null;
#endif
        }

#if UNITY_EDITOR
        private static void DrawOutline(Rect rect, Color color, int thickness = 2)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        }
#endif

        #endregion

        #region Fabric Pixel

        /// <summary> Fabric piksel grid boyutu (bake / editor). </summary>
        public Vector2Int PixelSize => m_PixelSize;

        /// <summary> Fabric piksel hucreleri [x, y]. Alpha &lt; 10 = bos. </summary>
        public PixelCellData[,] PixelGrid => m_PixelGrid;

        /// <summary> Bu hucre sayisinin uzerinde Inspector matrix gizlenir (Force Visual haric). </summary>
        private const int PixelVisualCellThreshold = 256;

        [FoldoutGroup("Fabric Pixel")]
        [MinValue(1)]
        [OnValueChanged(nameof(OnPixelSizeChanged))]
        [SerializeField]
        private Vector2Int m_PixelSize = new Vector2Int(32, 32);

#if UNITY_EDITOR
        /// <summary> Inspector: bake edilmis paletteId -> renk / ad / adet ozeti. </summary>
        [Serializable]
        private struct BakedPaletteSummaryRow
        {
            [TableColumnWidth(48, Resizable = false)]
            [LabelText("ID")]
            public short paletteId;

            [LabelText("Name")]
            public string colorName;

            [TableColumnWidth(56, Resizable = false)]
            [LabelText("RGB")]
            public Color swatch;

            [TableColumnWidth(72, Resizable = false)]
            [LabelText("Count")]
            public int pixelCount;
        }

        [FoldoutGroup("Fabric Pixel")]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("Palette ID Map")]
        [TableList(
            IsReadOnly = true,
            AlwaysExpanded = true,
            DrawScrollView = false,
            HideToolbar = true,
            ShowIndexLabels = false)]
        [InfoBox("Bake sonrasi dolu olur. ID = FacadeProperty.Level (ornek: 1 = Red).")]
        private List<BakedPaletteSummaryRow> m_PaletteIdMapPreview = new List<BakedPaletteSummaryRow>();
#endif

        [FoldoutGroup("Fabric Pixel")]
        [LabelText("Force Visual")]
        [Tooltip("Acikken esik asilsa bile pixel matrix gosterilir. Buyuk gridlerde Inspector kasabilir.")]
        [SerializeField]
        private bool m_ForcePixelVisual;

        private bool ShouldShowPixelMatrix()
        {
            int cells = Mathf.Max(0, m_PixelSize.x) * Mathf.Max(0, m_PixelSize.y);
            return m_ForcePixelVisual || cells <= PixelVisualCellThreshold;
        }

        private bool ShouldHidePixelMatrix()
        {
            return !ShouldShowPixelMatrix();
        }

        [FoldoutGroup("Fabric Pixel")]
        [SerializeField]
#if UNITY_EDITOR
        [HideInInspector]
#endif
        private PixelCellData[,] m_PixelGrid = new PixelCellData[32, 32];

        [FoldoutGroup("Fabric Pixel")]
        [SerializeField]
        private Color32 m_PixelPaintColor = new Color32(220, 60, 60, 255);

        [FoldoutGroup("Fabric Pixel")]
        [SerializeField]
        [HideInInspector]
        private PixelCellData m_PixelSelection;

#if UNITY_EDITOR
        [FoldoutGroup("Fabric Pixel")]
        [SerializeField]
        [Tooltip("Bake icin kaynak texture (Read/Write acik olmali).")]
        private Texture2D m_PixelBakeSource;

        [FoldoutGroup("Fabric Pixel")]
        [SerializeField]
        private bool m_PixelBakeDownsample = true;

        [FoldoutGroup("Fabric Pixel")]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("Opaque Pixel Count")]
        private int OpaquePixelCountPreview => CountOpaquePixels();

        [FoldoutGroup("Fabric Pixel")]
        [ShowIf("ShouldHidePixelMatrix")]
        [OnInspectorGUI]
        private void DrawPixelMatrixHiddenNotice()
        {
            EditorGUILayout.HelpBox(
                "Pixel grid >256 hucre; Inspector'da gizlendi. Gormek icin Force Visual'i ac.",
                MessageType.Warning);
        }

        /// <summary>
        /// Inspector: Unity y=0 altta; UI ustte gosterir — sadece cizim flip (veri degismez).
        /// </summary>
        [FoldoutGroup("Fabric Pixel")]
        [ShowIf("ShouldShowPixelMatrix")]
        [OnInspectorGUI]
        private void DrawPixelMatrixInspector()
        {
            if (m_PixelGrid == null)
                return;

            int w = m_PixelGrid.GetLength(0);
            int h = m_PixelGrid.GetLength(1);
            if (w < 1 || h < 1)
                return;

            float available = EditorGUIUtility.currentViewWidth - 48f;
            float cellSize = Mathf.Clamp(Mathf.Floor(available / w), 6f, 28f);
            Rect area = GUILayoutUtility.GetRect(w * cellSize, h * cellSize, GUILayout.ExpandWidth(false));

            for (int displayRow = 0; displayRow < h; displayRow++)
            {
                int y = h - 1 - displayRow;
                for (int x = 0; x < w; x++)
                {
                    Rect cellRect = new Rect(
                        area.x + x * cellSize,
                        area.y + displayRow * cellSize,
                        cellSize,
                        cellSize);

                    PixelCellData cell = m_PixelGrid[x, y];
                    DrawPixelCellInteractive(cellRect, ref cell);
                    m_PixelGrid[x, y] = cell;
                }
            }
        }

        [FoldoutGroup("Fabric Pixel")]
        [Button("Bake From Texture", ButtonSizes.Large)]
        [GUIColor(0.4f, 0.85f, 0.55f)]
        private void BakePixelFromTextureButton()
        {
            if (m_PixelBakeSource == null)
            {
                Debug.LogError("[LevelData] Fabric Pixel: Texture atanmadi.");
                return;
            }

            if (!m_PixelBakeSource.isReadable)
            {
                Debug.LogError("[LevelData] Fabric Pixel: Texture Read/Write kapali.");
                return;
            }

            Color32[] raw = m_PixelBakeSource.GetPixels32();
            List<PixelData> baked;
            Vector2Int size;

            PixelBakeUtility bakeUtility = new PixelBakeUtility();
            if (m_PixelBakeDownsample)
            {
                size = m_PixelSize;
                if (size.x < 1 || size.y < 1)
                    size = new Vector2Int(32, 32);
                baked = bakeUtility.BakeDownsampled(
                    raw, m_PixelBakeSource.width, m_PixelBakeSource.height, size);
            }
            else
            {
                size = new Vector2Int(m_PixelBakeSource.width, m_PixelBakeSource.height);
                baked = bakeUtility.BakeFullResolution(
                    raw, m_PixelBakeSource.width, m_PixelBakeSource.height);
            }

            // Edit-time paletteId: Facade Level (nearest RGB burada).
            ProjectSettings settings = ProjectSettings.Load();
            if (settings != null)
            {
                FacadePaletteMapper mapper = new FacadePaletteMapper();
                mapper.BuildFromFacades(settings.GetOriginalFacadeProperties());
                mapper.AssignPaletteIds(baked);
            }
            else
            {
                Debug.LogWarning("[LevelData] ProjectSettings yok; paletteId atanamadi.");
            }

            ApplyPixelBake(baked, size);
            EditorUtility.SetDirty(this);
            Debug.Log($"[LevelData] Fabric Pixel bake: {baked.Count} opak / {size.x}x{size.y}");
        }

        [FoldoutGroup("Fabric Pixel")]
        [Button("Import LevelPixelData Asset")]
        private void ImportLevelPixelDataButton()
        {
            string path = EditorUtility.OpenFilePanel(
                "Select LevelPixelData",
                "Assets/Resources",
                "asset");
            if (string.IsNullOrEmpty(path))
                return;

            if (path.StartsWith(Application.dataPath))
                path = "Assets" + path.Substring(Application.dataPath.Length);

            LevelPixelData source = AssetDatabase.LoadAssetAtPath<LevelPixelData>(path);
            if (source == null)
            {
                Debug.LogError("[LevelData] Secilen asset LevelPixelData degil.");
                return;
            }

            ApplyPixelBake(source.Pixels, source.TextureSize);
            EditorUtility.SetDirty(this);
            Debug.Log($"[LevelData] LevelPixelData import: {source.Pixels.Count} piksel");
        }

        [FoldoutGroup("Fabric Pixel")]
        [Button("Clear All Pixels")]
        [GUIColor(0.9f, 0.45f, 0.45f)]
        private void ClearAllPixelsButton()
        {
            ResetAllPixels();
            RefreshPaletteIdMapPreview();
            EditorUtility.SetDirty(this);
        }

        [FoldoutGroup("Fabric Pixel")]
        [Button("Refresh Palette ID Map")]
        private void RefreshPaletteIdMapButton()
        {
            RefreshPaletteIdMapPreview();
        }

        /// <summary>
        /// Grid'deki paletteId dagilimini ozetler (ID -> facade adi / renk / adet).
        /// </summary>
        private void RefreshPaletteIdMapPreview()
        {
            m_PaletteIdMapPreview.Clear();
            if (m_PixelGrid == null)
                return;

            // Editor-only sayac: id -> count.
            Dictionary<short, int> counts = new Dictionary<short, int>(16);
            Dictionary<short, Color32> sampleColor = new Dictionary<short, Color32>(16);

            int w = m_PixelGrid.GetLength(0);
            int h = m_PixelGrid.GetLength(1);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    PixelCellData cell = m_PixelGrid[x, y];
                    if (cell == null || !cell.HasPixel)
                        continue;

                    short id = cell.PaletteId;
                    if (id < 0)
                        continue;

                    if (counts.TryGetValue(id, out int c))
                        counts[id] = c + 1;
                    else
                    {
                        counts[id] = 1;
                        sampleColor[id] = cell.Color;
                    }
                }
            }

            Dictionary<int, string> levelNames = new Dictionary<int, string>(16);
            Dictionary<int, Color> levelColors = new Dictionary<int, Color>(16);
            ProjectSettings settings = ProjectSettings.Load();
            if (settings != null)
            {
                List<FacadeProperty> facades = settings.GetOriginalFacadeProperties();
                if (facades != null)
                {
                    for (int i = 0; i < facades.Count; i++)
                    {
                        FacadeProperty f = facades[i];
                        if (f == null || f.Level <= 0)
                            continue;
                        levelNames[f.Level] = string.IsNullOrEmpty(f.Name) ? f.Level.ToString() : f.Name;
                        levelColors[f.Level] = f.Color;
                    }
                }
            }

            List<short> sortedIds = new List<short>(counts.Keys);
            sortedIds.Sort();
            for (int i = 0; i < sortedIds.Count; i++)
            {
                short id = sortedIds[i];
                string name = levelNames.TryGetValue(id, out string n) ? n : ("Level " + id);
                Color swatch = levelColors.TryGetValue(id, out Color fc)
                    ? fc
                    : (Color)sampleColor[id];

                m_PaletteIdMapPreview.Add(new BakedPaletteSummaryRow
                {
                    paletteId = id,
                    colorName = name,
                    swatch = swatch,
                    pixelCount = counts[id]
                });
            }
        }
#endif

        /// <summary> Opak (gorunur) piksel sayisi. </summary>
        public int CountOpaquePixels()
        {
            if (m_PixelGrid == null)
                return 0;

            int count = 0;
            int w = m_PixelGrid.GetLength(0);
            int h = m_PixelGrid.GetLength(1);
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                PixelCellData cell = m_PixelGrid[x, y];
                if (cell != null && cell.HasPixel)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Runtime FabricGridRenderer icin opak pikselleri destination listesine yazar (once Clear).
        /// Allocation: sadece liste buyumesinde; kapasite onceden ayarlanmali.
        /// </summary>
        public void CopyOpaquePixelsTo(List<PixelData> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            if (m_PixelGrid == null)
                return;

            int w = m_PixelGrid.GetLength(0);
            int h = m_PixelGrid.GetLength(1);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    PixelCellData cell = m_PixelGrid[x, y];
                    if (cell == null || !cell.HasPixel)
                        continue;

                    destination.Add(new PixelData
                    {
                        position = new Vector2Int(x, y),
                        color = cell.Color,
                        paletteId = cell.PaletteId
                    });
                }
            }
        }

        /// <summary> Bake / import sonucunu PixelGrid'e uygular (color + paletteId). </summary>
        public void ApplyPixelBake(List<PixelData> opaquePixels, Vector2Int size)
        {
            m_PixelSize = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            PixelBakeUtility bakeUtility = new PixelBakeUtility();
            m_PixelGrid = bakeUtility.CreatePixelGrid(opaquePixels, m_PixelSize);
            // Yeni bake her zaman Unity GetPixels32 konvansiyonu (y=0 alti).
            m_PixelYMigrated = false;
#if UNITY_EDITOR
            RefreshPaletteIdMapPreview();
#endif
        }

        private void EnsurePixelGrid()
        {
            int w = Mathf.Max(1, m_PixelSize.x);
            int h = Mathf.Max(1, m_PixelSize.y);
            m_PixelSize = new Vector2Int(w, h);

            if (m_PixelGrid == null || m_PixelGrid.GetLength(0) != w || m_PixelGrid.GetLength(1) != h)
            {
                PixelCellData[,] old = m_PixelGrid;
                int oldW = old?.GetLength(0) ?? 0;
                int oldH = old?.GetLength(1) ?? 0;
                m_PixelGrid = new PixelCellData[w, h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (x < oldW && y < oldH && old[x, y] != null)
                        m_PixelGrid[x, y] = old[x, y];
                    else
                        m_PixelGrid[x, y] = new PixelCellData();
                }
            }
            else
            {
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (m_PixelGrid[x, y] == null)
                        m_PixelGrid[x, y] = new PixelCellData();
                }
            }
        }

        private void OnPixelSizeChanged()
        {
            EnsurePixelGrid();
        }

#if UNITY_EDITOR
        private void DrawPixelCellInteractive(Rect rect, ref PixelCellData element)
        {
            if (element == null)
                element = new PixelCellData();

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                GUI.changed = true;
                Event.current.Use();
                m_PixelSelection = element;

                if (Event.current.alt)
                {
                    m_PixelPaintColor = element.Color;
                }
                else
                {
                    Color32 paint = m_PixelPaintColor;
                    short paletteId = -1;
                    ProjectSettings settings = ProjectSettings.Load();
                    if (settings != null)
                    {
                        FacadePaletteMapper mapper = new FacadePaletteMapper();
                        mapper.BuildFromFacades(settings.GetOriginalFacadeProperties());
                        paletteId = mapper.ResolveLevel(paint);
                    }

                    element.SetPixel(paint, paletteId);
                    EditorUtility.SetDirty(this);
                }
            }

            if (Event.current.type == EventType.KeyDown && rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.keyCode == KeyCode.R)
                {
                    element.Clear();
                    EditorUtility.SetDirty(this);
                    Event.current.Use();
                }
                else if (Event.current.keyCode == KeyCode.Space)
                {
                    ResetAllPixels();
                    Event.current.Use();
                }
            }

            Color fill = element.HasPixel
                ? (Color)element.Color
                : new Color(0.12f, 0.12f, 0.12f, 1f);
            EditorGUI.DrawRect(rect.Padding(1), fill);

            if (ReferenceEquals(element, m_PixelSelection))
                DrawOutline(rect, Color.magenta, 3);
        }

        private void ResetAllPixels()
        {
            if (m_PixelGrid == null)
                return;

            foreach (PixelCellData cell in m_PixelGrid)
            {
                if (cell != null)
                    cell.Clear();
            }

            EditorUtility.SetDirty(this);
        }
#endif

        #endregion

        #region Grid Helper

        [SerializeField] [FoldoutGroup("Grid Helper")]
        private List<ThreadData> m_QuestionThreadData;

        [SerializeField] [FoldoutGroup("Grid Helper")]
        private List<TunnelData> m_TunnelThreadData;

        public List<ThreadData> QuestionThreadData => m_QuestionThreadData;

        public List<TunnelData> TunnelThreadData => m_TunnelThreadData;

        #endregion

        #region Slot

        [SerializeField] [FoldoutGroup("Slot")]
        private SlotData m_SlotData;

        public SlotData SlotData => m_SlotData;

        #endregion

        #region Plate

        [SerializeField]
        [FoldoutGroup("Plate")]
        [Tooltip("Level basina conveyor tabak (plate) sayisi. Kuyrukta dik dizilir.")]
        [MinValue(1)]
        private int m_PlateCount = 5;

        /// <summary> Mevcut asset'lerde 0 ise runtime default (5) kullanilir. </summary>
        public int PlateCount => m_PlateCount > 0 ? m_PlateCount : 5;

        #endregion
    }
}
