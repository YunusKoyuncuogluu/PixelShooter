using System.Collections.Generic;
using Grids;
using PixelShooter.Pooling;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public class Tunnel : MonoBehaviour
{
    [Inject] private ShooterManager shooterManager;
    [Inject] private CellManager cellManager;
    [Inject] private TunnelManager tunnelManager;
    [Inject] private SlotManager slotManager;
    [Inject] private ObjectPool objectPool;

    [FormerlySerializedAs("m_TunnelVisual")]
    [SerializeField] private TunnelVisual tunnelVisual;

    private Vector2Int coordinate;
    public Vector2Int Coordinate => coordinate;

    private Vector2Int exitCoordinate;
    public Vector2Int ExitCoordinate => exitCoordinate;

    private Direction direction;
    private Vector3 position;
    private Transform cachedTransform;
    private List<ShooterData> shooterData;
    public int PendingShooterCount => shooterData != null ? shooterData.Count : 0;

    public void Construct(Direction tunnelDirection, TunnelData tunnelData, Vector3 tunnelPosition, Vector2Int tunnelCoordinate)
    {
        cachedTransform = transform;
        position = tunnelPosition;
        direction = tunnelDirection;
        coordinate = tunnelCoordinate;
        exitCoordinate = GetExitCoordinate(tunnelDirection, tunnelCoordinate);
        cachedTransform.position = position;
        List<ShooterData> sourceShooters = tunnelData != null ? tunnelData.Shooters : null;
        shooterData = sourceShooters != null ? new List<ShooterData>(sourceShooters) : new List<ShooterData>();
        tunnelVisual.Construct(direction, shooterData.Count);

        if (IsOpenBoardExit())
        {
            CreateNewShooter();
            return;
        }

        if (cellManager.IsInside(exitCoordinate) && !cellManager.IsFull(exitCoordinate))
            CreateNewShooter();
        else
            RefreshExitReservation();
    }

    public void CreateNewShooter()
    {
        if (shooterData.Count == 0)
        {
            RefreshExitReservation();
            return;
        }

        if (shooterData.Count >= 1)
        {
            if (IsOpenBoardExit())
                ReleaseThroughBoardExit();
            else if (cellManager.IsInside(exitCoordinate))
                SpawnAndAnimateTunnelShooter();

            UpdateTunnelVisual();
        }

        RefreshExitReservation();

        if (shooterData.Count == 0)
        {
            tunnelVisual.SetCount(0);
            tunnelManager.RemoveTunnel(coordinate);
        }
    }

    private void RefreshExitReservation()
    {
        if (!cellManager.IsInside(exitCoordinate))
            return;

        cellManager.SetTunnelReserved(exitCoordinate, shooterData != null && shooterData.Count > 0);
    }

    private bool IsOpenBoardExit()
    {
        return direction == Direction.Up && exitCoordinate.y >= cellManager.Height;
    }

    private void ReleaseThroughBoardExit()
    {
        int shotCount = shooterManager.ResolveShotCount(shooterData[0]);
        Shooter shooter = shooterManager.CreateBoardExitShooter(shooterData[0].Color, position, shotCount);
        shooterData.RemoveAt(0);
        shooter.SetTunnelShooter(false);
        slotManager.SaveShooter(shooter);
    }

    private void SpawnAndAnimateTunnelShooter()
    {
        int shotCount = shooterManager.ResolveShotCount(shooterData[0]);
        Shooter shooter = shooterManager.CreateTunnelShooter(shooterData[0].Color, exitCoordinate, shotCount);
        shooter.transform.localScale = Vector3.zero;
        shooter.transform.position = new Vector3(transform.position.x, shooter.transform.position.y, transform.position.z);
        shooter.SetCoordinate(exitCoordinate);
        shooterData.RemoveAt(0);
        shooter.SetTunnelShooter(true);

        MoveShooterToExit(shooter);
    }

    private void MoveShooterToExit(Shooter shooter)
    {
        Vector3 targetPosition = cellManager.GetPosition(exitCoordinate);
        cellManager.SetFull(exitCoordinate, true);
        shooterManager.SetNewShooter(shooter, exitCoordinate);
        shooter.SetTunnelShooter(false);
        shooter.SetCoordinate(exitCoordinate);

        shooter.transform.gameObject.FollowTo(
            objectPool,
            targetPosition,
            shooter.SlotTravelSpeed,
            onComplete: () => { shooterManager.ControlClickableShooters(); },
            onUpdate: progress =>
            {
                shooter.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, progress);
            });
    }

    private void UpdateTunnelVisual()
    {
        if (shooterData.Count > 0)
            tunnelVisual.SetCount(shooterData.Count);
    }

    private Vector2Int GetExitCoordinate(Direction tunnelDirection, Vector2Int tunnelCoordinate)
    {
        return tunnelDirection switch
        {
            Direction.Left => new Vector2Int(tunnelCoordinate.x - 1, tunnelCoordinate.y),
            Direction.Right => new Vector2Int(tunnelCoordinate.x + 1, tunnelCoordinate.y),
            Direction.Up => new Vector2Int(tunnelCoordinate.x, tunnelCoordinate.y + 1),
            Direction.Down => new Vector2Int(tunnelCoordinate.x, tunnelCoordinate.y - 1),
            _ => tunnelCoordinate,
        };
    }

    public class Factory : PlaceholderFactory<Tunnel>
    {
    }
}
