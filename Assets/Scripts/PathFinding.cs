using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathFinding
{
    private readonly int m_Width;
    private readonly int m_Height;
    private readonly Vector2Int[] m_Directions;
    private Cell[,] m_Cells;

    public PathFinding(int width, int height, Cell[,] cells)
    {
        m_Directions = new[]
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        m_Width = width;
        m_Height = height;
        m_Cells = cells;
    }

    public List<Vector2Int> BreadthFirstSearch(Vector2Int start, out bool isExitFound)
    {
        List<Vector2Int> path = new List<Vector2Int>();

        if (start.y == m_Height - 1)
        {
            path.Add(start);
            isExitFound = true;
            return path;
        }

        bool[,] visited = new bool[m_Width, m_Height];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        queue.Enqueue(start);
        visited[start.x, start.y] = true;


        Vector2Int[] directions =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        bool isExit = false;

        Vector2Int exitPoint = new Vector2Int();

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (Vector2Int dir in directions)
            {
                Vector2Int next = current + dir;
                if (next.x >= 0 && next.x < m_Width && next.y >= 0 && next.y < m_Height &&
                    !m_Cells[next.x, next.y].IsFull && !m_Cells[next.x, next.y].IsLoccked && !visited[next.x, next.y])
                {
                    queue.Enqueue(next);
                    visited[next.x, next.y] = true;
                    cameFrom[next] = current;

                    if (next.y == m_Height - 1)
                    {
                        isExit = true;
                        exitPoint = next;
                        break;
                    }
                }
            }


            if (isExit)
            {
                break;
            }
        }

        if (isExit)
        {
            Vector2Int current = exitPoint;
            while (!current.Equals(start))
            {
                path.Add(current);
                current = cameFrom[current];
            }

            path.Add(start);
            path.Reverse();
            isExitFound = true;
            return path;
        }

        isExitFound = false;
        return new List<Vector2Int>();
    }

    public List<Vector3> PathConvert(List<Vector2Int> path)
    {
        List<Vector3> pathConverted = new List<Vector3>();

        foreach (Vector2Int p in path)
        {
            Vector3 worldPoint = GetWorldPositionFromGridCoordinate(p);
            pathConverted.Add(worldPoint);
        }

        return pathConverted;
    }

    private Vector3 GetWorldPositionFromGridCoordinate(Vector2Int gridCoordinate)
    {
        Cell cell = m_Cells[gridCoordinate.x, gridCoordinate.y];
        return cell.transform.position;
    }
}