using System.Collections.Generic;
using UnityEngine;

public class PathFinding
{
    private readonly int m_Width;
    private readonly int m_Height;
    private readonly Vector2Int[] m_Directions;
    private readonly Cell[,] m_Cells;

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

        bool exitReached = false;
        Vector2Int exitPoint = default;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            for (int i = 0; i < m_Directions.Length; i++)
            {
                Vector2Int next = current + m_Directions[i];
                if (next.x < 0 || next.x >= m_Width || next.y < 0 || next.y >= m_Height)
                    continue;
                if (m_Cells[next.x, next.y].IsFull || m_Cells[next.x, next.y].IsLocked || visited[next.x, next.y])
                    continue;

                queue.Enqueue(next);
                visited[next.x, next.y] = true;
                cameFrom[next] = current;

                if (next.y == m_Height - 1)
                {
                    exitReached = true;
                    exitPoint = next;
                    break;
                }
            }

            if (exitReached)
                break;
        }

        if (!exitReached)
        {
            isExitFound = false;
            return path;
        }

        Vector2Int walk = exitPoint;
        while (!walk.Equals(start))
        {
            path.Add(walk);
            walk = cameFrom[walk];
        }

        path.Add(start);
        path.Reverse();
        isExitFound = true;
        return path;
    }
}
