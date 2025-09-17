using System;
using Grids;
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

        [FoldoutGroup("Grid")]
        [SerializeField]
#if UNITY_EDITOR
        [TableMatrix
            (
                HideColumnIndices = false,
                HideRowIndices = true,
                SquareCells = true,
                DrawElementMethod = nameof(DrawCell)
            )
        ]
#endif

        private CellData[,] m_CellData = new CellData[5, 5];

        [FoldoutGroup("Grid")] [SerializeField]
        private CellData m_CellDataDistance = null;

        private GUIStyle m_LevelLabelStyle;
        private int m_TunnelCount;


        private static CellData DrawCell(Rect rect, ref CellData element)
        {
            if (Event.current.type == EventType.KeyDown)
            {
                s_Instance.m_CellDataDistance.ColorLevel = Event.current.keyCode switch
                {
                    KeyCode.Alpha1 or KeyCode.Keypad1 => 1,
                    KeyCode.Alpha2 or KeyCode.Keypad2 => 2,
                    KeyCode.Alpha3 or KeyCode.Keypad3 => 3,
                    KeyCode.Alpha4 or KeyCode.Keypad4 => 4,
                    KeyCode.Alpha5 or KeyCode.Keypad5 => 5,
                    KeyCode.Alpha6 or KeyCode.Keypad6 => 6,
                    KeyCode.Alpha7 or KeyCode.Keypad7 => 7,
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
                            directionText = "←←←" + " T" + element.TunnelIndex;
                            break;
                        case Direction.Right:
                            directionText = "→→→" + " T" + element.TunnelIndex;
                            break;
                        case Direction.Up:
                            directionText = "↑↑↑" + " T" + element.TunnelIndex;
                            break;
                        case Direction.Down:
                            directionText = "↓↓↓" + " T" + element.TunnelIndex;
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
            CellData[,] reversedCellData = ReverseItemData(m_CellData, m_Width, m_Height);
            int questionIndex = 1;

            for (int i = 0; i < m_Height; i++)
            {
                for (int j = 0; j < m_Width; j++)
                {
                    var cell = reversedCellData[j, i];
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
            CellData[,] reversedCellData = ReverseItemData(m_CellData, m_Width, m_Height);
            int tunnelIndex = 1;

            for (int i = 0; i < m_Height; i++)
            {
                for (int j = 0; j < m_Width; j++)
                {
                    var cell = reversedCellData[j, i];
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

        private CellData[,] ReverseItemData(CellData[,] cellData, int width, int height)
        {
            CellData[,] reversedCellData = new CellData[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    reversedCellData[x, y] = cellData[x, height - 1 - y];
            }

            return reversedCellData;
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

        [OnInspectorInit]
        private void OnInspectorInit()
        {
            s_Instance = this;

            for (int i = 0; i < m_Width; i++)
            for (int j = 0; j < m_Height; j++)
            {
                if (m_CellData[i, j] == null)
                    m_CellData[i, j] = new CellData();
            }
        }

        [OnInspectorDispose]
        private void OnInspectorDispose()
        {
            s_Instance = null;
            Debug.Log("DISPOSSS<<===");
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
    }

    #endregion
}