using System.Collections.Generic;
using DG.Tweening;
using Grids;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public class Tunnel : MonoBehaviour
{
    [Inject] private ShooterManager shooterManager;
    [Inject] private CellManager cellManager;
    [Inject] private TunnelManager tunnelManager;
    [Inject] private SlotManager slotManager;

    [FormerlySerializedAs("m_TunnelVisual")]
    [SerializeField] private TunnelVisual tunnelVisual;
    [FormerlySerializedAs("dispenseDuration")]
    [SerializeField] private float emergeDuration = 0.42f;
    [FormerlySerializedAs("chamberDepth")]
    [SerializeField] private float emergeDepth = 0.48f;
    [SerializeField] private float mouthDistance = 0.72f;

    private Vector2Int coordinate;
    public Vector2Int Coordinate => coordinate;

    private Vector2Int exitCoordinate;
    public Vector2Int ExitCoordinate => exitCoordinate;

    private Direction direction;
    private Vector3 tunnelPosition;
    private List<ShooterData> pendingShooters;
    private Tween emergeTween;
    private Shooter emergingShooter;
    private Vector3 emergeFrom;
    private Vector3 emergeTo;
    private Vector3 emergeDirection;
    private Vector3 emergeDestination;
    private Vector3 emergeScale;
    private float emergeLength;
    private bool scaleOnX;
    private bool seatAfterEmerge;
    private bool emerging;
    public int PendingShooterCount
    {
        get
        {
            int waiting = pendingShooters != null ? pendingShooters.Count : 0;
            if (emerging)
            {
                waiting++;
            }

            return waiting;
        }
    }

    public void Construct(Direction tunnelDirection, TunnelData tunnelData, Vector3 worldPosition, Vector2Int tunnelCoordinate)
    {
        tunnelPosition = worldPosition;
        direction = tunnelDirection;
        coordinate = tunnelCoordinate;
        exitCoordinate = GetExitCoordinate(tunnelDirection, tunnelCoordinate);
        transform.position = worldPosition;

        List<ShooterData> source = tunnelData != null ? tunnelData.Shooters : null;
        pendingShooters = source != null ? new List<ShooterData>(source) : new List<ShooterData>();
        tunnelVisual.Construct(direction, pendingShooters.Count);

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
        if (emerging || pendingShooters == null || pendingShooters.Count == 0)
        {
            RefreshExitReservation();
            return;
        }

        if (IsOpenBoardExit())
        {
            if (!slotManager.HasOpenSlot())
            {
                RefreshExitReservation();
                return;
            }

            BeginOffBoardEmerge();
        }
        else if (cellManager.IsInside(exitCoordinate))
            BeginGridEmerge();
        else
            return;

        RefreshCount();
        RefreshExitReservation();
    }

    private void BeginGridEmerge()
    {
        ShooterData data = pendingShooters[0];
        pendingShooters.RemoveAt(0);

        int shotCount = shooterManager.ResolveShotCount(data);
        Shooter shooter = shooterManager.CreateTunnelShooter(data.Color, exitCoordinate, shotCount);
        shooter.SetTunnelShooter(true);
        shooter.SetCoordinate(exitCoordinate);
        cellManager.SetFull(exitCoordinate, true);
        shooterManager.SetNewShooter(shooter, exitCoordinate);

        Vector3 endScale = shooter.ExitScale(shooterManager.HasOpenExit(exitCoordinate));
        BeginEmerge(shooter, cellManager.GetPosition(exitCoordinate), endScale, false);
    }

    private void BeginOffBoardEmerge()
    {
        ShooterData data = pendingShooters[0];
        pendingShooters.RemoveAt(0);

        int shotCount = shooterManager.ResolveShotCount(data);
        Shooter shooter = shooterManager.CreateBoardExitShooter(data.Color, tunnelPosition, shotCount);

        Vector3 origin = new Vector3(tunnelPosition.x, shooter.transform.position.y, tunnelPosition.z);
        Vector3 target = origin + ExitDirection() * CellStep();
        BeginEmerge(shooter, target, Vector3.one, true);
    }

    private void BeginEmerge(Shooter shooter, Vector3 targetPosition, Vector3 endScale, bool seatOnSlot)
    {
        shooter.PrepareTunnelExit();
        emerging = true;
        emergingShooter = shooter;
        seatAfterEmerge = seatOnSlot;
        emergeDestination = targetPosition;
        emergeScale = endScale;

        Vector3 mouth = new Vector3(tunnelPosition.x, targetPosition.y, tunnelPosition.z);
        Vector3 travel = targetPosition - mouth;
        travel.y = 0f;
        emergeDirection = travel.sqrMagnitude > 0.0001f ? travel.normalized : ExitDirection();
        scaleOnX = Mathf.Abs(emergeDirection.x) > Mathf.Abs(emergeDirection.z);
        emergeLength = scaleOnX ? endScale.x : endScale.z;

        float depth = emergeDepth > 0.05f ? emergeDepth : 0.05f;
        float reach = mouthDistance > 0.05f ? mouthDistance : 0.05f;
        Vector3 mouthPoint = mouth + emergeDirection * reach;
        emergeFrom = mouthPoint - emergeDirection * depth;
        emergeTo = targetPosition - emergeDirection * (0.5f * emergeLength);

        ApplyEmerge(0f);

        float duration = emergeDuration > 0.05f ? emergeDuration : 0.05f;
        emergeTween = DOVirtual.Float(0f, 1f, duration, ApplyEmerge)
            .SetEase(Ease.Linear)
            .SetTarget(this)
            .OnComplete(CompleteEmerge);
    }

    private void ApplyEmerge(float time)
    {
        if (!emerging || emergingShooter == null)
            return;

        float move = EaseOutCubic(time);
        float grow = InverseLerp(0.12f, 1f, move);
        float pop = EaseOutBack(grow);
        float along = Mathf.LerpUnclamped(0.14f, 1f, pop);
        float side = Mathf.LerpUnclamped(0.24f, 1f, pop);
        float height = Mathf.LerpUnclamped(0.2f, 1f, pop);
        float half = 0.5f * along * emergeLength;
        Vector3 back = Vector3.LerpUnclamped(emergeFrom, emergeTo, move);
        Transform body = emergingShooter.transform;

        body.position = back + emergeDirection * half;
        body.localScale = scaleOnX
            ? new Vector3(emergeScale.x * along, emergeScale.y * height, emergeScale.z * side)
            : new Vector3(emergeScale.x * side, emergeScale.y * height, emergeScale.z * along);

        tunnelVisual.ApplyExit(time);
    }

    private void CompleteEmerge()
    {
        if (!emerging)
            return;

        emerging = false;
        emergeTween = null;

        Shooter shooter = emergingShooter;
        bool seatOnSlot = seatAfterEmerge;
        emergingShooter = null;

        if (tunnelVisual != null)
            tunnelVisual.RestoreModel();

        if (shooter != null)
        {
            shooter.transform.position = emergeDestination;
            shooter.transform.localScale = emergeScale;
            shooter.SetHoldPresentationScale(false);

            if (seatOnSlot)
                slotManager.TrySeatShooter(shooter);
            else
            {
                shooter.SetTunnelShooter(false);
                shooterManager.ControlClickableShooters();
            }
        }

        if (pendingShooters == null || pendingShooters.Count == 0)
        {
            tunnelVisual.SetCount(0);
            tunnelManager.RemoveTunnel(coordinate);
            RefreshExitReservation();
            return;
        }

        RefreshCount();
        RefreshExitReservation();
    }

    private void OnDestroy()
    {
        emerging = false;
        emergingShooter = null;
        DOTween.Kill(this, false);
        emergeTween = null;
    }

    private void RefreshExitReservation()
    {
        if (!cellManager.IsInside(exitCoordinate))
            return;

        bool waiting = pendingShooters != null && pendingShooters.Count > 0;
        cellManager.SetTunnelReserved(exitCoordinate, waiting);
    }

    private void RefreshCount()
    {
        int waiting = pendingShooters != null ? pendingShooters.Count : 0;
        tunnelVisual.SetCount(waiting);
    }

    private bool IsOpenBoardExit()
    {
        return direction == Direction.Up && exitCoordinate.y >= cellManager.Height;
    }

    private float CellStep()
    {
        Vector3 facing = ExitDirection();
        if (Mathf.Abs(facing.x) > Mathf.Abs(facing.z))
            return 1f + cellManager.CellSpaceX;

        return 1f + cellManager.CellSpaceY;
    }

    private Vector3 ExitDirection()
    {
        if (direction == Direction.Left)
            return Vector3.left;

        if (direction == Direction.Right)
            return Vector3.right;

        if (direction == Direction.Down)
            return Vector3.back;

        return Vector3.forward;
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

    private float EaseOutCubic(float time)
    {
        float inverse = 1f - time;
        return 1f - inverse * inverse * inverse;
    }

    private float EaseOutBack(float time)
    {
        float shifted = time - 1f;
        return 1f + 2.70158f * shifted * shifted * shifted + 1.70158f * shifted * shifted;
    }

    private float InverseLerp(float from, float to, float value)
    {
        float span = to - from;
        if (span > -0.0001f && span < 0.0001f)
            return 1f;

        float time = (value - from) / span;
        if (time < 0f)
            return 0f;

        if (time > 1f)
            return 1f;

        return time;
    }

    public class Factory : PlaceholderFactory<Tunnel>
    {
    }
}
