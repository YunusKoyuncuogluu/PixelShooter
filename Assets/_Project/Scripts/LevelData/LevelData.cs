using System;
using System.Collections.Generic;
using Grids;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
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

        private static LevelData editorDrawContext;
        internal ShooterShotPlan editorShotPlan;
        private int editorShotPlanFrame = -1;

        [PropertyOrder(-1000)]
        [Button("Generate Random Level", ButtonSizes.Large)]
        private void GenerateRandomLevelButton()
        {
            new LevelRandomGenerator(this).Generate();
        }
#endif

        #region Grid

        [FoldoutGroup("Grid")] [OnValueChanged(nameof(OnWidthChanged))] [SerializeField]
        [FormerlySerializedAs("m_Width")]
        internal int width = 5;

        [FoldoutGroup("Grid")] [OnValueChanged(nameof(OnHeightChanged))] [SerializeField]
        [FormerlySerializedAs("m_Height")]
        internal int height = 5;

        public int Width => this.width;
        public int Height => this.height;

        public CellData[,] CellData => cellData;

        public int GameYToArrayRow(int gameY) => this.height - 1 - gameY;

        [FoldoutGroup("Grid")]
        [SerializeField]
#if UNITY_EDITOR
        [TableMatrix(HideColumnIndices = false, HideRowIndices = true, SquareCells = true, DrawElementMethod = nameof(DrawCell))]
#endif
        [FormerlySerializedAs("m_CellData")]
        private CellData[,] cellData = new CellData[5, 5];

        [FoldoutGroup("Grid")] [SerializeField]
        [FormerlySerializedAs("m_CellDataDistance")]
        private CellData cellDataDistance = null;

        [SerializeField, HideInInspector]
        [FormerlySerializedAs("m_GridYMigrated")]
        internal bool gridYMigrated;

        [SerializeField, HideInInspector]
        [FormerlySerializedAs("m_PixelYMigrated")]
        private bool pixelYMigrated;

#if UNITY_EDITOR
        private GUIStyle levelLabelStyle;
        private GUIStyle shotCountStyle;

        private static CellData DrawCell(Rect rect, ref CellData element)
        {
            if (Event.current.type == EventType.KeyDown && rect.Contains(Event.current.mousePosition) && editorDrawContext.cellDataDistance != null)
            {
                if (Event.current.keyCode == KeyCode.Alpha0 || Event.current.keyCode == KeyCode.Keypad0)
                {
                    editorDrawContext.ToggleWall(editorDrawContext.cellDataDistance);
                    editorDrawContext.editorShotPlan = null;
                    GUI.changed = true;
                    Event.current.Use();
                }
                else
                {
                    editorDrawContext.cellDataDistance.ColorLevel = Event.current.keyCode switch
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
                        _ => editorDrawContext.cellDataDistance.ColorLevel
                    };

                    editorDrawContext.cellDataDistance.OnColorLevelChanged();
                    editorDrawContext.editorShotPlan = null;
                }
            }

            if (Event.current.type == EventType.ScrollWheel &&
                rect.Contains(Event.current.mousePosition) &&
                element != null &&
                (element.ColorLevel != 0 || element.IsQuestionShooter))
            {
                int delta = Event.current.delta.y < 0f ? 1 : -1;
                editorDrawContext.EnsureShotPlan();
                int current = editorDrawContext.editorShotPlan.ForCell(element);
                element.SetShotCount(current + delta);
                editorDrawContext.editorShotPlan = null;
                GUI.changed = true;
                Event.current.Use();
            }

            if (Event.current.type == EventType.KeyDown &&
                rect.Contains(Event.current.mousePosition) &&
                element != null &&
                (element.ColorLevel != 0 || element.IsQuestionShooter) &&
                (Event.current.keyCode == KeyCode.Equals ||
                 Event.current.keyCode == KeyCode.KeypadPlus ||
                 Event.current.keyCode == KeyCode.Minus ||
                 Event.current.keyCode == KeyCode.KeypadMinus))
            {
                int delta = Event.current.keyCode == KeyCode.Minus || Event.current.keyCode == KeyCode.KeypadMinus ? -1 : 1;
                editorDrawContext.EnsureShotPlan();
                int current = editorDrawContext.editorShotPlan.ForCell(element);
                element.SetShotCount(current + delta);
                editorDrawContext.editorShotPlan = null;
                GUI.changed = true;
                Event.current.Use();
            }

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                GUI.changed = true;
                Event.current.Use();
                editorDrawContext.cellDataDistance = element;
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.T &&
                rect.Contains(Event.current.mousePosition))
            {
                element.ColorLevel = 0;
                element.QuestionIndex = 0;
                element.ChangeLocked(false);
                editorDrawContext.SetQuestionCell(element, false);
                element.OnColorLevelChanged();
                editorDrawContext.SetTunnelCell(element, !element.IsTunnel);
                editorDrawContext.editorShotPlan = null;
            }

            if (Event.current.type == EventType.KeyDown &&
                rect.Contains(Event.current.mousePosition) && editorDrawContext.cellDataDistance.IsTunnel)
            {
                int selectDirectionLevel =
                    Event.current.keyCode switch
                    {
                        KeyCode.LeftArrow => 1,
                        KeyCode.RightArrow => 2,
                        KeyCode.UpArrow => 3,
                        KeyCode.DownArrow => 4,
                        _ => editorDrawContext.cellDataDistance.DirectionLevel
                    };

                if (editorDrawContext.cellDataDistance.DirectionLevel != selectDirectionLevel)
                {
                    editorDrawContext.cellDataDistance.DirectionLevel = selectDirectionLevel;
                    editorDrawContext.cellDataDistance.OnDirectionLevelChanged();
                }
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Q &&
                rect.Contains(Event.current.mousePosition))
            {
                element.ColorLevel = 0;
                element.TunnelIndex = 0;
                element.DirectionLevel = 0;
                element.Direction = Direction.None;
                editorDrawContext.SetTunnelCell(element, false);
                element.ChangeLocked(false);
                element.OnColorLevelChanged();
                editorDrawContext.SetQuestionCell(element, !element.IsQuestionShooter);
                editorDrawContext.editorShotPlan = null;
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.L &&
                rect.Contains(Event.current.mousePosition))
            {
                editorDrawContext.ToggleWall(element);
                editorDrawContext.editorShotPlan = null;
                GUI.changed = true;
                Event.current.Use();
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space &&
                rect.Contains(Event.current.mousePosition))
            {
                editorDrawContext.ResetAllCell();
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.R &&
                rect.Contains(Event.current.mousePosition))
            {
                element.ChangeLocked(false);
                editorDrawContext.ResetCell(element);
            }

            if (element.ColorLevel == 0)
            {
                if (element.IsLocked)
                    EditorGUI.DrawRect(rect.Padding(1), Color.black);

                if (ReferenceEquals(element, editorDrawContext.cellDataDistance))
                    editorDrawContext.DrawOutline(rect, Color.magenta, 4);

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

                if (element.IsQuestionShooter)
                {
                    editorDrawContext.NumberQuestionShooter();
                    string questionText = "Q" + element.QuestionIndex;
                    GUI.Label(rect.Padding(1), questionText, editorDrawContext.levelLabelStyle);
                    editorDrawContext.DrawShotCount(rect, element);
                }

                return element;
            }

            element.ChangeLocked(false);
            editorDrawContext.SetQuestionCell(element, false);
            editorDrawContext.SetTunnelCell(element, false);
            element.QuestionIndex = 0;
            element.Direction = Direction.None;
            element.DirectionLevel = 0;

            Color shooterColor = editorDrawContext.ResolveDisplayedColor(element.ColorLevel, element.FacadeProperty);
            EditorGUI.DrawRect(rect.Padding(1), shooterColor);
            if (ReferenceEquals(element, editorDrawContext.cellDataDistance))
                editorDrawContext.DrawOutline(rect, Color.magenta, 4);
            editorDrawContext.SetTextProperties();
            editorDrawContext.DrawShotCount(rect, element);

            return element;
        }

        private void EnsureShotPlan()
        {
            if (editorShotPlan != null && editorShotPlanFrame == Time.frameCount)
                return;

            editorShotPlan = BuildShotPlan();
            editorShotPlanFrame = Time.frameCount;
        }

        private void DrawShotCount(Rect rect, CellData element)
        {
            EnsureShotPlan();

            int shots = editorShotPlan.ForCell(element);
            if (shotCountStyle == null)
            {
                shotCountStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }

            shotCountStyle.fontSize = Mathf.Max(10, Mathf.RoundToInt(rect.height * 0.42f));
            shotCountStyle.normal.textColor = Color.white;
            Rect labelRect = element.IsQuestionShooter
                ? new Rect(rect.x, rect.yMax - rect.height * 0.42f, rect.width, rect.height * 0.42f)
                : rect;
            GUI.Label(labelRect, shots.ToString(), shotCountStyle);
        }

        private void SetTextProperties()
        {
            if (levelLabelStyle == null)
            {
                levelLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }

            levelLabelStyle.fontSize = 50;
        }

        private void ResetCell(CellData element)
        {
            element.ColorLevel = 0;
            element.QuestionIndex = 0;
            element.TunnelIndex = 0;
            element.DirectionLevel = 0;
            element.Direction = Direction.None;
            SetTunnelCell(element, false);
            element.ChangeLocked(false);
            SetQuestionCell(element, false);
            element.SetShotCount(0);
            element.OnColorLevelChanged();
            editorShotPlan = null;
        }

        private void ResetAllCell()
        {
            foreach (var cell in cellData)
                ResetCell(cell);
        }
