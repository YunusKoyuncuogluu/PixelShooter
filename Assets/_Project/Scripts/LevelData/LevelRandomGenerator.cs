#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Grids;
using UnityEditor;
using UnityEngine;
using Direction = Grids.Direction;

namespace LevelManagement
{
    internal class LevelRandomGenerator
    {
        private readonly LevelData level;

        internal LevelRandomGenerator(LevelData level)
        {
            this.level = level;
        }

        internal void Generate()
        {
            GenerateRandomLevel();
        }

        private void GenerateRandomLevel()
        {
            Dictionary<int, int> pixelCounts = CollectPixelColorCounts();
            if (pixelCounts.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Generate Random Level",
                    "Image pixel data is empty. Bake a texture first.",
                    "OK");
                return;
            }

            List<int> colorIds = new List<int>(pixelCounts.Count);
            foreach (KeyValuePair<int, int> pair in pixelCounts)
                colorIds.Add(pair.Key);

            colorIds.Sort();
            Undo.RecordObject(level, "Generate Random Level");

            int minimumCells = Mathf.Max(colorIds.Count + 12, 20);
            EnsureBoardCapacity(minimumCells);
            ClearBoard();

            System.Random random = new System.Random(Guid.NewGuid().GetHashCode());
            bool solved = false;
            for (int attempt = 0; attempt < 24 && !solved; attempt++)
            {
                if (attempt > 0)
                    ClearBoard();

                int cellCount = level.width * level.height;
                bool[] walls = new bool[cellCount];
                PlaceWallShapes(walls, random);

                List<int> openCells = new List<int>(cellCount);
                for (int i = 0; i < cellCount; i++)
                {
                    if (!walls[i])
                        openCells.Add(i);
                }

                Shuffle(openCells, random);

                int emptyBudget = Mathf.Max(4, cellCount * 35 / 100);
                int occupied = openCells.Count - emptyBudget;
                if (occupied > openCells.Count * 45 / 100)
                    occupied = Mathf.Max(4, openCells.Count * 45 / 100);
                if (occupied < 4)
                    occupied = Mathf.Min(4, openCells.Count);
                if (occupied > openCells.Count)
                    occupied = openCells.Count;

                int tunnelBudget = occupied >= 6 ? random.Next(1, 3) : (occupied > 1 ? 1 : 0);
                int questionBudget = occupied >= 5 ? random.Next(1, 3) : (occupied > 2 ? 1 : 0);
                if (attempt >= 16)
                    tunnelBudget = 0;

                while (tunnelBudget + questionBudget >= occupied && tunnelBudget + questionBudget > 0)
                {
                    if (tunnelBudget >= questionBudget && tunnelBudget > 0)
                        tunnelBudget--;
                    else if (questionBudget > 0)
                        questionBudget--;
                    else
                        break;
                }

                if (tunnelBudget < 1 && occupied > 2 && attempt < 16)
                    tunnelBudget = 1;
                if (questionBudget < 1 && occupied > tunnelBudget + 1)
                    questionBudget = 1;

                List<int> tunnelCells = new List<int>(tunnelBudget);
                List<int> questionCells = new List<int>(questionBudget);
                List<int> shooterCells = new List<int>(occupied);
                for (int i = 0; i < occupied; i++)
                {
                    if (i < tunnelBudget)
                        tunnelCells.Add(openCells[i]);
                    else if (i < tunnelBudget + questionBudget)
                        questionCells.Add(openCells[i]);
                    else
                        shooterCells.Add(openCells[i]);
                }

                int innerPerTunnel = tunnelCells.Count > 0 ? 2 : 0;
                int slotCount = shooterCells.Count + questionCells.Count + tunnelCells.Count * innerPerTunnel;
                while (tunnelCells.Count > 0 && slotCount < colorIds.Count && innerPerTunnel < 6)
                {
                    innerPerTunnel++;
                    slotCount = shooterCells.Count + questionCells.Count + tunnelCells.Count * innerPerTunnel;
                }

                int reservedEmpty = Mathf.Max(4, cellCount * 30 / 100);
                int nextOpen = occupied;
                while (slotCount < colorIds.Count && nextOpen < openCells.Count && openCells.Count - nextOpen > reservedEmpty)
                {
                    shooterCells.Add(openCells[nextOpen]);
                    nextOpen++;
                    slotCount++;
                }

                List<int> colorQueue = BuildColorQueue(colorIds, pixelCounts, slotCount, random);

                int colorCursor = 0;
                PlaceShooterCells(shooterCells, colorQueue, ref colorCursor);
                PlaceQuestionCells(questionCells, colorQueue, ref colorCursor);
                PlaceTunnelCells(tunnelCells, walls, colorQueue, innerPerTunnel, ref colorCursor);

                for (int i = 0; i < cellCount; i++)
                {
                    if (!walls[i])
                        continue;

                    CellData cell = CellAt(i);
                    if (cell == null)
                        continue;

                    cell.ChangeLocked(true);
                }

                solved = CanExtractAllShooters();
            }

