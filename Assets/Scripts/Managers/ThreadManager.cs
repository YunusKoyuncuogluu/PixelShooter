using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
    private float m_SpaceX;
    private float m_SpaceY;

    public int Width => m_Width;
    public int Height => m_Height;

    private CellPositioner m_CellPositioner;
    private List<Vector3> m_PositionList;
    private List<Vector2Int> m_CoordinateList;

    private CellData[,] m_CellData;
    private LevelData m_LevelData;
    private List<ThreadData> m_QuestionThreadData;
    private PathFinding m_PathFinding;

    public void Construct(LevelData levelData)
    {
        m_LevelData = levelData;
        m_CellData = m_CellManager.CellData;
        m_QuestionThreadData = new List<ThreadData>(m_LevelData.QuestionThreadData);

        InitializeGridPositioner();
        CreateThreads();
        ConstructClickableSausagesWithDelay();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ControlClickableThreads();
        }
    }

    private void InitializeGridPositioner()
    {
        m_Width = m_LevelData.Width;
        m_Height = m_LevelData.Height;
        m_SpaceX = m_ProjectSettings.CellSpaceX;
        m_SpaceY = m_ProjectSettings.CellSpaceY;

        m_CellPositioner = new CellPositioner(m_Width, m_Height, m_CellManager.CenterTransform, m_SpaceX, m_SpaceY);
        m_PositionList = new List<Vector3>(m_CellPositioner.PositionList());
        m_CoordinateList = new List<Vector2Int>(m_CellPositioner.CoordinateList());
        m_Threads = new Thread[m_Width, m_Height];
    }

    private void CreateThreads()
    {
        for (int i = 0; i < m_PositionList.Count; i++)
        {
            CellData cellData = m_CellData[m_CoordinateList[i].x, m_CoordinateList[i].y];
            if (!cellData.IsFull && !cellData.IsQuestionThread)
                continue;

            GeneralColor color = cellData.IsQuestionThread
                ? m_QuestionThreadData[0].Color
                : (GeneralColor)cellData.ColorLevel;

            ThreadData threadData =
                new ThreadData(color, cellData.IsQuestionThread, m_CoordinateList[i], m_PositionList[i]);

            CreateThread(threadData);

            if (cellData.IsQuestionThread)
                m_QuestionThreadData.RemoveAt(0);
        }
    }

    private void CreateThread(ThreadData threadData)
    {
        Thread thread = m_ThreadFactory.Create();
        thread.transform.SetParent(transform);
        thread.Construct(threadData);
        Vector2Int threadCoordinate = threadData.Coordinate;
        m_Threads[threadData.Coordinate.x, threadData.Coordinate.y] = thread;
        m_CellManager.SetFull(threadCoordinate, true);
    }

    private async void ConstructClickableSausagesWithDelay()
    {
        await UniTask.DelayFrame(1);
        ControlClickableThreads();
    }

    public Thread CreateTunnelThread(GeneralColor color, Vector2Int coordinate)
    {
        Vector3 threadPosition = m_PositionList[m_CoordinateList.IndexOf(coordinate)];
        
        ThreadData threadData = new ThreadData(color, false, coordinate, threadPosition);
        return CreateThreadForTunnel(threadData);
    }

    private Thread CreateThreadForTunnel(ThreadData threadData)
    {
        Thread thread = m_ThreadFactory.Create();
        thread.transform.SetParent(transform);
        thread.Construct(threadData);
        m_Threads[thread.Coordinate.x, thread.Coordinate.y] = thread;
        return thread;
    }

    public void ControlClickableThreads()
    {
        m_PathFinding = new PathFinding(m_Width, m_Height, m_CellManager.Cells);

        for (int i = 0; i < m_Height; i++)
        {
            for (int j = 0; j < m_Width; j++)
            {
                if (m_Threads[i, j] == null || m_Threads[i, j].IsTunnelThread)
                    continue;

                Thread thread = m_Threads[i, j];
                Vector2Int coordinate = new Vector2Int(i, j);

                List<Vector2Int> pathCoordinates = m_PathFinding.BreadthFirstSearch(coordinate, out bool isExitFound);

                List<Vector3> path = m_PathFinding.PathConvert(pathCoordinates);

                //Temp Path

                thread.SetClickable(isExitFound);
                thread.ClickableProcess(path);
            }
        }
    }

    public void SetNewThread(Thread thread, Vector2Int exitCoordinate) =>
        m_Threads[exitCoordinate.x, exitCoordinate.y] = thread;

    public void RemoveThread(Vector2Int coordinate) => m_Threads[coordinate.x, coordinate.y] = null;
}