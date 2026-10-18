using System.Collections.Generic;
using Grids;
using LevelManagement;
using UnityEngine;
using Zenject;

public class TunnelManager : MonoBehaviour
{
    [Inject] private Tunnel.Factory tunnelFactory;
    [Inject] private CellManager cellManager;
    [Inject] private ShooterManager shooterManager;

    private Tunnel[,] tunnels;

    private CellData[,] cellData;
    private CellPositioner cellPositioner;
    private int width;
    private int height;
    private LevelData levelData;
    private List<Vector3> positionList;
    private List<Vector2Int> coordinateList;
    private List<TunnelData> tunnelDataList;

    public void Construct(LevelData levelData)
    {
        this.levelData = levelData;
        levelData.MigrateGridYIfNeeded();

        tunnelDataList = levelData.TunnelShooterData != null
            ? new List<TunnelData>(levelData.TunnelShooterData)
            : new List<TunnelData>();
        width = levelData.Width;
        height = levelData.Height;
        cellData = levelData.CellData;

        CreateTunnels();
    }

    private void CreateTunnels()
    {
        cellPositioner = new CellPositioner(
            width,
            height,
            cellManager.CellSpaceX,
            cellManager.CellSpaceY);

        positionList = new List<Vector3>(cellPositioner.PositionList());
        coordinateList = new List<Vector2Int>(cellPositioner.CoordinateList());

        CreateTunnel(width, height);
    }

    private void CreateTunnel(int gridWidth, int gridHeight)
    {
        tunnels = new Tunnel[gridWidth, gridHeight];

        for (int i = 0; i < positionList.Count; i++)
        {
            int arrayRow = levelData.GameYToArrayRow(coordinateList[i].y);
            CellData cell = cellData[coordinateList[i].x, arrayRow];
            Vector3 tunnelPosition = cellManager.CenterTransform.TransformPoint(positionList[i]);
            Vector2Int tunnelCoordinate = coordinateList[i];

            if (!cell.IsTunnel)
                continue;

            cellManager.SetFull(tunnelCoordinate, true);
            Tunnel tunnel = tunnelFactory.Create();
            tunnel.transform.SetParent(transform);
            TunnelData tunnelData;

            if (tunnelDataList.Count > 0 && tunnelDataList[0] != null)
            {
                tunnelData = tunnelDataList[0];
                tunnelDataList.RemoveAt(0);
            }
            else
            {
                if (tunnelDataList.Count > 0)
                    tunnelDataList.RemoveAt(0);

                tunnelData = new TunnelData();
                tunnelData.InitializeDefault();
            }

            tunnelData.EnsureShooters();
            tunnel.Construct(cell.Direction, tunnelData, tunnelPosition, tunnelCoordinate);
            tunnels[tunnelCoordinate.x, tunnelCoordinate.y] = tunnel;
        }
    }

    public void ControlTunnel()
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Tunnel tunnel = tunnels[x, y];
                if (tunnel == null)
                    continue;

                Vector2Int tunnelExitCoordinate = tunnel.ExitCoordinate;
                if (!cellManager.IsInside(tunnelExitCoordinate))
                {
                    tunnel.CreateNewShooter();
                    continue;
                }

                if (cellManager.IsFull(tunnelExitCoordinate))
                    continue;
                if (shooterManager.ShooterAt(tunnelExitCoordinate) != null)
                    continue;

                tunnels[x, y].CreateNewShooter();
            }
        }
    }

    public bool HasPendingShooters()
    {
        if (tunnels == null)
            return false;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Tunnel tunnel = tunnels[x, y];
                if (tunnel != null && tunnel.PendingShooterCount > 0)
                    return true;
            }
        }

        return false;
    }

    public void RemoveTunnel(Vector2Int coordinate) => tunnels[coordinate.x, coordinate.y] = null;
}
