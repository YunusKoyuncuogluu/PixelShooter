using System.Collections.Generic;
using UnityEngine;

public class CellPositioner
{
    private readonly int m_Width;
    private readonly int m_Height;
    private readonly Transform m_CenterTransform;
    private readonly float m_SpaceX;
    private readonly float m_SpaceY;
    private readonly float m_TopAnchorZ;

    public CellPositioner(
        int width,
        int height,
        Transform centerTransform,
        float spaceX,
        float spaceY,
        float topAnchorZ)
    {
        m_Width = width;
        m_Height = height;
        m_CenterTransform = centerTransform;
        m_SpaceX = spaceX;
        m_SpaceY = spaceY;
        m_TopAnchorZ = topAnchorZ;
    }

    /// <summary>
    /// Veri: y=0 alt (duvar), y=Height-1 ust (acik/cikis).
    /// X: center etrafinda simetrik. Z: grid ust kenari topAnchorZ'ye sabit (conveyor alti).
    /// </summary>
    public List<Vector3> PositionList()
    {
        List<Vector3> cellPositionList = new List<Vector3>();
        float extentX = (m_Width + m_SpaceX * (m_Width - 1)) * 0.5f;
        float stepZ = 1f + m_SpaceY;

        for (int y = 0; y < m_Height; y++)
        {
            for (int x = 0; x < m_Width; x++)
            {
                float localX = (x + m_SpaceX * x) - extentX + 0.5f;
                float localZ = (m_TopAnchorZ - 0.5f) - (m_Height - 1 - y) * stepZ;
                cellPositionList.Add(m_CenterTransform.TransformPoint(new Vector3(localX, 0f, localZ)));
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
