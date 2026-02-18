using System;
using System.Collections;
using System.Collections.Generic;
using Grids;
using LevelManagement;
using UnityEngine;
using Zenject;

public class CellManager : MonoBehaviour
{
    [Inject] LevelManager m_LevelManager;
    [Inject] ProjectSettings m_ProjectSettings;
    [Inject] Cell.Factory m_CellFactory;
    [SerializeField] private Transform m_CenterTransform;

    private int m_Width;
    private int m_Height;

    private float m_SpaceX;
    private float m_SpaceY;

    public int Width => m_Width;
    public int Height => m_Height;

    private CellData[,] m_CellData;
    private Cell[,] m_Cells;

    public Cell[,] Cells => m_Cells;

    public Transform CenterTransform => m_CenterTransform;

    public CellData[,] CellData => m_CellData;

    private CellPositioner m_GridPositioner;
    private List<Vector3> m_PositionList;
    private List<Vector2Int> m_CoordinateList;


    public void Construct(LevelData levelData)
    {
        levelData.MigrateGridYIfNeeded();
        m_Width = levelData.Width;
        m_Height = levelData.Height;
        m_SpaceX = m_ProjectSettings.CellSpaceX;
        m_SpaceY = m_ProjectSettings.CellSpaceY;
        m_CellData = levelData.CellData;
        CreateCells();
    }

    public void CreateCells()
    {
        m_GridPositioner = new CellPositioner(m_Width, m_Height, m_CenterTransform, m_SpaceX, m_SpaceY);
        m_PositionList = new List<Vector3>(m_GridPositioner.PositionList());
        m_CoordinateList = new List<Vector2Int>(m_GridPositioner.CoordinateList());
        CreateCell(m_Width, m_Height);
    }

    public void CreateCell(int width, int height)
    {
        m_Cells = new Cell[width, height];

        for (int i = 0; i < m_PositionList.Count; i++)
        {
            int ay = LevelData.GameYToArrayRow(m_CoordinateList[i].y, m_Height);
            CellData cellData = m_CellData[m_CoordinateList[i].x, ay];
            Vector3 cellPosition = m_PositionList[i];
            Vector2Int cellCoordinate = m_CoordinateList[i];
            Cell cell = m_CellFactory.Create();
            cell.Construct(cellPosition, cellCoordinate, m_CenterTransform, cellData.IsLocked);
            m_Cells[cellCoordinate.x, cellCoordinate.y] = cell;
        }
    }

    public bool CellIsLocked(Vector2Int coordinate) => m_Cells[coordinate.x, coordinate.y].IsLocked;

    public void SetFull(Vector2Int tunnelCoordinate, bool a) =>
        m_Cells[tunnelCoordinate.x, tunnelCoordinate.y].SetFull(a);

    public bool IsFull(Vector2Int coordinate) => m_Cells[coordinate.x, coordinate.y].IsFull;

    public Cell GetCell(Vector2Int coordinate)
    {
        if (coordinate.x < 0 || coordinate.x >= m_Width || coordinate.y < 0 || coordinate.y >= m_Height)
            return null;

        return m_Cells[coordinate.x, coordinate.y];
    }


    public Vector3 GetPosition(Vector2Int coordinate)
    {
        Cell cell = GetCell(coordinate);
        return cell != null ? cell.transform.position : Vector3.zero;
    }
}