            level.gridYMigrated = true;
            level.ApplyShotPlan();
            level.editorShotPlan = null;
            EditorUtility.SetDirty(level);
        }

        private Dictionary<int, int> CollectPixelColorCounts()
        {
            Dictionary<int, int> pixelCounts = new Dictionary<int, int>(8);
            if (level.PixelGrid == null)
                return pixelCounts;

            int pixelWidth = level.PixelGrid.GetLength(0);
            int pixelHeight = level.PixelGrid.GetLength(1);
            for (int y = 0; y < pixelHeight; y++)
            {
                for (int x = 0; x < pixelWidth; x++)
                {
                    PixelCellData pixel = level.PixelGrid[x, y];
                    if (pixel == null || !pixel.HasPixel || pixel.PaletteId <= 0)
                        continue;

                    int colorId = pixel.PaletteId;
                    if (pixelCounts.TryGetValue(colorId, out int current))
                        pixelCounts[colorId] = current + 1;
                    else
                        pixelCounts[colorId] = 1;
                }
            }

            return pixelCounts;
        }

        private void EnsureBoardCapacity(int minimumCells)
        {
            if (level.width < 5)
                level.width = 5;

            if (level.height < 6)
                level.height = 6;

            if (level.CellData == null || level.CellData.GetLength(0) != level.width || level.CellData.GetLength(1) != level.height)
            {
                level.OnWidthChanged();
                level.OnHeightChanged();
            }

            while (level.width * level.height < minimumCells)
            {
                level.height++;
                level.OnHeightChanged();
            }

            for (int y = 0; y < level.height; y++)
            {
                for (int x = 0; x < level.width; x++)
                {
                    if (level.CellData[x, y] == null)
                        level.CellData[x, y] = new CellData();
                }
            }
        }

        private void ClearBoard()
        {
            if (level.questionShooterData == null)
                level.questionShooterData = new List<ShooterData>();
            else
                level.questionShooterData.Clear();

            if (level.tunnelShooterData == null)
                level.tunnelShooterData = new List<TunnelData>();
            else
                level.tunnelShooterData.Clear();

            for (int y = 0; y < level.height; y++)
            {
                for (int x = 0; x < level.width; x++)
                {
                    CellData cell = level.CellData[x, y];
                    if (cell == null)
                    {
                        cell = new CellData();
                        level.CellData[x, y] = cell;
                    }

                    cell.ColorLevel = 0;
                    cell.SetQuestionShooter(false);
                    cell.SetTunnelShooter(false);
                    cell.ChangeLocked(false);
                    cell.SetShotCount(0);
                    cell.Direction = Direction.None;
                    cell.DirectionLevel = 0;
                    cell.QuestionIndex = 0;
                    cell.TunnelIndex = 0;
                    cell.OnColorLevelChanged();
                }
            }
        }

        private void PlaceWallShapes(bool[] walls, System.Random random)
        {
            int pieceCount = Mathf.Clamp((level.width * level.height) / 16, 2, 5);
            List<Vector2Int> shape = new List<Vector2Int>(8);
            List<int> stamped = new List<int>(8);
            int placed = 0;
            int attempts = 0;
            while (placed < pieceCount && attempts < pieceCount * 24)
            {
                attempts++;
                CopyWallShape(random.Next(6), shape);
                RotateShape(shape, random.Next(4));
                if (!TryStampShape(walls, shape, random, stamped))
                    continue;

                placed++;
            }
        }