#endif

        public void ToggleWall(CellData cell)
        {
            if (cell == null)
                return;

            bool placeWall = !cell.IsLocked;
            cell.ColorLevel = 0;
            cell.QuestionIndex = 0;
            cell.TunnelIndex = 0;
            cell.DirectionLevel = 0;
            cell.Direction = Direction.None;
            cell.SetShotCount(0);
            SetTunnelCell(cell, false);
            SetQuestionCell(cell, false);
            cell.OnColorLevelChanged();
            cell.ChangeLocked(placeWall);
        }

        public void SetTunnelCell(CellData cell, bool enabled)
        {
            if (cell == null || cell.IsTunnel == enabled)
                return;

            if (tunnelShooterData == null)
                tunnelShooterData = new List<TunnelData>();

            if (enabled)
            {
                int index = CountTunnelsBefore(cell);
                if (index > tunnelShooterData.Count)
                    index = tunnelShooterData.Count;

                tunnelShooterData.Insert(index, TunnelData.CreateDefault());
                cell.SetTunnelShooter(true);
                return;
            }

            int removeIndex = CountTunnelsBefore(cell);
            cell.SetTunnelShooter(false);
            if (removeIndex >= 0 && removeIndex < tunnelShooterData.Count)
                tunnelShooterData.RemoveAt(removeIndex);
        }

        public void RepairTunnelList()
        {
            int needed = CountTunnels();
            if (tunnelShooterData == null)
                tunnelShooterData = new List<TunnelData>();

            if (tunnelShooterData.Count == needed)
                return;

            while (tunnelShooterData.Count < needed)
                tunnelShooterData.Add(TunnelData.CreateDefault());

            while (tunnelShooterData.Count > needed)
                tunnelShooterData.RemoveAt(tunnelShooterData.Count - 1);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private int CountTunnels()
        {
            if (cellData == null)
                return 0;

            int count = 0;
            int rows = cellData.GetLength(1);
            int columns = cellData.GetLength(0);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    CellData cell = cellData[column, row];
                    if (cell != null && cell.IsTunnel)
                        count++;
                }
            }

            return count;
        }

        internal int CountTunnelsBefore(CellData target)
        {
            if (cellData == null || target == null)
                return 0;

            int count = 0;
            for (int gameY = 0; gameY < this.height; gameY++)
            {
                int row = GameYToArrayRow(gameY);
                if (row < 0 || row >= cellData.GetLength(1))
                    continue;

                for (int x = 0; x < this.width; x++)
                {
                    if (x >= cellData.GetLength(0))
                        continue;

                    CellData cell = cellData[x, row];
                    if (ReferenceEquals(cell, target))
                        return count;

                    if (cell != null && cell.IsTunnel)
                        count++;
                }
            }

            return count;
        }

        public void SetQuestionCell(CellData cell, bool enabled)
        {
            if (cell == null || cell.IsQuestionShooter == enabled)
                return;

            if (questionShooterData == null)
                questionShooterData = new List<ShooterData>();

            if (enabled)
            {
                int index = CountQuestionShootersBefore(cell);
                if (index > questionShooterData.Count)
                    index = questionShooterData.Count;

                questionShooterData.Insert(index, new ShooterData(GeneralColor.Red, true, Vector2Int.zero, Vector3.zero));
                cell.SetQuestionShooter(true);
                return;
            }

            int removeIndex = CountQuestionShootersBefore(cell);
            cell.SetQuestionShooter(false);
            if (removeIndex >= 0 && removeIndex < questionShooterData.Count)
                questionShooterData.RemoveAt(removeIndex);
        }

        public void RepairQuestionShooterList()
        {
            int needed = CountQuestionShooters();
            if (questionShooterData == null)
                questionShooterData = new List<ShooterData>();

            if (questionShooterData.Count == needed)
                return;

            while (questionShooterData.Count < needed)
                questionShooterData.Add(new ShooterData(GeneralColor.Red, true, Vector2Int.zero, Vector3.zero));

            while (questionShooterData.Count > needed)
                questionShooterData.RemoveAt(questionShooterData.Count - 1);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private int CountQuestionShooters()
        {
            if (cellData == null)
                return 0;

            int count = 0;
            int rows = cellData.GetLength(1);
            int columns = cellData.GetLength(0);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    CellData cell = cellData[column, row];
                    if (cell != null && cell.IsQuestionShooter)
                        count++;
                }
            }

            return count;
        }

        internal int CountQuestionShootersBefore(CellData target)
        {
            if (cellData == null || target == null)
                return 0;

            int count = 0;
            for (int gameY = 0; gameY < this.height; gameY++)
            {
                int row = GameYToArrayRow(gameY);
                if (row < 0 || row >= cellData.GetLength(1))
                    continue;

                for (int x = 0; x < this.width; x++)
                {
                    if (x >= cellData.GetLength(0))
                        continue;

                    CellData cell = cellData[x, row];
                    if (ReferenceEquals(cell, target))
                        return count;

                    if (cell != null && cell.IsQuestionShooter)
                        count++;
                }
            }

            return count;
        }

        private void NumberQuestionShooter()
        {
            int questionIndex = 1;

            for (int i = 0; i < this.height; i++)
            {
                for (int j = 0; j < this.width; j++)
                {
                    var cell = cellData[j, i];
                    if (cell.IsQuestionShooter)
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

            for (int i = 0; i < this.height; i++)
            {
                for (int j = 0; j < this.width; j++)
                {
                    var cell = cellData[j, i];
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

        internal void OnWidthChanged()
        {
            if (cellData.GetLength(0) != this.width)
            {
                CellData[,] newArray = new CellData[this.width, this.height];
                int minRows = Math.Min(this.width, cellData.GetLength(0));
                int minCols = Math.Min(this.height, cellData.GetLength(1));
                for (int h = 0; h < this.height; h++)
                {
                    for (int w = 0; w < this.width; w++)
                        newArray[w, h] = new CellData();
                }

                for (int i = 0; i < minRows; i++)
                {
                    for (int j = 0; j < minCols; j++)
                        newArray[i, j] = cellData[i, j];
                }

                cellData = newArray;
            }
        }

        internal void OnHeightChanged()
        {
            if (cellData.GetLength(1) != this.height)
            {
                CellData[,] newArray = new CellData[this.width, this.height];
                int minRows = Math.Min(this.width, cellData.GetLength(0));
                int minCols = Math.Min(this.height, cellData.GetLength(1));

                for (int h = 0; h < this.height; h++)
                {
                    for (int w = 0; w < this.width; w++)
                        newArray[w, h] = new CellData();
                }

                for (int i = 0; i < minRows; i++)
                {
                    for (int j = 0; j < minCols; j++)
                        newArray[i, j] = cellData[i, j];
                }

                cellData = newArray;
            }
        }

        public void MigrateGridYIfNeeded()
        {
            if (gridYMigrated || cellData == null)
                return;
            int w = cellData.GetLength(0), h = cellData.GetLength(1);
            var flipped = new CellData[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    flipped[x, h - 1 - y] = cellData[x, y];
            cellData = flipped;
            gridYMigrated = true;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public void UndoPixelYMigrateIfNeeded()
        {
            if (!pixelYMigrated || pixelGrid == null)
                return;

            int w = pixelGrid.GetLength(0);
            int h = pixelGrid.GetLength(1);
            PixelCellData[,] flipped = new PixelCellData[w, h];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                    flipped[x, h - 1 - y] = pixelGrid[x, y];
            }

            pixelGrid = flipped;
            pixelYMigrated = false;
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
            RepairQuestionShooterList();
            RepairTunnelList();
            MigrateGridYIfNeeded();
            EnsurePixelGrid();
            UndoPixelYMigrateIfNeeded();

            for (int i = 0; i < this.width; i++)
            {
                for (int j = 0; j < this.height; j++)
                {
                    if (cellData[i, j] == null)
                        cellData[i, j] = new CellData();
                }
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
        private void DrawOutline(Rect rect, Color color, int thickness = 2)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        }
#endif

        #endregion

        #region Image Pixel

        public Vector2Int PixelSize => pixelSize;

        public PixelCellData[,] PixelGrid => pixelGrid;

        private const int PixelVisualCellThreshold = 256;

        [FoldoutGroup("Image Pixel")]
        [MinValue(1)]
        [OnValueChanged(nameof(OnPixelSizeChanged))]
        [SerializeField]
        [FormerlySerializedAs("m_PixelSize")]
        private Vector2Int pixelSize = new Vector2Int(32, 32);

        [FoldoutGroup("Image Pixel")]
        [LabelText("Palette ID Map")]
        [TableList(AlwaysExpanded = true, DrawScrollView = false, HideToolbar = true, ShowIndexLabels = false)]
#if UNITY_EDITOR
        [OnValueChanged(nameof(OnPaletteRowsChanged), true)]
#endif
        [SerializeField]
        [FormerlySerializedAs("m_PaletteRows")]
        private List<LevelPaletteRow> paletteRows = new List<LevelPaletteRow>();

#if UNITY_EDITOR
        [SerializeField]
        [HideInInspector]
        private List<Color> ignoredBakeColors = new List<Color>();

        [SerializeField]
        [HideInInspector]
        private int ignoreColorDistance = 30;

        [FoldoutGroup("Image Pixel")]
        [OnInspectorGUI]
        private void DrawIgnoredBakeColors()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Ignore Colors", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Ignore ile secilen renge yakin pikseller okunmaz. Bake, kalan sekli kirpip gridin tamamina sigdirir.",
                MessageType.None);

            if (paletteRows != null)
            {
                for (int i = 0; i < paletteRows.Count; i++)
                {
                    LevelPaletteRow row = paletteRows[i];
                    if (row == null)
                        continue;

                    EditorGUILayout.BeginHorizontal();
                    Rect swatchRect = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18f), GUILayout.Height(18f));
                    EditorGUI.DrawRect(swatchRect, row.swatch);
                    EditorGUILayout.LabelField(row.colorName + "  " + row.pixelCount);
                    if (GUILayout.Button("Ignore", GUILayout.Width(72f)))
                        AddIgnoredBakeColor(row.swatch);
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (ignoredBakeColors == null)
                ignoredBakeColors = new List<Color>();

            for (int i = 0; i < ignoredBakeColors.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                Color edited = EditorGUILayout.ColorField(ignoredBakeColors[i]);
                if (edited != ignoredBakeColors[i])
                {
                    Undo.RecordObject(this, "Edit Ignored Color");
                    ignoredBakeColors[i] = edited;
                    EditorUtility.SetDirty(this);
                }

                if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                {
                    Undo.RecordObject(this, "Remove Ignored Color");
                    ignoredBakeColors.RemoveAt(i);
                    EditorUtility.SetDirty(this);
                    BakePixelFromTextureButton();
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.BeginChangeCheck();
            int distance = EditorGUILayout.IntSlider("Ignore Distance", ignoreColorDistance, 0, 90);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Ignore Distance");
                ignoreColorDistance = distance;
                EditorUtility.SetDirty(this);
            }
        }

        private void AddIgnoredBakeColor(Color color)
        {
            if (ignoredBakeColors == null)
                ignoredBakeColors = new List<Color>();

            Color32 incoming = color;
            int limit = ignoreColorDistance * ignoreColorDistance;
            for (int i = 0; i < ignoredBakeColors.Count; i++)
            {
                Color32 existing = ignoredBakeColors[i];
                int dr = incoming.r - existing.r;
                int dg = incoming.g - existing.g;
                int db = incoming.b - existing.b;
                if (dr * dr + dg * dg + db * db <= limit)
                    return;
            }

            Undo.RecordObject(this, "Ignore Bake Color");
            ignoredBakeColors.Add(color);
            EditorUtility.SetDirty(this);
            BakePixelFromTextureButton();
        }

        private Color32[] CopyIgnoredBakeColors()
        {
            if (ignoredBakeColors == null || ignoredBakeColors.Count == 0)
                return null;

            Color32[] colors = new Color32[ignoredBakeColors.Count];
            for (int i = 0; i < ignoredBakeColors.Count; i++)
                colors[i] = ignoredBakeColors[i];

            return colors;
        }
#endif

        [FoldoutGroup("Image Pixel")]
        [LabelText("Force Visual")]
        [Tooltip("Acikken esik asilsa bile pixel matrix gosterilir. Buyuk gridlerde Inspector kasabilir.")]
        [SerializeField]
        [FormerlySerializedAs("m_ForcePixelVisual")]
        private bool forcePixelVisual;

        private bool ShouldShowPixelMatrix()
        {
            int cells = Mathf.Max(0, pixelSize.x) * Mathf.Max(0, pixelSize.y);
            return forcePixelVisual || cells <= PixelVisualCellThreshold;
        }

        private bool ShouldHidePixelMatrix()
        {
            return !ShouldShowPixelMatrix();
        }

        [FoldoutGroup("Image Pixel")]
        [SerializeField]
#if UNITY_EDITOR
        [HideInInspector]
#endif
        [FormerlySerializedAs("m_PixelGrid")]
        private PixelCellData[,] pixelGrid = new PixelCellData[32, 32];

        [FoldoutGroup("Image Pixel")]
        [SerializeField]
        [FormerlySerializedAs("m_PixelPaintColor")]
        private Color32 pixelPaintColor = new Color32(220, 60, 60, 255);

        [FoldoutGroup("Image Pixel")]
        [SerializeField]
        [HideInInspector]
        [FormerlySerializedAs("m_PixelSelection")]
        private PixelCellData pixelSelection;

#if UNITY_EDITOR
        [FoldoutGroup("Image Pixel")]
        [SerializeField]
        [Tooltip("Bake icin kaynak texture (Read/Write acik olmali).")]
        [FormerlySerializedAs("m_PixelBakeSource")]
        private Texture2D pixelBakeSource;

        [FoldoutGroup("Image Pixel")]
        [SerializeField]
        [FormerlySerializedAs("m_PixelBakeDownsample")]
        private bool pixelBakeDownsample = true;

        [FoldoutGroup("Image Pixel")]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("Opaque Pixel Count")]
        private int OpaquePixelCountPreview => CountOpaquePixels();

        [FoldoutGroup("Image Pixel")]
        [OnInspectorGUI]
        private void DrawShotBalanceInspector()
        {
            EnsureShotPlan();

            ShooterShotPlan plan = editorShotPlan;
            string message = plan.IsBalanced
                ? "Shooter shot counts match the image pixel counts."
                : "Shot counts do not match pixel counts per color. The button splits image pixels across shooters of that color.";

            EditorGUILayout.HelpBox(message, plan.IsBalanced ? MessageType.Info : MessageType.Warning);

            List<ShooterShotPlan.Row> rows = plan.Rows;
            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    ShooterShotPlan.Row row = rows[i];
                    string mark = row.Pixels == row.Shots ? "match" : "mismatch";
                    EditorGUILayout.LabelField(
                        "Color " + row.ColorId,
                        "pixels " + row.Pixels + "  shots " + row.Shots + "  shooters " + row.Shooters + "  " + mark);
                }
            }

            if (GUILayout.Button("Match Shooter Shots To Image Pixels"))
            {
                ApplyShotPlan();
                editorShotPlan = null;
                EditorUtility.SetDirty(this);
            }
        }

        [FoldoutGroup("Image Pixel")]
        [ShowIf("ShouldHidePixelMatrix")]
        [OnInspectorGUI]
        private void DrawPixelMatrixHiddenNotice()
        {
            EditorGUILayout.HelpBox(
                "Pixel grid >256 hucre; Inspector'da gizlendi. Gormek icin Force Visual'i ac.",
                MessageType.Warning);
        }

        [FoldoutGroup("Image Pixel")]
        [ShowIf("ShouldShowPixelMatrix")]
        [OnInspectorGUI]
        private void DrawPixelMatrixInspector()
        {
            if (pixelGrid == null)
                return;

            int w = pixelGrid.GetLength(0);
            int h = pixelGrid.GetLength(1);
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

                    PixelCellData cell = pixelGrid[x, y];
                    DrawPixelCellInteractive(cellRect, ref cell);
                    pixelGrid[x, y] = cell;
                }
            }
        }

        [FoldoutGroup("Image Pixel")]
        [Button("Bake From Texture", ButtonSizes.Large)]
        [GUIColor(0.4f, 0.85f, 0.55f)]
        private void BakePixelFromTextureButton()
        {
            if (pixelBakeSource == null)
            {
                return;
            }

            if (!pixelBakeSource.isReadable)
            {
                return;
            }

            Color32[] raw = pixelBakeSource.GetPixels32();
            List<PixelData> baked;
            Vector2Int size;

            PixelBakeUtility bakeUtility = new PixelBakeUtility();
            Color32[] ignoredColors = CopyIgnoredBakeColors();
            int ignoreDistanceSqr = ignoreColorDistance * ignoreColorDistance;
            if (pixelBakeDownsample || ignoredColors != null)
            {
                size = pixelSize;
                if (size.x < 1 || size.y < 1)
                    size = new Vector2Int(32, 32);
                baked = bakeUtility.BakeDownsampled(
                    raw,
                    pixelBakeSource.width,
                    pixelBakeSource.height,
                    size,
                    ignoredColors,
                    ignoreDistanceSqr);
            }
            else
            {
                size = new Vector2Int(pixelBakeSource.width, pixelBakeSource.height);
                baked = bakeUtility.BakeFullResolution(
                    raw, pixelBakeSource.width, pixelBakeSource.height);
            }

            bakeUtility.CollapseToDominantColors(baked);

            ProjectSettings settings = ProjectSettings.Load();
            if (settings != null)
            {
                FacadePaletteMapper mapper = new FacadePaletteMapper();
                mapper.BuildFromFacades(settings.GetOriginalFacadeProperties());
                mapper.AssignPaletteIds(baked);
            }
            resetPaletteSwatchesOnRefresh = true;
            ApplyPixelBake(baked, size);
            EditorUtility.SetDirty(this);
        }

        [FoldoutGroup("Image Pixel")]
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
                return;
            }

            ApplyPixelBake(source.Pixels, source.TextureSize);
            EditorUtility.SetDirty(this);
        }

        [FoldoutGroup("Image Pixel")]
        [Button("Clear All Pixels")]
        [GUIColor(0.9f, 0.45f, 0.45f)]
        private void ClearAllPixelsButton()
        {
            ResetAllPixels();
            RefreshPaletteIdMapPreview();
            EditorUtility.SetDirty(this);
        }

        [FoldoutGroup("Image Pixel")]
        [Button("Refresh Palette ID Map")]
        private void RefreshPaletteIdMapButton()
        {
            RefreshPaletteIdMapPreview();
        }

        private bool suppressPaletteCallback;
        private bool resetPaletteSwatchesOnRefresh;

        private void OnPaletteRowsChanged()
        {
            if (suppressPaletteCallback)
                return;

            ApplyPaletteSwatchesToPixels();
            EditorUtility.SetDirty(this);
        }

        [OnInspectorGUI]
        private void SyncPalettePixelsInInspector()
        {
            if (suppressPaletteCallback || paletteRows == null || pixelGrid == null)
                return;

            if (!PalettePixelsDiffer())
                return;

            ApplyPaletteSwatchesToPixels();
            EditorUtility.SetDirty(this);
        }

        private bool PalettePixelsDiffer()
        {
            int w = pixelGrid.GetLength(0);
            int h = pixelGrid.GetLength(1);
            for (int i = 0; i < paletteRows.Count; i++)
            {
                LevelPaletteRow row = paletteRows[i];
                if (row == null || row.paletteId <= 0)
                    continue;

                Color32 swatch = (Color32)row.swatch;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        PixelCellData cell = pixelGrid[x, y];
                        if (cell == null || !cell.HasPixel || cell.PaletteId != row.paletteId)
                            continue;

                        Color32 pixel = cell.Color;
                        if (pixel.r != swatch.r || pixel.g != swatch.g || pixel.b != swatch.b)
                            return true;
                    }
                }
            }

            return false;
        }

        private Color ResolveDisplayedColor(int paletteId, FacadeProperty facade)
        {
            if (TryGetPaletteSwatch((short)paletteId, out Color swatch))
                return swatch;

            if (facade != null)
                return facade.Color;

            return Color.gray;
        }

        private void ApplyPaletteSwatchesToPixels()
        {
            if (paletteRows == null || pixelGrid == null)
                return;

            int w = pixelGrid.GetLength(0);
            int h = pixelGrid.GetLength(1);
            for (int i = 0; i < paletteRows.Count; i++)
            {
                LevelPaletteRow row = paletteRows[i];
                if (row == null || row.paletteId <= 0)
                    continue;

                Color32 color = (Color32)row.swatch;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        PixelCellData cell = pixelGrid[x, y];
                        if (cell == null || !cell.HasPixel || cell.PaletteId != row.paletteId)
                            continue;

                        cell.SetPixel(color, row.paletteId);
                    }
                }
            }
        }

        private void RefreshPaletteIdMapPreview()
        {
            if (paletteRows == null)
                paletteRows = new List<LevelPaletteRow>();

            Dictionary<short, int> counts = new Dictionary<short, int>(8);
            Dictionary<short, Color32> samples = new Dictionary<short, Color32>(8);
            if (pixelGrid != null)
            {
                int w = pixelGrid.GetLength(0);
                int h = pixelGrid.GetLength(1);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        PixelCellData cell = pixelGrid[x, y];
                        if (cell == null || !cell.HasPixel || cell.PaletteId <= 0)
                            continue;

                        short id = cell.PaletteId;
                        if (counts.TryGetValue(id, out int current))
                            counts[id] = current + 1;
                        else
                        {
                            counts[id] = 1;
                            samples[id] = cell.Color;
                        }
                    }
                }
            }

            Dictionary<int, string> names = new Dictionary<int, string>(8);
            Dictionary<int, Color> facadeColors = new Dictionary<int, Color>(8);
            ProjectSettings settings = ProjectSettings.Load();
            if (settings != null)
            {
                List<FacadeProperty> facades = settings.GetOriginalFacadeProperties();
                if (facades != null)
                {
                    for (int i = 0; i < facades.Count; i++)
                    {
                        FacadeProperty facade = facades[i];
                        if (facade == null || facade.Level <= 0)
                            continue;

                        names[facade.Level] = string.IsNullOrEmpty(facade.Name) ? facade.Level.ToString() : facade.Name;
                        facadeColors[facade.Level] = facade.Color;
                    }
                }
            }

            suppressPaletteCallback = true;
            for (int i = paletteRows.Count - 1; i >= 0; i--)
            {
                LevelPaletteRow row = paletteRows[i];
                if (row == null || !counts.ContainsKey(row.paletteId))
                    paletteRows.RemoveAt(i);
            }

            List<short> ids = new List<short>(counts.Keys);
            ids.Sort();
            for (int i = 0; i < ids.Count; i++)
            {
                short id = ids[i];
                LevelPaletteRow row = null;
                for (int r = 0; r < paletteRows.Count; r++)
                {
                    if (paletteRows[r] != null && paletteRows[r].paletteId == id)
                    {
                        row = paletteRows[r];
                        break;
                    }
                }

                bool isNew = row == null;
                if (isNew)
                {
                    row = new LevelPaletteRow();
                    row.paletteId = id;
                    paletteRows.Add(row);
                }

                if (isNew || resetPaletteSwatchesOnRefresh)
                {
                    if (samples.TryGetValue(id, out Color32 sample))
                        row.swatch = sample;
                    else if (facadeColors.TryGetValue(id, out Color facadeColor))
                        row.swatch = facadeColor;
                    else
                        row.swatch = Color.white;
                }

                row.colorName = names.TryGetValue(id, out string name) ? name : id.ToString();
                row.pixelCount = counts[id];
            }

            paletteRows.Sort(ComparePaletteRows);
            resetPaletteSwatchesOnRefresh = false;
            suppressPaletteCallback = false;
        }

        private int ComparePaletteRows(LevelPaletteRow a, LevelPaletteRow b)
        {
            short left = a != null ? a.paletteId : (short)0;
            short right = b != null ? b.paletteId : (short)0;
            return left.CompareTo(right);
        }
