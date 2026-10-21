using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Grids;
using LevelManagement;
using UnityEngine;
using Zenject;

public class ShooterManager : MonoBehaviour
{
    [Inject] private CellManager cellManager;
    [Inject] private Shooter.Factory shooterFactory;

    private Shooter[,] shooters;

    private int width;
    private int height;

    public int GridWidth => width;
    public int GridHeight => height;

    private CellPositioner cellPositioner;
    private List<Vector3> positionList;
    private List<Vector2Int> coordinateList;

    private CellData[,] cellData;
    private LevelData levelData;
    private List<ShooterData> questionShooterData;
    private PathFinding pathFinding;
    private ShooterShotPlan shotPlan;

    public void Construct(LevelData data)
    {
        levelData = data;
        cellData = cellManager.CellData;
        questionShooterData = levelData.QuestionShooterData != null
            ? new List<ShooterData>(levelData.QuestionShooterData)
            : new List<ShooterData>();
        shotPlan = levelData.BuildShotPlan();

        InitializeGridPositioner();
        CreateShooters();
        ControlClickableShooters();
        RefreshClickableShootersNextFrame().Forget();
    }

    private void InitializeGridPositioner()
    {
        width = levelData.Width;
        height = levelData.Height;

        cellPositioner = new CellPositioner(
            width,
            height,
            cellManager.CellSpaceX,
            cellManager.CellSpaceY);

        positionList = new List<Vector3>(cellPositioner.PositionList());
        coordinateList = new List<Vector2Int>(cellPositioner.CoordinateList());
        shooters = new Shooter[width, height];
    }

    private void CreateShooters()
    {
        for (int i = 0; i < positionList.Count; i++)
        {
            int arrayRow = levelData.GameYToArrayRow(coordinateList[i].y);
            CellData cell = cellData[coordinateList[i].x, arrayRow];

            if (!cell.IsFull && !cell.IsQuestionShooter)
                continue;

            GeneralColor color = (GeneralColor)cell.ColorLevel;
            if (cell.IsQuestionShooter)
            {
                if (questionShooterData.Count > 0)
                {
                    color = questionShooterData[0].Color;
                    questionShooterData.RemoveAt(0);
                }

                if (color == GeneralColor.None)
                    color = GeneralColor.Red;
            }

            int shots = shotPlan != null ? shotPlan.ForCell(cell) : cell.ShotCount;
            Vector2Int coordinate = coordinateList[i];
            ShooterData shooterData =
                new ShooterData(color, cell.IsQuestionShooter, coordinate, cellManager.GetPosition(coordinate), shots);

            CreateShooter(shooterData);
        }
    }

    private void CreateShooter(ShooterData shooterData)
    {
        Shooter shooter = shooterFactory.Create();
        PlaceOnCell(shooter, shooterData);
        Vector2Int shooterCoordinate = shooterData.Coordinate;
        shooters[shooterData.Coordinate.x, shooterData.Coordinate.y] = shooter;
        cellManager.SetFull(shooterCoordinate, true);
    }

    private void PlaceOnCell(Shooter shooter, ShooterData shooterData)
    {
        shooter.transform.SetParent(transform, false);
        shooter.transform.localScale = Vector3.one;
        shooter.transform.localRotation = Quaternion.identity;
        shooter.Construct(shooterData);
        TintShooter(shooter);
    }

    private async UniTaskVoid RefreshClickableShootersNextFrame()
    {
        await UniTask.DelayFrame(1, cancellationToken: this.GetCancellationTokenOnDestroy());
        ControlClickableShooters();
    }

    public int ResolveShotCount(ShooterData data)
    {
        if (data == null)
            return 0;

        return shotPlan != null ? shotPlan.ForData(data) : data.ShotCount;
    }

    public Shooter CreateTunnelShooter(GeneralColor color, Vector2Int coordinate, int shotCount)
    {
        Vector3 shooterPosition = cellManager.GetPosition(coordinate);
        ShooterData shooterData = new ShooterData(color, false, coordinate, shooterPosition, shotCount);
        return CreateShooterForTunnel(shooterData);
    }