        private void CopyWallShape(int shapeId, List<Vector2Int> shape)
        {
            shape.Clear();
            if (shapeId == 0)
            {
                shape.Add(new Vector2Int(0, 0));
                shape.Add(new Vector2Int(0, 1));
                shape.Add(new Vector2Int(0, 2));
                shape.Add(new Vector2Int(1, 2));
                return;
            }

            if (shapeId == 1)
            {
                shape.Add(new Vector2Int(0, 0));
                shape.Add(new Vector2Int(1, 0));
                shape.Add(new Vector2Int(2, 0));
                shape.Add(new Vector2Int(1, 1));
                return;
            }

            if (shapeId == 2)
            {
                shape.Add(new Vector2Int(0, 0));
                shape.Add(new Vector2Int(0, 1));
                shape.Add(new Vector2Int(0, 2));
                return;
            }

            if (shapeId == 3)
            {
                shape.Add(new Vector2Int(0, 1));
                shape.Add(new Vector2Int(1, 1));
                shape.Add(new Vector2Int(1, 0));
                shape.Add(new Vector2Int(2, 0));
                return;
            }

            if (shapeId == 4)
            {
                shape.Add(new Vector2Int(0, 0));
                shape.Add(new Vector2Int(0, 1));
                shape.Add(new Vector2Int(1, 0));
                shape.Add(new Vector2Int(2, 0));
                shape.Add(new Vector2Int(2, 1));
                return;
            }

            shape.Add(new Vector2Int(0, 0));
            shape.Add(new Vector2Int(1, 0));
            shape.Add(new Vector2Int(1, 1));
            shape.Add(new Vector2Int(1, 2));
            shape.Add(new Vector2Int(2, 1));
        }

        private void RotateShape(List<Vector2Int> shape, int turns)
        {
            int quarter = turns % 4;
            if (quarter < 0)
                quarter += 4;

            for (int step = 0; step < quarter; step++)
            {
                for (int i = 0; i < shape.Count; i++)
                {
                    Vector2Int cell = shape[i];
                    shape[i] = new Vector2Int(cell.y, -cell.x);
                }
            }

            int minX = shape[0].x;
            int minY = shape[0].y;
            for (int i = 1; i < shape.Count; i++)
            {
                if (shape[i].x < minX)
                    minX = shape[i].x;
                if (shape[i].y < minY)
                    minY = shape[i].y;
            }

            for (int i = 0; i < shape.Count; i++)
                shape[i] = new Vector2Int(shape[i].x - minX, shape[i].y - minY);
        }

        private bool TryStampShape(bool[] walls, List<Vector2Int> shape, System.Random random, List<int> stamped)
        {
            int maxX = 0;
            int maxY = 0;
            for (int i = 0; i < shape.Count; i++)
            {
                if (shape[i].x > maxX)
                    maxX = shape[i].x;
                if (shape[i].y > maxY)
                    maxY = shape[i].y;
            }

            int originSpanX = level.width - maxX;
            int originSpanY = level.height - maxY;
            if (originSpanX < 1 || originSpanY < 1)
                return false;

            int originX = random.Next(originSpanX);
            int originY = random.Next(originSpanY);
            stamped.Clear();
            for (int i = 0; i < shape.Count; i++)
            {
                int x = originX + shape[i].x;
                int y = originY + shape[i].y;
                int index = x + y * level.width;
                if (walls[index])
                    return false;

                stamped.Add(index);
            }

            for (int i = 0; i < stamped.Count; i++)
                walls[stamped[i]] = true;

            if (WallsKeepExits(walls))
                return true;

            for (int i = 0; i < stamped.Count; i++)
                walls[stamped[i]] = false;

            return false;
        }

