using System.Collections.Generic;
using UnityEngine;

public class CellPositioner
{
    private readonly int width;
    private readonly int height;
    private readonly float spaceX;
    private readonly float spaceY;

    public CellPositioner(
        int width,
        int height,
        float spaceX,
        float spaceY)
    {
        this.width = width;
        this.height = height;
        this.spaceX = spaceX;
        this.spaceY = spaceY;
    }

    public List<Vector3> PositionList()
    {
        List<Vector3> cellPositionList = new List<Vector3>();
        float extentX = (this.width + this.spaceX * (this.width - 1)) * 0.5f;
        float stepZ = 1f + this.spaceY;

        for (int y = 0; y < this.height; y++)
        {
            for (int x = 0; x < this.width; x++)
            {
                float localX = (x + this.spaceX * x) - extentX + 0.5f;
                float localZ = -0.5f - (this.height - 1 - y) * stepZ;
                cellPositionList.Add(new Vector3(localX, 0f, localZ));
            }
        }

        return cellPositionList;
    }

    public List<Vector2Int> CoordinateList()
    {
        List<Vector2Int> cellCoordinateList = new List<Vector2Int>();
        for (int y = 0; y < this.height; y++)
        {
            for (int x = 0; x < this.width; x++)
                cellCoordinateList.Add(new Vector2Int(x, y));
        }

        return cellCoordinateList;
    }
}
