using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CellPositioner
{
    private readonly int m_Width;
    private readonly int m_Height;
    private readonly Transform m_CenterTransform;
    private readonly float m_SpaceX;
    private readonly float m_SpaceY;
    
    public CellPositioner(int width, int height, Transform centerTransform, float spaceX, float spaceY)
    {
        m_Width = width;
        m_Height = height;
        m_CenterTransform = centerTransform;
        m_SpaceX = spaceX;
        m_SpaceY = spaceY;
    }
    
    /// <summary> Veri: y=0 alt (duvar), y=Height-1 üst (açık/çıkış). Pozisyonlar: y=0 ekranda altta, y=Height-1 üstte. </summary>
    public List<Vector3> PositionList()
    {
        float virtualHeight = m_Height <= 5 ? 3.5f : 4.2f;
        List<Vector3> cellPositionList = new List<Vector3>();
        float addSizeY = virtualHeight - m_Height;
        for (int y = 0; y < m_Height; y++)
        {
            for (int x = 0; x < m_Width; x++)
            {
                Vector3 localCellPosition =
                    new Vector3(
                        (x + m_SpaceX * x) - (m_Width + m_SpaceX * (m_Width - 1)) / 2f + 0.5f,
                        0f, addSizeY +
                        (y + m_SpaceY * y) - (m_SpaceY * (m_Height - 1)) / 2f + 0.5f);
                Vector3 worldCellPosition = m_CenterTransform.TransformPoint(localCellPosition);
                cellPositionList.Add(worldCellPosition);
            }
        }

        return cellPositionList;
    }
    
    public List<Vector2Int> CoordinateList()
    {
        List<Vector2Int> cellCoordinateList = new List<Vector2Int>();
        for (int y = 0; y < m_Height; y++)
        {
            for (int x = 0; x < m_Width; x++)
                cellCoordinateList.Add(new Vector2Int(x, y));
        }

        return cellCoordinateList;
    }
}