        private bool WallsKeepExits(bool[] walls)
        {
            int width = level.width;
            int height = level.height;
            bool[] reached = new bool[walls.Length];
            int[] queue = new int[walls.Length];
            int head = 0;
            int tail = 0;

            for (int x = 0; x < width; x++)
            {
                if (walls[x])
                    continue;

                reached[x] = true;
                queue[tail++] = x;
            }

            if (tail == 0)
                return false;

            while (head < tail)
            {
                int current = queue[head++];
                int cellX = current % width;
                int cellY = current / width;
                TryVisit(walls, reached, queue, ref tail, cellX + 1, cellY, width, height);
                TryVisit(walls, reached, queue, ref tail, cellX - 1, cellY, width, height);
                TryVisit(walls, reached, queue, ref tail, cellX, cellY + 1, width, height);
                TryVisit(walls, reached, queue, ref tail, cellX, cellY - 1, width, height);
            }

            for (int i = 0; i < walls.Length; i++)
            {
                if (!walls[i] && !reached[i])
                    return false;
            }

            return true;
        }

        private void TryVisit(
            bool[] walls,
            bool[] reached,
            int[] queue,
            ref int tail,
            int x,
            int y,
            int width,
            int height)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int index = x + y * width;
            if (walls[index] || reached[index])
                return;

