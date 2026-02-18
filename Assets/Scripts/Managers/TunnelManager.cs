using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Grids;
using LevelManagement;
using UnityEngine;
using Zenject;

public class TunnelManager : MonoBehaviour
{
    [Inject] private Tunnel.Factory m_TunnelFactory;
    [Inject] private ProjectSettings m_ProjectSettings;
    [Inject] private CellManager m_CellManager;

    [SerializeField] private Transform m_CenterTransform;
    public Transform CenterTransform => m_CenterTransform;

    private Tunnel[,] m_Tunnels;
    public Tunnel[,] Tunnels => m_Tunnels;

    private LevelData m_LevelData;
    private CellData[,] m_CellData;
    private CellPositioner m_CellPositioner;
    private int m_Width;
    private int m_Height;
    private float m_SpaceX;
    private float m_SpaceY;
    private List<Vector3> m_PositionList;
    private List<Vector2Int> m_CoordinateList;
    private List<TunnelData> m_TunneDatalList;


    public void Construct(LevelData levelData)
    {
        m_LevelData = levelData;
        m_TunneDatalList = new List<TunnelData>(levelData.TunnelThreadData);
        m_Width = levelData.Width;
        m_Height = levelData.Height;
        m_SpaceX = m_ProjectSettings.CellSpaceX;
        m_SpaceY = m_ProjectSettings.CellSpaceY;
        m_CellData = levelData.CellData;
        CreateTunnels();
    }


    private void CreateTunnels()
    {
        m_CellPositioner = new CellPositioner(m_Width, m_Height, m_CenterTransform, m_SpaceX, m_SpaceY);
        m_PositionList = new List<Vector3>(m_CellPositioner.PositionList());
        m_CoordinateList = new List<Vector2Int>(m_CellPositioner.CoordinateList());
        CreateTunnel(m_Width, m_Height);
    }

    private void CreateTunnel(int width, int height)
    {
        m_Tunnels = new Tunnel[width, height];

        for (int i = 0; i < m_PositionList.Count; i++)
        {
            CellData cellData = m_CellData[m_CoordinateList[i].x, m_CoordinateList[i].y];
            Vector3 tunnelPosition = m_PositionList[i];
            Vector2Int tunnelCoordinate = m_CoordinateList[i];

            if (!cellData.IsTunnel)
                continue;

            m_CellManager.SetFull(tunnelCoordinate, true);
            Tunnel tunnel = m_TunnelFactory.Create();
            tunnel.transform.SetParent(transform);
            TunnelData tunnelData = m_TunneDatalList[0];
            m_TunneDatalList.RemoveAt(0);
            tunnel.Construct(cellData.Direction, tunnelData, tunnelPosition, tunnelCoordinate);
            m_Tunnels[tunnelCoordinate.x, tunnelCoordinate.y] = tunnel;
        }
    }

    public void ControlTunnel()
    {
        for (int y = 0; y < m_Height; y++)
        {
            for (int x = 0; x < m_Width; x++)
            {
                Tunnel tunnel = m_Tunnels[x, y];
                if (tunnel == null)
                    continue;

                Vector2Int tunnelExitCoordinate = tunnel.ExitCoordinate;
                if (m_CellManager.IsFull(tunnelExitCoordinate))
                    continue;

                m_Tunnels[x, y].CreateNewThread();
            }
        }
    }

    public void RemoveTunnel(Vector2Int coordinate) => m_Tunnels[coordinate.x, coordinate.y] = null;
}