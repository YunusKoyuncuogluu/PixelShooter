using System.Collections.Generic;
using UnityEngine;

public class PathFinding
{
    private readonly int width;
    private readonly int height;
    private readonly Vector2Int[] directions;
    private readonly Cell[,] cells;

    public PathFinding(int width, int height, Cell[,] cells)
    {
        directions = new[]
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        this.width = width;
        this.height = height;
        this.cells = cells;
    }

    public List<Vector2Int> BreadthFirstSearch(Vector2Int start, out bool isExitFound)
    {
        List<Vector2Int> path = new List<Vector2Int>();

        if (start.y == this.height - 1)
        {
            path.Add(start);
            isExitFound = true;
            return path;
        }

        bool[,] visited = new bool[this.width, this.height];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        queue.Enqueue(start);
        visited[start.x, start.y] = true;

        bool exitReached = false;
        Vector2Int exitPoint = default;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int next = current + directions[i];
                if (next.x < 0 || next.x >= this.width || next.y < 0 || next.y >= this.height)
                    continue;
                if (this.cells[next.x, next.y].IsFull || this.cells[next.x, next.y].IsLocked || visited[next.x, next.y])
                    continue;

                queue.Enqueue(next);
                visited[next.x, next.y] = true;
                cameFrom[next] = current;

                if (next.y == this.height - 1)
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