            reached[index] = true;
            queue[tail++] = index;
        }

        private List<int> BuildColorQueue(
            List<int> colorIds,
            Dictionary<int, int> pixelCounts,
            int slotCount,
            System.Random random)
        {
            List<int> queue = new List<int>(slotCount);
            for (int i = 0; i < colorIds.Count && queue.Count < slotCount; i++)
                queue.Add(colorIds[i]);

            int weightTotal = 0;
            for (int i = 0; i < colorIds.Count; i++)
                weightTotal += pixelCounts[colorIds[i]];

            if (weightTotal < 1)
                weightTotal = 1;

            while (queue.Count < slotCount)
            {
                int roll = random.Next(weightTotal);
                int cursor = 0;
                int picked = colorIds[colorIds.Count - 1];
                for (int i = 0; i < colorIds.Count; i++)
                {
                    cursor += pixelCounts[colorIds[i]];
                    if (roll < cursor)
                    {
                        picked = colorIds[i];
                        break;
                    }
                }

                queue.Add(picked);
            }

            Shuffle(queue, random);
            return queue;
        }

        private void PlaceShooterCells(List<int> cells, List<int> colorQueue, ref int colorCursor)
        {
            cells.Sort(CompareGameOrder);
            for (int i = 0; i < cells.Count; i++)
            {
                CellData cell = CellAt(cells[i]);
                if (cell == null || colorCursor >= colorQueue.Count)
                    continue;

                cell.ColorLevel = colorQueue[colorCursor++];
                cell.SetShotCount(0);
                cell.OnColorLevelChanged();
            }
        }

        private void PlaceQuestionCells(List<int> cells, List<int> colorQueue, ref int colorCursor)
        {
            cells.Sort(CompareGameOrder);
            for (int i = 0; i < cells.Count; i++)
            {
                CellData cell = CellAt(cells[i]);
                if (cell == null || colorCursor >= colorQueue.Count)
                    continue;

                int insertIndex = level.CountQuestionShootersBefore(cell);
                level.SetQuestionCell(cell, true);
                if (level.questionShooterData != null && insertIndex >= 0 && insertIndex < level.questionShooterData.Count)
                    level.questionShooterData[insertIndex].SetColor((GeneralColor)colorQueue[colorCursor]);

                colorCursor++;
            }
        }

        private void PlaceTunnelCells(List<int> cells, bool[] walls, List<int> colorQueue, int innerCount, ref int colorCursor)
        {
            int cellCount = level.width * level.height;
            bool[] blocked = new bool[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                CellData cell = CellAt(i);
                bool occupied = cell != null && (cell.ColorLevel != 0 || cell.IsQuestionShooter || cell.IsTunnel);
                blocked[i] = (walls != null && i < walls.Length && walls[i]) || occupied;
            }

            cells.Sort(CompareGameOrder);
            for (int i = 0; i < cells.Count; i++)
            {
                int cellIndex = cells[i];
                if (!TryPickTunnelDirection(cellIndex, blocked, out Direction direction))
                {
                    if (!TryFindOpenTunnelCell(blocked, out cellIndex, out direction))
                        continue;
                }

                CellData cell = CellAt(cellIndex);
                if (cell == null)
                    continue;

                blocked[cellIndex] = true;
                int exitIndex = TunnelExitIndex(cellIndex, direction);
                if (exitIndex >= 0 && exitIndex < blocked.Length)
                    blocked[exitIndex] = true;

                int insertIndex = level.CountTunnelsBefore(cell);
                level.SetTunnelCell(cell, true);
                cell.DirectionLevel = (int)direction;
                cell.OnDirectionLevelChanged();

                List<ShooterData> inners = new List<ShooterData>(innerCount);
                for (int inner = 0; inner < innerCount && colorCursor < colorQueue.Count; inner++)
                {
                    GeneralColor color = (GeneralColor)colorQueue[colorCursor++];
                    inners.Add(new ShooterData(color, false, Vector2Int.zero, Vector3.zero));
                }

                if (inners.Count == 0)
                    inners.Add(new ShooterData(GeneralColor.Red, false, Vector2Int.zero, Vector3.zero));

                if (level.tunnelShooterData != null && insertIndex >= 0 && insertIndex < level.tunnelShooterData.Count)
                    level.tunnelShooterData[insertIndex].SetShooters(inners);
            }
        }

        private bool TryPickTunnelDirection(int cellIndex, bool[] blocked, out Direction direction)
        {
            direction = Direction.None;
            Direction[] options =
            {
                Direction.Up,
                Direction.Right,
                Direction.Left,
                Direction.Down
            };

            for (int i = 0; i < options.Length; i++)
            {
                if (!IsTunnelExitOpen(cellIndex, options[i], blocked))
                    continue;

                direction = options[i];
                return true;
            }

            return false;
        }

        private bool TryFindOpenTunnelCell(bool[] blocked, out int cellIndex, out Direction direction)
        {
            cellIndex = -1;
            direction = Direction.None;
            for (int i = 0; i < blocked.Length; i++)
            {
                if (blocked[i])
                    continue;

                if (!TryPickTunnelDirection(i, blocked, out direction))
                    continue;

                cellIndex = i;
                return true;
            }

            return false;
        }

        private bool IsTunnelExitOpen(int cellIndex, Direction direction, bool[] blocked)
        {
            int exitIndex = TunnelExitIndex(cellIndex, direction);
            if (exitIndex < 0 || blocked == null || exitIndex >= blocked.Length)
                return false;

            return !blocked[exitIndex];
        }

        private int TunnelExitIndex(int cellIndex, Direction direction)
        {
            int x = cellIndex % level.width;
            int arrayY = cellIndex / level.width;
            if (direction == Direction.Left)
                x--;
            else if (direction == Direction.Right)
                x++;
            else if (direction == Direction.Up)
                arrayY--;
            else if (direction == Direction.Down)
                arrayY++;
            else
                return -1;

            if (x < 0 || arrayY < 0 || x >= level.width || arrayY >= level.height)
                return -1;

            return x + arrayY * level.width;
        }

        private CellData CellAt(int index)
        {
            int x = index % level.width;
            int y = index / level.width;
            if (x < 0 || y < 0 || x >= level.width || y >= level.height)
                return null;

            return level.CellData[x, y];
        }

        private int CompareGameOrder(int a, int b)
        {
            return GameOrderKey(a).CompareTo(GameOrderKey(b));
        }

        private int GameOrderKey(int index)
        {
            int x = index % level.width;
            int row = index / level.width;
            int gameY = level.height - 1 - row;
            return gameY * level.width + x;
        }

        private bool CanExtractAllShooters()
        {
            int width = level.width;
            int height = level.height;
            int cellCount = width * height;
            bool[] blocked = new bool[cellCount];
            bool[] occupied = new bool[cellCount];
            int[] tunnelOrder = new int[8];
            int tunnelCount = 0;

            for (int gameY = 0; gameY < height; gameY++)
            {
                int row = height - 1 - gameY;
                for (int x = 0; x < width; x++)
                {
                    int index = x + row * width;
                    CellData cell = level.CellData[x, row];
                    if (cell == null)
                        continue;

                    if (cell.IsLocked)
                    {
                        blocked[index] = true;
                        continue;
                    }

                    if (cell.IsTunnel)
                    {
                        blocked[index] = true;
                        if (tunnelCount >= tunnelOrder.Length)
                        {
                            int[] grown = new int[tunnelOrder.Length * 2];
                            for (int copy = 0; copy < tunnelOrder.Length; copy++)
                                grown[copy] = tunnelOrder[copy];
                            tunnelOrder = grown;
                        }

                        tunnelOrder[tunnelCount] = index;
                        tunnelCount++;
                        continue;
                    }

                    if (cell.IsFull || cell.IsQuestionShooter)
                        occupied[index] = true;
                }
            }

            int[] pending = new int[tunnelCount];
            for (int i = 0; i < tunnelCount; i++)
            {
                int count = 0;
                if (level.tunnelShooterData != null && i < level.tunnelShooterData.Count && level.tunnelShooterData[i] != null && level.tunnelShooterData[i].Shooters != null)
                    count = level.tunnelShooterData[i].Shooters.Count;
                pending[i] = count;
            }

            int[] queue = new int[cellCount];
            bool[] visited = new bool[cellCount];
            bool progressed = true;
            while (progressed)
            {
                progressed = false;
                for (int i = 0; i < cellCount; i++)
                {
                    if (!occupied[i])
                        continue;
                    if (!CanReachExit(i, occupied, blocked, visited, queue, width, height))
                        continue;

                    occupied[i] = false;
                    progressed = true;
                }

                for (int tunnel = 0; tunnel < tunnelCount; tunnel++)
                {
                    if (pending[tunnel] <= 0)
                        continue;

                    int tunnelCell = tunnelOrder[tunnel];
                    CellData cell = CellAt(tunnelCell);
                    if (cell == null)
                        continue;

                    int arrayY = tunnelCell / width;
                    if (cell.Direction == Direction.Up && arrayY == 0)
                    {
                        pending[tunnel] = 0;
                        progressed = true;
                        continue;
                    }

                    int exitIndex = TunnelExitIndex(tunnelCell, cell.Direction);
                    if (exitIndex < 0 || blocked[exitIndex] || occupied[exitIndex])
                        continue;

                    occupied[exitIndex] = true;
                    pending[tunnel]--;
                    progressed = true;
                }
            }

            for (int i = 0; i < cellCount; i++)
            {
                if (occupied[i])
                    return false;
            }

            for (int tunnel = 0; tunnel < tunnelCount; tunnel++)
            {
                if (pending[tunnel] > 0)
                    return false;
            }

            return true;
        }

        private bool CanReachExit(int start, bool[] occupied, bool[] blocked, bool[] visited, int[] queue, int width, int height)
        {
            if (start / width == 0)
                return true;

            for (int i = 0; i < visited.Length; i++)
                visited[i] = false;

            int head = 0;
            int tail = 0;
            visited[start] = true;
            queue[tail++] = start;

            while (head < tail)
            {
                int current = queue[head++];
                int x = current % width;
                int y = current / width;
                if (TryExitStep(x + 1, y, occupied, blocked, visited, queue, ref tail, width, height))
                    return true;
                if (TryExitStep(x - 1, y, occupied, blocked, visited, queue, ref tail, width, height))
                    return true;
                if (TryExitStep(x, y + 1, occupied, blocked, visited, queue, ref tail, width, height))
                    return true;
                if (TryExitStep(x, y - 1, occupied, blocked, visited, queue, ref tail, width, height))
                    return true;
            }

            return false;
        }

        private bool TryExitStep(
            int x,
            int y,
            bool[] occupied,
            bool[] blocked,
            bool[] visited,
            int[] queue,
            ref int tail,
            int width,
            int height)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return false;

            int index = x + y * width;
            if (visited[index] || blocked[index] || occupied[index])
                return false;

            if (y == 0)
                return true;

            visited[index] = true;
            queue[tail++] = index;
            return false;
        }

        private void Shuffle<T>(List<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                T value = list[i];
                list[i] = list[swap];
                list[swap] = value;
            }
        }
    }
}
#endif
