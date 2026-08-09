using System.Collections.Generic;
using Dreamteck.Splines;
using Grids;
using UnityEngine;
using Zenject;

public class Tunnel : MonoBehaviour
{
    [Inject] private ThreadManager m_ThreadManager;
    [Inject] private CellManager m_CellManager;
    [Inject] private TunnelManager m_TunnelManager;

    [SerializeField] private TunnelVisual m_TunnelVisual;

    private Vector2Int m_Coordinate;
    public Vector2Int Coordinate => m_Coordinate;

    private Vector2Int m_ExitCoordinate;

    public Vector2Int ExitCoordinate => m_ExitCoordinate;

    private Direction m_Direction;
    private Vector3 m_Position;
    private Transform m_Transform;
    private List<ThreadData> m_ThreadData;


    public void Construct(Direction direction, TunnelData tunnelData, Vector3 position, Vector2Int coordinate)
    {
        m_Transform = transform;
        m_Position = position;
        m_Direction = direction;
        m_Coordinate = coordinate;
        m_ExitCoordinate = GetExitCoordinate(direction, coordinate);
        m_Transform.position = m_Position;
        m_ThreadData = new List<ThreadData>(tunnelData.Threads);

        m_TunnelVisual.Construct(m_Direction, m_ThreadData.Count);

        if (!m_CellManager.IsFull(m_ExitCoordinate))
            CreateNewThread();
    }

    public void CreateNewThread()
    {
        if (m_ThreadData.Count == 0)
            return;

        if (m_ThreadData.Count >= 1)
        {
            SpawnAndAnimateTunnelThread();
            UpdateTunnelVisual();
        }

        if (m_ThreadData.Count == 0)
        {
            m_TunnelVisual.SetCount(0);
            m_TunnelManager.RemoveTunnel(m_Coordinate);
        }
    }

    private void SpawnAndAnimateTunnelThread()
    {
        Thread thread = m_ThreadManager.CreateTunnelThread(m_ThreadData[0].Color, m_ExitCoordinate);
        thread.transform.localScale = Vector3.zero;
        thread.transform.position =
            new Vector3(transform.position.x, thread.transform.position.y, transform.position.z);
        thread.SetCoordinate(m_ExitCoordinate);
        m_ThreadData.RemoveAt(0);
        thread.SetTunnelThread(true);

        MoveThreadToExit(thread);
    }

    private void MoveThreadToExit(Thread thread)
    {
        Vector3 targetPosition = m_CellManager.GetPosition(m_ExitCoordinate);
        m_CellManager.SetFull(m_ExitCoordinate, true);
        m_ThreadManager.SetNewThread(thread, m_ExitCoordinate);
        thread.SetTunnelThread(false);
        thread.SetCoordinate(m_ExitCoordinate);

        thread.transform.gameObject.FollowTo(targetPosition, thread.SlotTravelSpeed,
            onComplete: () =>
            {
                m_ThreadManager.ControlClickableThreads();
            },
            onUpdate: (progress) =>
            {
                thread.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, progress);
            });
    }

    private void UpdateTunnelVisual()
    {
        if (m_ThreadData.Count > 0)
            m_TunnelVisual.SetCount(m_ThreadData.Count);
    }

    private Vector2Int GetExitCoordinate(Direction direction, Vector2Int coordinate)
    {
        return direction switch
        {
            Direction.Left => new Vector2Int(coordinate.x - 1, coordinate.y),
            Direction.Right => new Vector2Int(coordinate.x + 1, coordinate.y),
            Direction.Up => new Vector2Int(coordinate.x, coordinate.y + 1),
            Direction.Down => new Vector2Int(coordinate.x, coordinate.y - 1),
            _ => coordinate,
        };
    }

    public class Factory : PlaceholderFactory<Tunnel>
    {
    }
}