    public Shooter CreateBoardExitShooter(GeneralColor color, Vector3 worldPosition, int shotCount)
    {
        ShooterData shooterData = new ShooterData(color, false, new Vector2Int(-1, -1), worldPosition, shotCount);
        Shooter shooter = shooterFactory.Create();
        shooter.transform.SetParent(transform, false);
        shooter.transform.localScale = Vector3.one;
        shooter.Construct(shooterData);
        TintShooter(shooter);

        return shooter;
    }

    private void TintShooter(Shooter shooter)
    {
        if (shooter == null || levelData == null)
            return;

        if (levelData.TryGetPaletteSwatch((short)shooter.Color, out Color swatch))
            shooter.ApplyPaletteSwatch(swatch);
    }

    private Shooter CreateShooterForTunnel(ShooterData shooterData)
    {
        Shooter shooter = shooterFactory.Create();
        PlaceOnCell(shooter, shooterData);
        shooters[shooter.Coordinate.x, shooter.Coordinate.y] = shooter;

        return shooter;
    }

    public bool HasOpenExit(Vector2Int coordinate)
    {
        if (coordinate.x < 0 || coordinate.y < 0 || coordinate.x >= width || coordinate.y >= height)
            return false;

        pathFinding = new PathFinding(width, height, cellManager.Cells);
        pathFinding.BreadthFirstSearch(coordinate, out bool exitFound);
        return exitFound;
    }

    public void ControlClickableShooters()
    {
        pathFinding = new PathFinding(width, height, cellManager.Cells);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (shooters[x, y] == null || shooters[x, y].IsTunnelShooter)
                    continue;

                Shooter shooter = shooters[x, y];
                Vector2Int coordinate = new Vector2Int(x, y);
                pathFinding.BreadthFirstSearch(coordinate, out bool isExitFound);
                shooter.SetClickable(isExitFound);
                shooter.RefreshLockState();
            }
        }
    }

    public void SetNewShooter(Shooter shooter, Vector2Int exitCoordinate)
    {
        if (exitCoordinate.x < 0 || exitCoordinate.y < 0 || exitCoordinate.x >= width || exitCoordinate.y >= height)
            return;

        shooters[exitCoordinate.x, exitCoordinate.y] = shooter;
    }

    public void RemoveShooter(Vector2Int coordinate) => shooters[coordinate.x, coordinate.y] = null;

    public int CountGridShooters()
    {
        if (shooters == null)
            return 0;

        int count = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (shooters[x, y] != null)
                    count++;
            }
        }

        return count;
    }

    public bool AllGridShootersCanLeave()
    {
        if (shooters == null)
            return false;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Shooter shooter = shooters[x, y];
                if (shooter == null)
                {
                    continue;
                }

                if (shooter.IsTunnelShooter || shooter.IsBusy || !shooter.IsClickable)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public void SendFittingShooters(int room)
    {
        if (room <= 0 || shooters == null)
            return;

        int sent = 0;
        bool sentOne = true;
        while (sentOne && sent < room)
        {
            sentOne = false;
            for (int y = 0; y < height && !sentOne; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Shooter shooter = shooters[x, y];
                    if (shooter == null || shooter.IsTunnelShooter || !shooter.IsClickable || shooter.IsBusy)
                        continue;

                    Vector2Int coordinate = shooter.Coordinate;
                    if (!shooter.TryEnterSlot())
                        continue;

                    cellManager.SetFull(coordinate, false);
                    sent++;
                    sentOne = true;

                    break;
                }
            }

            if (sentOne)
                ControlClickableShooters();
        }
    }

    public Shooter ShooterAt(Vector2Int coordinate)
    {
        if (coordinate.x < 0 || coordinate.x >= width || coordinate.y < 0 || coordinate.y >= height)
            return null;

        return shooters[coordinate.x, coordinate.y];
    }
}
