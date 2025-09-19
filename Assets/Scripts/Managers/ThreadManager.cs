using System.Collections;
using System.Collections.Generic;
using Grids;
using JetBrains.Annotations;
using LevelManagement;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

public class ThreadManager : MonoBehaviour
{
    [Inject] private LevelManager m_LevelManager;
    [Inject] private CellManager m_CellManager;
    [Inject] private ProjectSettings m_ProjectSettings;
    [Inject] private Thread.Factory m_ThreadFactory;

    private Thread[,] m_Threads;
    public Thread[,] Threads => m_Threads;

    private int m_Width;
    private int m_Height;
    private int m_SpaceX;
    private int m_SpaceY;

    public int Width => m_Width;
    public int Height => m_Height;

    private CellPositioner m_CellPositioner;
    private List<Vector3> m_PositionList;
    private List<Vector2Int> m_CoordinateList;

    private CellData m_CellData;
    private LevelData m_LevelData;
    private List<ThreadData> m_QuestionThreadData;
    private PathFinding m_PathFinding;

    public void RemoveThread(Vector2Int coordinate) => m_Threads[coordinate.x, coordinate.y] = null;
}