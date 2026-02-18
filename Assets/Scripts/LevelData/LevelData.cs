using System;
using System.Collections.Generic;
using Grids;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;
using Direction = Grids.Direction;

namespace LevelManagement
{
    [CreateAssetMenu(fileName = nameof(LevelData) + " 001", menuName = "RopeWrap" + "/" + nameof(LevelData))]
    public class LevelData : ScriptableLevelBase
    {
        private static LevelData s_Instance;

        #region Grid

        [FoldoutGroup("Grid")] [OnValueChanged(nameof(OnWidthChanged))] [SerializeField]
        private int m_Width = 5;

        [FoldoutGroup("Grid")] [OnValueChanged(nameof(OnHeightChanged))] [SerializeField]
        private int m_Height = 5;

        public int Width => m_Width;
        public int Height => m_Height;

        public CellData[,] CellData => m_CellData;

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

        private GUIStyle m_LevelLabelStyle;
        private int m_TunnelCount;


        private static CellData DrawCell(Rect rect, ref CellData element)
        {
            if (Event.current.type == EventType.KeyDown && rect.Contains(Event.current.mousePosition) && s_Instance.m_CellDataDistance != null)
            {
                s_Instance.m_CellDataDistance.ColorLevel = Event.current.keyCode switch
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
                    _ => s_Instance.m_CellDataDistance.ColorLevel
                };

                s_Instance.m_CellDataDistance.OnColorLevelChanged();
            }

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                Debug.Log("<color=magenta>SELECT GRID</color>");
                GUI.changed = true;
                Event.current.Use();
                s_Instance.m_CellDataDistance = element;
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
                Debug.Log(element.IsTunnel
                    ? "<color=green>ADD</color> <color=yellow>TUNNEL</color>"
                    : "<color=red>REMOVE</color> <color=yellow>TUNNEL</color>");
            }

            if (Event.current.type == EventType.KeyDown &&
                rect.Contains(Event.current.mousePosition) && s_Instance.m_CellDataDistance.IsTunnel)
            {
                int selectDirectionLevel =
                    Event.current.keyCode switch
                    {
                        KeyCode.LeftArrow => 1,
                        KeyCode.RightArrow => 2,
                        KeyCode.UpArrow => 3,
                        KeyCode.DownArrow => 4,
                        _ => s_Instance.m_CellDataDistance.DirectionLevel
                    };

                if (s_Instance.m_CellDataDistance.DirectionLevel != selectDirectionLevel)
                {
                    s_Instance.m_CellDataDistance.DirectionLevel = selectDirectionLevel;
                    s_Instance.m_CellDataDistance.OnDirectionLevelChanged();
                    Debug.Log("<color=green>EDIT</color> <color=yellow>TUNNEL DIRECTION</color>");
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
                Debug.Log(element.IsQuestionThread
                    ? "<color=green>ADD</color> <color=yellow>QUESTION</color>"
                    : "<color=red>REMOVE</color> <color=yellow>QUESTION</color>");
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
                Debug.Log(element.IsLocked
                    ? "<color=green>ADD</color> <color=yellow>LOCKED</color>"
                    : "<color=red>REMOVE</color> <color=yellow>LOCKED</color>");
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space &&
                rect.Contains(Event.current.mousePosition))
            {
                ResetAllCell();
                Debug.Log("<color=red>RESET ALL GRID</color>");
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.R &&
                rect.Contains(Event.current.mousePosition))
            {
                element.ChangeLocked(false);
                ResetCell(element);
                Debug.Log("<color=magenta>SELECT GRID</color> <color=red>RESET</color>");
            }

            if (element.ColorLevel == 0)
            {
#if UNITY_EDITOR
                if (element.IsLocked)
                    EditorGUI.DrawRect(rect.Padding(1), Color.black);

                if (ReferenceEquals(element, s_Instance.m_CellDataDistance))
                    DrawOutline(rect, Color.magenta, 4);

                if (element.IsTunnel)
                {
                    s_Instance.NumberTunnels();

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
                    s_Instance.NumberQuestionThread();
                    string questionText = "Q" + element.QuestionIndex;
                    GUI.Label(rect.Padding(1), questionText, s_Instance.m_LevelLabelStyle);
                }

#endif
                return element;
            }

            element.ChangeLocked(false);
            element.ChangeQuestionThread(false);
            element.ChangeTunnelThread(false);
            element.QuestionIndex = 0;
            element.Direction = Direction.None;
            element.DirectionLevel = 0;

#if UNITY_EDITOR
            EditorGUI.DrawRect(rect.Padding(1), element.FacadeProperty.Color);
            if (ReferenceEquals(element, s_Instance.m_CellDataDistance))
                DrawOutline(rect, Color.magenta, 4);
#endif
            SetTextProperties();

            return element;
        }