#endif

        public int CountOpaquePixels()
        {
            if (pixelGrid == null)
                return 0;

            int count = 0;
            int w = pixelGrid.GetLength(0);
            int h = pixelGrid.GetLength(1);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    PixelCellData cell = pixelGrid[x, y];
                    if (cell != null && cell.HasPixel)
                        count++;
                }
            }

            return count;
        }

        public void CopyOpaquePixelsTo(List<PixelData> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            if (pixelGrid == null)
                return;

            int w = pixelGrid.GetLength(0);
            int h = pixelGrid.GetLength(1);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    PixelCellData cell = pixelGrid[x, y];
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

        public void ApplyPixelBake(List<PixelData> opaquePixels, Vector2Int size)
        {
            pixelSize = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            PixelBakeUtility bakeUtility = new PixelBakeUtility();
            pixelGrid = bakeUtility.CreatePixelGrid(opaquePixels, pixelSize);

            pixelYMigrated = false;
#if UNITY_EDITOR
            RefreshPaletteIdMapPreview();
#endif
        }

        private void EnsurePixelGrid()
        {
            int w = Mathf.Max(1, pixelSize.x);
            int h = Mathf.Max(1, pixelSize.y);
            pixelSize = new Vector2Int(w, h);

            if (pixelGrid == null || pixelGrid.GetLength(0) != w || pixelGrid.GetLength(1) != h)
            {
                PixelCellData[,] old = pixelGrid;
                int oldW = old?.GetLength(0) ?? 0;
                int oldH = old?.GetLength(1) ?? 0;
                pixelGrid = new PixelCellData[w, h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (x < oldW && y < oldH && old[x, y] != null)
                            pixelGrid[x, y] = old[x, y];
                        else
                            pixelGrid[x, y] = new PixelCellData();
                    }
                }
            }
            else
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (pixelGrid[x, y] == null)
                            pixelGrid[x, y] = new PixelCellData();
                    }
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
                pixelSelection = element;

                if (Event.current.alt)
                {
                    pixelPaintColor = element.Color;
                }
                else
                {
                    Color32 paint = pixelPaintColor;
                    short paletteId = -1;
                    ProjectSettings settings = ProjectSettings.Load();
                    if (settings != null)
                    {
                        FacadePaletteMapper mapper = new FacadePaletteMapper();
                        mapper.BuildFromFacades(settings.GetOriginalFacadeProperties());
                        paletteId = mapper.ResolveLevel(paint);
                        if (mapper.TryGetColor(paletteId, out Color32 paletteColor))
                            paint = paletteColor;
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

            Color fill = new Color(0.12f, 0.12f, 0.12f, 1f);
            if (element.HasPixel)
            {
                fill = TryGetPaletteSwatch(element.PaletteId, out Color swatch)
                    ? swatch
                    : (Color)element.Color;
            }
            EditorGUI.DrawRect(rect.Padding(1), fill);

            if (ReferenceEquals(element, pixelSelection))
                DrawOutline(rect, Color.magenta, 3);
        }

        private void ResetAllPixels()
        {
            if (pixelGrid == null)
                return;

            foreach (PixelCellData cell in pixelGrid)
            {
                if (cell != null)
                    cell.Clear();
            }

            EditorUtility.SetDirty(this);
        }
#endif

        #endregion

        public ShooterShotPlan BuildShotPlan()
        {
            List<ShotSlot> slots = new List<ShotSlot>(64);

            if (cellData != null)
            {
                int questionCursor = 0;
                for (int gameY = 0; gameY < this.height; gameY++)
                {
                    int row = GameYToArrayRow(gameY);
                    if (row < 0 || row >= cellData.GetLength(1))
                        continue;

                    for (int x = 0; x < this.width; x++)
                    {
                        if (x >= cellData.GetLength(0))
                            continue;

                        CellData cell = cellData[x, row];
                        if (cell == null)
                            continue;

                        if (cell.IsQuestionShooter)
                        {
                            ShooterData questionData = null;
                            int colorId = 0;
                            if (questionShooterData != null && questionCursor < questionShooterData.Count)
                            {
                                questionData = questionShooterData[questionCursor];
                                questionCursor++;
                                if (questionData != null)
                                    colorId = (int)questionData.Color;
                            }

                            int stored = cell.ShotCount > 0
                                ? cell.ShotCount
                                : (questionData != null ? questionData.ShotCount : 0);

                            slots.Add(new ShotSlot
                            {
                                ColorId = colorId,
                                Stored = stored,
                                Cell = cell,
                                Data = questionData
                            });
                            continue;
                        }

                        if (!cell.IsFull)
                            continue;

                        slots.Add(new ShotSlot
                        {
                            ColorId = cell.ColorLevel,
                            Stored = cell.ShotCount,
                            Cell = cell
                        });
                    }
                }
            }

            if (tunnelShooterData != null)
            {
                for (int tunnelIndex = 0; tunnelIndex < tunnelShooterData.Count; tunnelIndex++)
                {
                    TunnelData tunnel = tunnelShooterData[tunnelIndex];
                    if (tunnel == null || tunnel.Shooters == null)
                        continue;

                    for (int shooterIndex = 0; shooterIndex < tunnel.Shooters.Count; shooterIndex++)
                    {
                        ShooterData data = tunnel.Shooters[shooterIndex];
                        if (data == null)
                            continue;

                        slots.Add(new ShotSlot
                        {
                            ColorId = (int)data.Color,
                            Stored = data.ShotCount,
                            Data = data
                        });
                    }
                }
            }

            Dictionary<int, int> pixelCounts = new Dictionary<int, int>(8);
            if (pixelGrid != null)
            {
                int pixelWidth = pixelGrid.GetLength(0);
                int pixelHeight = pixelGrid.GetLength(1);
                for (int y = 0; y < pixelHeight; y++)
                {
                    for (int x = 0; x < pixelWidth; x++)
                    {
                        PixelCellData pixel = pixelGrid[x, y];
                        if (pixel == null || !pixel.HasPixel || pixel.PaletteId <= 0)
                            continue;

                        int colorId = pixel.PaletteId;
                        if (pixelCounts.TryGetValue(colorId, out int current))
                            pixelCounts[colorId] = current + 1;
                        else
                            pixelCounts[colorId] = 1;
                    }
                }
            }

            Dictionary<int, List<ShotSlot>> groups = new Dictionary<int, List<ShotSlot>>(8);
            for (int i = 0; i < slots.Count; i++)
            {
                ShotSlot slot = slots[i];
                if (slot.ColorId <= 0)
                    continue;

                if (!groups.TryGetValue(slot.ColorId, out List<ShotSlot> group))
                {
                    group = new List<ShotSlot>(8);
                    groups[slot.ColorId] = group;
                }

                group.Add(slot);
            }

            List<int> colorIds = new List<int>(groups.Count + pixelCounts.Count);
            foreach (KeyValuePair<int, int> pair in pixelCounts)
            {
                if (pair.Key > 0 && !colorIds.Contains(pair.Key))
                    colorIds.Add(pair.Key);
            }

            foreach (KeyValuePair<int, List<ShotSlot>> pair in groups)
            {
                if (!colorIds.Contains(pair.Key))
                    colorIds.Add(pair.Key);
            }

            colorIds.Sort();

            Dictionary<CellData, int> cellShots = new Dictionary<CellData, int>(slots.Count);
            Dictionary<ShooterData, int> dataShots = new Dictionary<ShooterData, int>(slots.Count);
            List<ShooterShotPlan.Row> rows = new List<ShooterShotPlan.Row>(colorIds.Count);
            bool balanced = true;

            for (int colorIndex = 0; colorIndex < colorIds.Count; colorIndex++)
            {
                int colorId = colorIds[colorIndex];
                int pixelCount = pixelCounts.TryGetValue(colorId, out int pixels) ? pixels : 0;
                groups.TryGetValue(colorId, out List<ShotSlot> group);
                int shooterCount = group != null ? group.Count : 0;
                int explicitSum = 0;
                int zeroCount = 0;

                if (group != null)
                {
                    for (int i = 0; i < group.Count; i++)
                    {
                        if (group[i].Stored > 0)
                            explicitSum += group[i].Stored;
                        else
                            zeroCount++;
                    }
                }

                int shotSum = 0;
                if (group != null && shooterCount > 0)
                {
                    int[] resolvedShots = new int[shooterCount];
                    if (explicitSum > pixelCount && explicitSum > 0)
                    {
                        int given = 0;
                        for (int i = 0; i < shooterCount; i++)
                        {
                            int stored = group[i].Stored;
                            resolvedShots[i] = stored > 0 ? stored * pixelCount / explicitSum : 0;
                            given += resolvedShots[i];
                        }

                        int leftover = pixelCount - given;
                        for (int i = 0; i < shooterCount && leftover > 0; i++)
                        {
                            if (group[i].Stored <= 0)
                                continue;

                            resolvedShots[i]++;
                            leftover--;
                        }
                    }
                    else if (explicitSum == pixelCount || zeroCount == 0)
                    {
                        int leftover = pixelCount - explicitSum;
                        if (leftover < 0)
                            leftover = 0;

                        int extraCursor = 0;
                        for (int i = 0; i < shooterCount; i++)
                        {
                            int stored = group[i].Stored;
                            int resolved = stored > 0 ? stored : 0;
                            if (leftover > 0 && stored > 0)
                            {
                                int share = leftover / shooterCount;
                                int extra = leftover % shooterCount;
                                resolved += share + (extraCursor < extra ? 1 : 0);
                                extraCursor++;
                            }

                            resolvedShots[i] = resolved;
                        }
                    }
                    else
                    {
                        int remaining = pixelCount - explicitSum;
                        if (remaining < 0)
                            remaining = 0;

                        int baseCount = remaining / zeroCount;
                        int extra = remaining % zeroCount;
                        int zeroIndex = 0;
                        for (int i = 0; i < shooterCount; i++)
                        {
                            if (group[i].Stored > 0)
                            {
                                resolvedShots[i] = group[i].Stored;
                                continue;
                            }

                            resolvedShots[i] = baseCount + (zeroIndex < extra ? 1 : 0);
                            zeroIndex++;
                        }
                    }

                    for (int i = 0; i < shooterCount; i++)
                    {
                        ShotSlot slot = group[i];
                        int resolved = resolvedShots[i];
                        shotSum += resolved;
                        if (slot.Cell != null)
                            cellShots[slot.Cell] = resolved;
                        if (slot.Data != null)
                            dataShots[slot.Data] = resolved;
                    }
                }

                if (shotSum != pixelCount || (pixelCount > 0 && shooterCount == 0))
                    balanced = false;

                rows.Add(new ShooterShotPlan.Row
                {
                    ColorId = colorId,
                    Pixels = pixelCount,
                    Shots = shotSum,
                    Shooters = shooterCount
                });
            }

            return new ShooterShotPlan(cellShots, dataShots, rows, balanced);
        }

        public void ApplyShotPlan()
        {
            ShooterShotPlan plan = BuildShotPlan();
            if (cellData != null)
            {
                int width = cellData.GetLength(0);
                int height = cellData.GetLength(1);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        CellData cell = cellData[x, y];
                        if (cell == null)
                            continue;

                        if (!cell.IsFull && !cell.IsQuestionShooter)
                            continue;

                        cell.SetShotCount(plan.ForCell(cell));
                    }
                }
            }

            if (questionShooterData != null)
            {
                for (int i = 0; i < questionShooterData.Count; i++)
                {
                    ShooterData data = questionShooterData[i];
                    if (data != null)
                        data.SetShotCount(plan.ForData(data));
                }
            }

            if (tunnelShooterData != null)
            {
                for (int tunnelIndex = 0; tunnelIndex < tunnelShooterData.Count; tunnelIndex++)
                {
                    TunnelData tunnel = tunnelShooterData[tunnelIndex];
                    if (tunnel == null || tunnel.Shooters == null)
                        continue;

                    for (int shooterIndex = 0; shooterIndex < tunnel.Shooters.Count; shooterIndex++)
                    {
                        ShooterData data = tunnel.Shooters[shooterIndex];
                        if (data != null)
                            data.SetShotCount(plan.ForData(data));
                    }
                }
            }
        }

        private struct ShotSlot
        {
            public int ColorId;
            public int Stored;
            public CellData Cell;
            public ShooterData Data;
        }

        #region Grid Helper

        [SerializeField]
        [FoldoutGroup("Grid Helper")]
        [FormerlySerializedAs("m_QuestionThreadData")]
        internal List<ShooterData> questionShooterData;

        [SerializeField]
        [FoldoutGroup("Grid Helper")]
        [FormerlySerializedAs("m_TunnelThreadData")]
        internal List<TunnelData> tunnelShooterData;

        public List<ShooterData> QuestionShooterData => questionShooterData;

        public List<TunnelData> TunnelShooterData => tunnelShooterData;

        #endregion

        #region Slot

        [SerializeField] [FoldoutGroup("Slot")]
        [FormerlySerializedAs("m_SlotData")]
        private SlotData slotData;

        public SlotData SlotData => slotData;

        #endregion

        #region Plate

        [SerializeField]
        [FoldoutGroup("Plate")]
        [Tooltip("Level basina conveyor tabak (plate) sayisi. Kuyrukta dik dizilir.")]
        [MinValue(1)]
        [FormerlySerializedAs("m_PlateCount")]
        private int plateCount = 5;

        public int PlateCount => plateCount > 0 ? plateCount : 5;

        #endregion

        public bool TryGetPaletteSwatch(short paletteId, out Color color)
        {
            color = Color.white;
            if (paletteRows == null || paletteId <= 0)
                return false;

            for (int i = 0; i < paletteRows.Count; i++)
            {
                LevelPaletteRow row = paletteRows[i];
                if (row == null || row.paletteId != paletteId)
                    continue;

                color = row.swatch;
                return true;
            }

            return false;
        }
    }

    [Serializable]
    public class LevelPaletteRow
    {
        [TableColumnWidth(48, Resizable = false)]
        [ReadOnly]
        [LabelText("ID")]
        public short paletteId;

        [ReadOnly]
        [LabelText("Name")]
        public string colorName;

        [TableColumnWidth(90, Resizable = false)]
        [LabelText("RGB")]
        public Color swatch;

        [TableColumnWidth(72, Resizable = false)]
        [ReadOnly]
        [LabelText("Count")]
        public int pixelCount;
    }
}
