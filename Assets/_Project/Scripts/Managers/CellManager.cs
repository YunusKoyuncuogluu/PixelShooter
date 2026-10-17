using System.Collections.Generic;
using Grids;
using LevelManagement;
using UnityEngine;
using Zenject;
using UnityEngine.Serialization;

public class CellManager : MonoBehaviour
{
    [Inject] Cell.Factory cellFactory;

    [Header("Grid Parent")]
    [Tooltip("CellParent. X ortalanir. Local Z=0 grid ust kenaridir; farkli height'larda ust sabit kalir, grid asagi buyur.")]
    [FormerlySerializedAs("m_CenterTransform")]
    [SerializeField] private Transform centerTransform;

    [Header("Cell Spacing")]
    [SerializeField] private float cellSpaceX;
    [SerializeField] private float cellSpaceY;

    private int width;
    private int height;
    private LevelData levelData;

    public int Width => this.width;
    public int Height => this.height;
    public float CellSpaceX => cellSpaceX;
    public float CellSpaceY => cellSpaceY;

    private CellData[,] cellData;
    private Cell[,] cells;

    public Cell[,] Cells => cells;

    public Transform CenterTransform => centerTransform;

    public CellData[,] CellData => this.cellData;

    private CellPositioner gridPositioner;
    private List<Vector3> positionList;
    private List<Vector2Int> coordinateList;

    public void Construct(LevelData levelData)
    {
        this.levelData = levelData;
        levelData.MigrateGridYIfNeeded();
        this.width = levelData.Width;
        this.height = levelData.Height;
        this.cellData = levelData.CellData;
        CreateCells();
    }

    public void CreateCells()
    {
        gridPositioner = new CellPositioner(
            this.width,
            this.height,
            cellSpaceX,
            cellSpaceY);
        positionList = new List<Vector3>(gridPositioner.PositionList());
        coordinateList = new List<Vector2Int>(gridPositioner.CoordinateList());
        CreateCell(this.width, this.height);
    }

    public void CreateCell(int width, int height)
    {
        cells = new Cell[width, height];

        for (int i = 0; i < positionList.Count; i++)
        {
            int ay = levelData.GameYToArrayRow(coordinateList[i].y);
            CellData cellData = this.cellData[coordinateList[i].x, ay];
            Vector3 cellPosition = positionList[i];
            Vector2Int cellCoordinate = coordinateList[i];
            Cell cell = cellFactory.Create();
            cell.Construct(cellPosition, cellCoordinate, centerTransform, cellData.IsLocked);
            cells[cellCoordinate.x, cellCoordinate.y] = cell;
        }
    }

    public bool IsInside(Vector2Int coordinate)
    {
        return coordinate.x >= 0 && coordinate.y >= 0 && coordinate.x < this.width && coordinate.y < this.height;
    }

    public void SetFull(Vector2Int tunnelCoordinate, bool isFull)
    {
        if (!IsInside(tunnelCoordinate))
            return;

        cells[tunnelCoordinate.x, tunnelCoordinate.y].SetFull(isFull);
    }

    public bool IsFull(Vector2Int coordinate)
    {
        if (!IsInside(coordinate))
            return false;

        return cells[coordinate.x, coordinate.y].IsFull;
    }

    public void SetTunnelReserved(Vector2Int coordinate, bool reserved)
    {
        if (!IsInside(coordinate))
            return;

        cells[coordinate.x, coordinate.y].SetTunnelReserved(reserved);
    }

    public Cell GetCell(Vector2Int coordinate)
    {
        if (coordinate.x < 0 || coordinate.x >= this.width || coordinate.y < 0 || coordinate.y >= this.height)
            return null;

        return cells[coordinate.x, coordinate.y];
    }

    public Vector3 GetPosition(Vector2Int coordinate)
    {
        Cell cell = GetCell(coordinate);
        return cell != null ? cell.transform.position : Vector3.zero;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (centerTransform == null)
            return;

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 1f);
        Vector3 origin = centerTransform.position;
        Vector3 right = centerTransform.right * 4f;
        Gizmos.DrawLine(origin - right, origin + right);
        Gizmos.DrawSphere(origin, 0.08f);
    }
#endif
}