        private static void SetTextProperties()
        {
            if (s_Instance.m_LevelLabelStyle == null)
            {
                s_Instance.m_LevelLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }

            s_Instance.m_LevelLabelStyle.fontSize = 50;
        }

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
            foreach (var cell in s_Instance.m_CellData)
                ResetCell(cell);
        }

        private void EnsureStitchDataDimensions()
        {
            if (m_StitchData == null || m_StitchData.GetLength(0) != m_StitchWidth || m_StitchData.GetLength(1) != m_StitchHeight)
            {
                var newStitch = new StitchCellData[m_StitchWidth, m_StitchHeight];
                int oldW = m_StitchData?.GetLength(0) ?? 0;
                int oldH = m_StitchData?.GetLength(1) ?? 0;
                for (int h = 0; h < m_StitchHeight; h++)
                for (int w = 0; w < m_StitchWidth; w++)
                    newStitch[w, h] = new StitchCellData();
                for (int i = 0; i < Math.Min(m_StitchWidth, oldW); i++)
                for (int j = 0; j < Math.Min(m_StitchHeight, oldH); j++)
                    if (m_StitchData[i, j] != null)
                        newStitch[i, j] = m_StitchData[i, j];
                m_StitchData = newStitch;
            }
        }

        private void OnStitchWidthChanged()
        {
            if (m_StitchData == null || m_StitchData.GetLength(0) != m_StitchWidth)
            {
                var newStitch = new StitchCellData[m_StitchWidth, m_StitchHeight];
                int oldW = m_StitchData?.GetLength(0) ?? 0;
                int oldH = m_StitchData?.GetLength(1) ?? 0;
                for (int h = 0; h < m_StitchHeight; h++)
                for (int w = 0; w < m_StitchWidth; w++)
                    newStitch[w, h] = new StitchCellData();
                for (int i = 0; i < Math.Min(m_StitchWidth, oldW); i++)
                for (int j = 0; j < Math.Min(m_StitchHeight, oldH); j++)
                    if (m_StitchData[i, j] != null)
                        newStitch[i, j] = m_StitchData[i, j];
                m_StitchData = newStitch;
            }
        }

        private void OnStitchHeightChanged()
        {
            if (m_StitchData == null || m_StitchData.GetLength(1) != m_StitchHeight)
            {
                var newStitch = new StitchCellData[m_StitchWidth, m_StitchHeight];
                int oldW = m_StitchData?.GetLength(0) ?? 0;
                int oldH = m_StitchData?.GetLength(1) ?? 0;
                for (int h = 0; h < m_StitchHeight; h++)
                for (int w = 0; w < m_StitchWidth; w++)
                    newStitch[w, h] = new StitchCellData();
                for (int i = 0; i < Math.Min(m_StitchWidth, oldW); i++)
                for (int j = 0; j < Math.Min(m_StitchHeight, oldH); j++)
                    if (m_StitchData[i, j] != null)
                        newStitch[i, j] = m_StitchData[i, j];
                m_StitchData = newStitch;
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

        [OnInspectorInit]
        private void OnInspectorInit()
        {
            s_Instance = this;
            MigrateGridYIfNeeded();

            for (int i = 0; i < m_Width; i++)
            for (int j = 0; j < m_Height; j++)
            {
                if (m_CellData[i, j] == null)
                    m_CellData[i, j] = new CellData();
            }

            EnsureStitchDataDimensions();
            for (int i = 0; i < m_StitchWidth; i++)
            for (int j = 0; j < m_StitchHeight; j++)
            {
                if (m_StitchData[i, j] == null)
                    m_StitchData[i, j] = new StitchCellData();
            }
        }

        [OnInspectorDispose]
        private void OnInspectorDispose()
        {
            s_Instance = null;
        }

#if UNITY_EDITOR
        private static void DrawOutline(Rect rect, Color color, int thickness = 2)
        {
            // Left Edge
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            // Right Edge
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
            // Top Edge
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            // Down Edge
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        }
#endif

        #region Stitch

        public StitchCellData[,] StitchData => m_StitchData;
        public int StitchWidth => m_StitchWidth;
        public int StitchHeight => m_StitchHeight;

        [FoldoutGroup("Stitch")] [MinValue(1)] [OnValueChanged(nameof(OnStitchWidthChanged))] [SerializeField]
        private int m_StitchWidth = 10;

        [FoldoutGroup("Stitch")] [MinValue(1)] [OnValueChanged(nameof(OnStitchHeightChanged))] [SerializeField]
        private int m_StitchHeight = 3;

        [FoldoutGroup("Stitch")]
        [SerializeField]
#if UNITY_EDITOR
        [TableMatrix(
            HideColumnIndices = false,
            HideRowIndices = true,
            SquareCells = true,
            DrawElementMethod = nameof(DrawStitchCell))]
#endif
        private StitchCellData[,] m_StitchData = new StitchCellData[10, 3];

        [FoldoutGroup("Stitch")] [SerializeField]
        [HideInInspector]
        private StitchCellData m_StitchDataDistance;

        private static StitchCellData DrawStitchCell(Rect rect, ref StitchCellData element)
        {
            if (s_Instance == null || element == null) return element;

            if (Event.current.type == EventType.KeyDown && rect.Contains(Event.current.mousePosition) && s_Instance.m_StitchDataDistance != null)
            {
                s_Instance.m_StitchDataDistance.ColorLevel = Event.current.keyCode switch
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
                    _ => s_Instance.m_StitchDataDistance.ColorLevel
                };
                s_Instance.m_StitchDataDistance.OnColorLevelChanged();
            }

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                GUI.changed = true;
                Event.current.Use();
                s_Instance.m_StitchDataDistance = element;
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space && rect.Contains(Event.current.mousePosition))
            {
                ResetAllStitch();
                Event.current.Use();
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.R && rect.Contains(Event.current.mousePosition))
            {
                ResetStitchCell(element);
                Event.current.Use();
            }

            if (element.ColorLevel <= 0)
            {
#if UNITY_EDITOR
                EditorGUI.DrawRect(rect.Padding(1), Color.black);
                if (ReferenceEquals(element, s_Instance.m_StitchDataDistance))
                    DrawOutline(rect, Color.magenta, 4);
#endif
                return element;
            }

#if UNITY_EDITOR
            if (element.FacadeProperty != null)
                EditorGUI.DrawRect(rect.Padding(1), element.FacadeProperty.Color);
            else
                EditorGUI.DrawRect(rect.Padding(1), Color.gray);
            if (ReferenceEquals(element, s_Instance.m_StitchDataDistance))
                DrawOutline(rect, Color.magenta, 4);
#endif
            return element;
        }

        private static void ResetStitchCell(StitchCellData element)
        {
            if (element == null) return;
            element.ColorLevel = 0;
            element.OnColorLevelChanged();
        }

        private static void ResetAllStitch()
        {
            if (s_Instance?.m_StitchData == null) return;
            foreach (var cell in s_Instance.m_StitchData)
                ResetStitchCell(cell);
        }

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

        #region KnitData

        [SerializeField] [FoldoutGroup("Knit")]
        private List<GeneralColor> m_KnitColor;

        public List<GeneralColor> KnitColor
        {
            get => m_KnitColor;
            set => m_KnitColor = value;
        }
        
        #endregion
    }

    #endregion
}