using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Grids;
using LevelManagement;
using UnityEngine;
using Zenject;

public class ThreadManager : MonoBehaviour
{
    [Inject] private CellManager m_CellManager;
    [Inject] private Thread.Factory m_ThreadFactory;

    private Thread[,] m_Threads;
    public Thread[,] Threads => m_Threads;

    private int m_Width;
    private int m_Height;

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
        RefreshClickableThreadsNextFrame().Forget();
    }

    private void InitializeGridPositioner()
    {
        m_Width = m_LevelData.Width;
        m_Height = m_LevelData.Height;

        m_CellPositioner = new CellPositioner(
            m_Width,
            m_Height,
            m_CellManager.CenterTransform,
            m_CellManager.CellSpaceX,
            m_CellManager.CellSpaceY,
            m_CellManager.GridTopAnchorZ);
        m_PositionList = new List<Vector3>(m_CellPositioner.PositionList());
        m_CoordinateList = new List<Vector2Int>(m_CellPositioner.CoordinateList());
        m_Threads = new Thread[m_Width, m_Height];
    }

    private void CreateThreads()
    {
        for (int i = 0; i < m_PositionList.Count; i++)
        {
            int ay = LevelData.GameYToArrayRow(m_CoordinateList[i].y, m_Height);
            CellData cellData = m_CellData[m_CoordinateList[i].x, ay];
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

    private async UniTaskVoid RefreshClickableThreadsNextFrame()
    {
        await UniTask.DelayFrame(1, cancellationToken: this.GetCancellationTokenOnDestroy());
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

        for (int y = 0; y < m_Height; y++)
        {
            for (int x = 0; x < m_Width; x++)
            {
                if (m_Threads[x, y] == null || m_Threads[x, y].IsTunnelThread)
                    continue;

                Thread thread = m_Threads[x, y];
                Vector2Int coordinate = new Vector2Int(x, y);

                m_PathFinding.BreadthFirstSearch(coordinate, out bool isExitFound);

                thread.SetClickable(isExitFound);
                thread.ClickableProcess();
            }
        }
    }

    public void SetNewThread(Thread thread, Vector2Int exitCoordinate) =>
        m_Threads[exitCoordinate.x, exitCoordinate.y] = thread;

    public void RemoveThread(Vector2Int coordinate) => m_Threads[coordinate.x, coordinate.y] = null;

    public Thread ThreadAt(Vector2Int coordinate)
    {
        if (coordinate.x < 0 || coordinate.x >= m_Width || coordinate.y < 0 || coordinate.y >= m_Height)
            return null;
        return m_Threads[coordinate.x, coordinate.y];
    }
}