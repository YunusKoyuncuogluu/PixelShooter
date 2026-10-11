using System;
using System.Collections.Generic;
using Grids;
using LevelManagement;
using UnityEngine;
using Zenject;

public class ImageManager
{
    private const short EmptyPaletteId = -1;

    private enum ApproachSide : byte
    {
        Bottom = 0,
        Top = 1,
        Left = 2,
        Right = 3
    }

    [Inject] private ProjectSettings projectSettings;
    [InjectOptional] private ImageGridRenderer imageGridRenderer;

    private Color32[] shooterColorsByLevel;
    private int[] shooterLevels;
    private int shooterColorCount;

    private LevelData levelData;
    private int gridWidth;
    private int gridHeight;
    private int opaqueRemaining;

    private short[] paletteIds;
    private bool[] reserved;
    private Vector3[] worldPositions;
    private bool isBuilt;

    private float boundsMinX;
    private float boundsMaxX;
    private float boundsMinZ;
    private float boundsMaxZ;
    private Vector3 gridCenter;

    public event Action OnImageCleared;

    public int OpaqueRemaining => opaqueRemaining;
    public Vector3 GridCenter => gridCenter;

    public void Construct(LevelData data)
    {
        levelData = data;
        isBuilt = false;
        opaqueRemaining = 0;
        paletteIds = null;
        reserved = null;
        worldPositions = null;
        gridWidth = 0;
        gridHeight = 0;
        gridCenter = Vector3.zero;

        BuildShooterColorLookup();

        if (levelData == null || levelData.PixelGrid == null)
        {
            return;
        }

        Vector2Int size = levelData.PixelSize;
        gridWidth = size.x > 0 ? size.x : 1;
        gridHeight = size.y > 0 ? size.y : 1;

        int cellCount = gridWidth * gridHeight;
        paletteIds = new short[cellCount];
        reserved = new bool[cellCount];
        worldPositions = new Vector3[cellCount];

        PixelCellData[,] grid = levelData.PixelGrid;
        int opaque = 0;
        int missingPalette = 0;

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int flat = ToFlatIndex(x, y);
                PixelCellData cell = null;
                if (x < grid.GetLength(0) && y < grid.GetLength(1))
                    cell = grid[x, y];

                if (cell == null || !cell.HasPixel)
                {
                    paletteIds[flat] = EmptyPaletteId;
                    worldPositions[flat] = Vector3.zero;
                    continue;
                }

                short bakedId = cell.PaletteId;
                if (bakedId <= 0)
                {
                    paletteIds[flat] = EmptyPaletteId;
                    worldPositions[flat] = ResolveWorldPosition(new Vector2Int(x, y));
                    missingPalette++;
                    continue;
                }

                paletteIds[flat] = bakedId;
                worldPositions[flat] = ResolveWorldPosition(new Vector2Int(x, y));
                opaque++;
            }
        }

        opaqueRemaining = opaque;
        isBuilt = true;
        CacheImageWorldBounds();

        ApplyLevelPaletteSwatches();

        if (imageGridRenderer != null)
            imageGridRenderer.RetintToPalette(shooterLevels, shooterColorsByLevel, shooterColorCount);

    }

    public bool TryClaimFrontPixel(
        Vector3 shooterWorldPos,
        short targetPaletteId,
        int previousSide,
        int previousLane,
        out int side,
        out int lane,
        out Vector2Int gridPos,
        out Vector3 pixelWorldPos)
    {
        gridPos = default;
        pixelWorldPos = default;
        side = 0;
        lane = 0;

        if (!isBuilt || paletteIds == null || targetPaletteId <= 0)
            return false;

        ApproachSide approach = ResolveApproachSide(shooterWorldPos.x, shooterWorldPos.z);
        side = (int)approach;
        lane = ResolvePreferredLane(shooterWorldPos, approach);
        if (side == previousSide && lane == previousLane)
            return false;

        if (!TryGetFrontierPixelOnLane(approach, lane, targetPaletteId, out gridPos, out pixelWorldPos))
            return false;

        return ReservePixel(gridPos);
    }

    public void ActivatePixel(Vector2Int gridPos)
    {
        if (!isBuilt || paletteIds == null || !IsInsideGrid(gridPos))
            return;

        int flat = ToFlatIndex(gridPos.x, gridPos.y);
        if (paletteIds[flat] == EmptyPaletteId)
            return;

        reserved[flat] = false;
        paletteIds[flat] = EmptyPaletteId;
        worldPositions[flat] = Vector3.zero;

        if (imageGridRenderer != null)
            imageGridRenderer.HidePixel(gridPos);

        opaqueRemaining--;
        if (opaqueRemaining < 0)
            opaqueRemaining = 0;

        if (opaqueRemaining == 0)
            OnImageCleared?.Invoke();
    }

    public void ReleaseReservation(Vector2Int gridPos)
    {
        if (!isBuilt || reserved == null || !IsInsideGrid(gridPos))
            return;

        reserved[ToFlatIndex(gridPos.x, gridPos.y)] = false;
    }

    public bool IsImageCleared() => isBuilt && opaqueRemaining <= 0;

    public int CountUnreservedPixels(int[] counts)
    {
        if (counts == null)
            return -1;

        int bucketCount = counts.Length;
        for (int i = 0; i < bucketCount; i++)
            counts[i] = 0;

        if (!isBuilt || paletteIds == null || reserved == null)
            return 0;

        int total = 0;
        int cellCount = paletteIds.Length;
        for (int i = 0; i < cellCount; i++)
        {
            short paletteId = paletteIds[i];
            if (paletteId <= 0 || reserved[i])
                continue;

            if (paletteId >= bucketCount)
                return -1;

            counts[paletteId]++;
            total++;
        }

        return total;
    }

    public bool HasRemainingPixelsOfPaletteId(short targetPaletteId)
    {
        if (!isBuilt || paletteIds == null || targetPaletteId <= 0)
            return false;

        int count = paletteIds.Length;
        for (int i = 0; i < count; i++)
        {
            if (paletteIds[i] == targetPaletteId && !reserved[i])
                return true;
        }

        return false;
    }

    public short ResolvePaletteId(GeneralColor generalColor)
    {
        if (generalColor == GeneralColor.None)
            return EmptyPaletteId;

        short id = (short)(int)generalColor;
        return id <= 0 ? EmptyPaletteId : id;
    }

    public Color32 ResolveShooterColor(GeneralColor generalColor)
    {
        int level = (int)generalColor;
        for (int i = 0; i < shooterColorCount; i++)
        {
            if (shooterLevels[i] == level)
                return shooterColorsByLevel[i];
        }

        return new Color32(255, 255, 255, 255);
    }

    private bool ReservePixel(Vector2Int gridPos)
    {
        if (!isBuilt || paletteIds == null || reserved == null || !IsInsideGrid(gridPos))
            return false;

        int flat = ToFlatIndex(gridPos.x, gridPos.y);
        if (paletteIds[flat] == EmptyPaletteId || reserved[flat])
            return false;

        reserved[flat] = true;
        return true;
    }

    private void CacheImageWorldBounds()
    {
        if (gridWidth < 1 || gridHeight < 1)
        {
            boundsMinX = boundsMaxX = boundsMinZ = boundsMaxZ = 0f;
            gridCenter = Vector3.zero;
            return;
        }

        Vector3 corner00 = ResolveWorldPosition(new Vector2Int(0, 0));
        Vector3 corner10 = ResolveWorldPosition(new Vector2Int(gridWidth - 1, 0));
        Vector3 corner01 = ResolveWorldPosition(new Vector2Int(0, gridHeight - 1));
        Vector3 corner11 = ResolveWorldPosition(new Vector2Int(gridWidth - 1, gridHeight - 1));

        boundsMinX = Mathf.Min(Mathf.Min(corner00.x, corner10.x), Mathf.Min(corner01.x, corner11.x));
        boundsMaxX = Mathf.Max(Mathf.Max(corner00.x, corner10.x), Mathf.Max(corner01.x, corner11.x));
        boundsMinZ = Mathf.Min(Mathf.Min(corner00.z, corner10.z), Mathf.Min(corner01.z, corner11.z));
        boundsMaxZ = Mathf.Max(Mathf.Max(corner00.z, corner10.z), Mathf.Max(corner01.z, corner11.z));

        float centerY = (corner00.y + corner10.y + corner01.y + corner11.y) * 0.25f;
        gridCenter = new Vector3((boundsMinX + boundsMaxX) * 0.5f, centerY, (boundsMinZ + boundsMaxZ) * 0.5f);
    }

    private ApproachSide ResolveApproachSide(float shooterX, float shooterZ)
    {
        float outBottom = boundsMinZ - shooterZ;
        float outTop = shooterZ - boundsMaxZ;
        float outLeft = boundsMinX - shooterX;
        float outRight = shooterX - boundsMaxX;

        ApproachSide best = ApproachSide.Bottom;
        float bestScore = outBottom;

        if (outTop > bestScore)
        {
            bestScore = outTop;
            best = ApproachSide.Top;
        }

        if (outLeft > bestScore)
        {
            bestScore = outLeft;
            best = ApproachSide.Left;
        }

        if (outRight > bestScore)
            best = ApproachSide.Right;

        return best;
    }

    private int ResolvePreferredLane(Vector3 shooterWorldPos, ApproachSide side)
    {
        ProjectShooterToGrid(shooterWorldPos, out int col, out int row);
        return side == ApproachSide.Bottom || side == ApproachSide.Top ? col : row;
    }

    private void ProjectShooterToGrid(Vector3 shooterWorldPos, out int col, out int row)
    {
        float spacing = imageGridRenderer != null ? imageGridRenderer.CellSpacing : 0.25f;
        if (spacing < 0.0001f)
            spacing = 0.25f;

        Vector3 anchor = imageGridRenderer != null
            ? imageGridRenderer.transform.position
            : Vector3.zero;
        Quaternion rotation = imageGridRenderer != null
            ? imageGridRenderer.transform.rotation
            : Quaternion.identity;

        Vector3 local = Quaternion.Inverse(rotation) * (shooterWorldPos - anchor);
        float halfW = (gridWidth - 1) * 0.5f;
        float halfH = (gridHeight - 1) * 0.5f;

        col = Mathf.Clamp(Mathf.RoundToInt(local.x / spacing + halfW), 0, gridWidth > 0 ? gridWidth - 1 : 0);
        row = Mathf.Clamp(Mathf.RoundToInt(local.z / spacing + halfH), 0, gridHeight > 0 ? gridHeight - 1 : 0);
    }

    private bool TryGetFrontierPixelOnLane(
        ApproachSide side,
        int lane,
        short targetPaletteId,
        out Vector2Int gridPos,
        out Vector3 pixelWorldPos)
    {
        gridPos = default;
        pixelWorldPos = default;

        int width = gridWidth;
        int height = gridHeight;
        if (width < 1 || height < 1)
            return false;

        if (side == ApproachSide.Bottom || side == ApproachSide.Top)
        {
            int x = Mathf.Clamp(lane, 0, width - 1);
            int yStart = side == ApproachSide.Bottom ? 0 : height - 1;
            int yEnd = side == ApproachSide.Bottom ? height : -1;
            int yStep = side == ApproachSide.Bottom ? 1 : -1;

            for (int y = yStart; y != yEnd; y += yStep)
            {
                int flat = ToFlatIndex(x, y);
                if (paletteIds[flat] == EmptyPaletteId || reserved[flat])
                    continue;
                if (paletteIds[flat] != targetPaletteId)
                    return false;

                gridPos = new Vector2Int(x, y);
                pixelWorldPos = worldPositions[flat];
                return true;
            }

            return false;
        }

        int row = Mathf.Clamp(lane, 0, height - 1);
        int xStart = side == ApproachSide.Left ? 0 : width - 1;
        int xEnd = side == ApproachSide.Left ? width : -1;
        int xStep = side == ApproachSide.Left ? 1 : -1;

        for (int x = xStart; x != xEnd; x += xStep)
        {
            int flat = ToFlatIndex(x, row);
            if (paletteIds[flat] == EmptyPaletteId || reserved[flat])
                continue;
            if (paletteIds[flat] != targetPaletteId)
                return false;

            gridPos = new Vector2Int(x, row);
            pixelWorldPos = worldPositions[flat];
            return true;
        }

        return false;
    }

    private void BuildShooterColorLookup()
    {
        shooterColorCount = 0;
        shooterColorsByLevel = null;
        shooterLevels = null;

        if (projectSettings == null)
            return;

        IList<FacadeProperty> facades = projectSettings.GetOriginalFacadeProperties();
        if (facades == null || facades.Count == 0)
            return;

        int facadeCount = facades.Count;
        shooterColorsByLevel = new Color32[facadeCount];
        shooterLevels = new int[facadeCount];

        for (int i = 0; i < facadeCount; i++)
        {
            FacadeProperty facade = facades[i];
            if (facade == null || facade.Level <= 0)
                continue;

            Color facadeColor = facade.Color;
            shooterColorsByLevel[shooterColorCount] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(facadeColor.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(facadeColor.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(facadeColor.b * 255f), 0, 255),
                255);
            shooterLevels[shooterColorCount] = facade.Level;
            shooterColorCount++;
        }
    }

    private void ApplyLevelPaletteSwatches()
    {
        if (levelData == null || shooterColorsByLevel == null || shooterLevels == null)
            return;

        for (int i = 0; i < shooterColorCount; i++)
        {
            if (!levelData.TryGetPaletteSwatch((short)shooterLevels[i], out Color swatch))
                continue;

            shooterColorsByLevel[i] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(swatch.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(swatch.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(swatch.b * 255f), 0, 255),
                255);
        }
    }

    private Vector3 ResolveWorldPosition(Vector2Int gridPos)
    {
        if (imageGridRenderer != null &&
            imageGridRenderer.TryGetWorldPosition(gridPos, out Vector3 fromRenderer))
        {
            return fromRenderer;
        }

        float spacing = imageGridRenderer != null ? imageGridRenderer.CellSpacing : 0.25f;
        Vector3 anchor = imageGridRenderer != null
            ? imageGridRenderer.transform.position
            : Vector3.zero;
        Quaternion rotation = imageGridRenderer != null
            ? imageGridRenderer.transform.rotation
            : Quaternion.identity;

        float halfW = (gridWidth - 1) * 0.5f;
        float halfH = (gridHeight - 1) * 0.5f;
        float localX = (gridPos.x - halfW) * spacing;
        float localZ = (gridPos.y - halfH) * spacing;
        return anchor + rotation * new Vector3(localX, 0f, localZ);
    }

    private bool IsInsideGrid(Vector2Int gridPos)
    {
        return gridPos.x >= 0 && gridPos.y >= 0 &&
               gridPos.x < gridWidth && gridPos.y < gridHeight;
    }

    private int ToFlatIndex(int x, int y) => x + y * gridWidth;
}
