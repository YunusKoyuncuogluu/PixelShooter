using System;
using System.Collections.Generic;
using Grids;
using LevelManagement;
using UnityEngine;
using Zenject;

/// <summary>
/// Pixel grid mantik katmani.
/// Bake: paletteId (Facade Level). Runtime claim: sadece int id esitligi.
/// </summary>
public class FabricManager
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
    [InjectOptional] private FabricGridRenderer fabricGridRenderer;

    private Color32[] spoolColorsByLevel;
    private int[] spoolLevels;
    private int spoolColorCount;

    private LevelData levelData;
    private int gridWidth;
    private int gridHeight;
    private int opaqueRemaining;

    private short[] paletteIds;
    private Vector3[] worldPositions;
    private bool isBuilt;

    // Soyulunca daralmayan fabric AABB (kenar secimi).
    private float boundsMinX;
    private float boundsMaxX;
    private float boundsMinZ;
    private float boundsMaxZ;

    public event Action OnFabricCleared;

    public int OpaqueRemaining => opaqueRemaining;

    /// <summary> Bake edilmis paletteId dizisini kurar. </summary>
    public void Construct(LevelData data)
    {
        levelData = data;
        isBuilt = false;
        opaqueRemaining = 0;
        paletteIds = null;
        worldPositions = null;
        gridWidth = 0;
        gridHeight = 0;

        BuildSpoolColorLookup();

        if (levelData == null || levelData.PixelGrid == null)
        {
            Debug.LogWarning("[FabricManager] LevelData / PixelGrid yok.");
            return;
        }

        Vector2Int size = levelData.PixelSize;
        gridWidth = Mathf.Max(1, size.x);
        gridHeight = Mathf.Max(1, size.y);

        int cellCount = gridWidth * gridHeight;
        paletteIds = new short[cellCount];
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
        CacheFabricWorldBounds();

        if (missingPalette > 0)
        {
            Debug.LogWarning(
                $"[FabricManager] {missingPalette} pikselde gecerli paletteId yok. Level Baker ile yeniden bake edin.");
        }

        if (opaqueRemaining == 0)
        {
            Debug.LogError(
                "[FabricManager] 0 gecerli paletteId'li piksel. Level Baker ile bake edin.");
        }
    }

    /// <summary> Spool'un hizali oldugu sutun (Bottom/Top) veya satir (Left/Right). </summary>
    public int ResolvePreferredLane(Vector3 spoolWorldPos)
    {
        if (!isBuilt)
            return 0;

        ApproachSide side = ResolveApproachSide(spoolWorldPos.x, spoolWorldPos.z);
        return ResolvePreferredLane(spoolWorldPos, side);
    }

    /// <summary> Kenar index (lane takibini kenar degisiminde sifirlamak icin). </summary>
    public int ResolveApproachSideIndex(Vector3 spoolWorldPos)
    {
        if (!isBuilt)
            return 0;
        return (int)ResolveApproachSide(spoolWorldPos.x, spoolWorldPos.z);
    }

    /// <summary>
    /// Kenar siluetinden claim. previousLane ile hiza arasindaki atlanan seritleri 1'er doldurur.
    /// Hot-path: 0 GC.
    /// </summary>
    public bool TryClaimClosestPixelOfColor(
        Vector3 spoolWorldPos,
        short targetPaletteId,
        float searchRadius,
        int previousLane,
        out Vector2Int closestGridPos,
        out Vector3 pixelWorldPos,
        out int claimedLane)
    {
        if (!TryFindClaimTarget(
                spoolWorldPos,
                targetPaletteId,
                searchRadius,
                previousLane,
                out closestGridPos,
                out pixelWorldPos,
                out claimedLane))
        {
            return false;
        }

        if (!ConsumePixel(closestGridPos))
        {
            claimedLane = -1;
            return false;
        }

        return true;
    }

    public bool IsFabricCleared() => isBuilt && opaqueRemaining <= 0;

    public bool HasRemainingPixelsOfPaletteId(short targetPaletteId)
    {
        if (!isBuilt || paletteIds == null || targetPaletteId <= 0)
            return false;

        int count = paletteIds.Length;
        for (int i = 0; i < count; i++)
        {
            if (paletteIds[i] == targetPaletteId)
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

    public Color32 ResolveSpoolColor(GeneralColor generalColor)
    {
        int level = (int)generalColor;
        for (int i = 0; i < spoolColorCount; i++)
        {
            if (spoolLevels[i] == level)
                return spoolColorsByLevel[i];
        }

        return new Color32(255, 255, 255, 255);
    }

    private bool TryFindClaimTarget(
        Vector3 spoolWorldPos,
        short targetPaletteId,
        float searchRadius,
        int previousLane,
        out Vector2Int closestGridPos,
        out Vector3 pixelWorldPos,
        out int claimedLane)
    {
        closestGridPos = default;
        pixelWorldPos = default;
        claimedLane = -1;

        if (!isBuilt || paletteIds == null || worldPositions == null || targetPaletteId <= 0)
            return false;

        ApproachSide side = ResolveApproachSide(spoolWorldPos.x, spoolWorldPos.z);
        int laneMax = GetLaneCount(side) - 1;
        if (laneMax < 0)
            return false;

        int preferredLane = Mathf.Clamp(ResolvePreferredLane(spoolWorldPos, side), 0, laneMax);
        float radiusSqr = searchRadius * searchRadius;

        if (previousLane == int.MinValue)
        {
            return TryGetNearestFrontierOnSide(
                side,
                targetPaletteId,
                spoolWorldPos,
                radiusSqr,
                out closestGridPos,
                out pixelWorldPos,
                out claimedLane);
        }

        previousLane = Mathf.Clamp(previousLane, 0, laneMax);
        if (preferredLane == previousLane)
            return false;

        int step = preferredLane > previousLane ? 1 : -1;
        int endLane = preferredLane;

        for (int lane = previousLane + step;; lane += step)
        {
            if (TryGetFrontierPixelOnLane(side, lane, targetPaletteId, out Vector2Int gridPos, out Vector3 worldPos) &&
                IsWithinRadiusSqr(spoolWorldPos, worldPos, radiusSqr))
            {
                closestGridPos = gridPos;
                pixelWorldPos = worldPos;
                claimedLane = lane;
                return true;
            }

            if (lane == endLane)
                break;
        }

        return false;
    }

    private bool TryGetNearestFrontierOnSide(
        ApproachSide side,
        short targetPaletteId,
        Vector3 spoolWorldPos,
        float radiusSqr,
        out Vector2Int closestGridPos,
        out Vector3 pixelWorldPos,
        out int claimedLane)
    {
        closestGridPos = default;
        pixelWorldPos = default;
        claimedLane = -1;

        int laneCount = GetLaneCount(side);
        float bestSqr = float.MaxValue;
        bool found = false;

        for (int lane = 0; lane < laneCount; lane++)
        {
            if (!TryGetFrontierPixelOnLane(side, lane, targetPaletteId, out Vector2Int gridPos, out Vector3 worldPos))
                continue;

            float sqr = SqrDistanceXZ(spoolWorldPos, worldPos);
            if (sqr > radiusSqr || sqr >= bestSqr)
                continue;

            bestSqr = sqr;
            closestGridPos = gridPos;
            pixelWorldPos = worldPos;
            claimedLane = lane;
            found = true;
        }

        return found;
    }

    private bool ConsumePixel(Vector2Int gridPos)
    {
        if (!isBuilt || paletteIds == null || !IsInsideGrid(gridPos))
            return false;

        int flat = ToFlatIndex(gridPos.x, gridPos.y);
        if (paletteIds[flat] == EmptyPaletteId)
            return false;

        // Runtime dizi + GPU; LevelData asset'e dokunma.
        paletteIds[flat] = EmptyPaletteId;
        worldPositions[flat] = Vector3.zero;

        if (fabricGridRenderer != null)
            fabricGridRenderer.HidePixel(gridPos);

        opaqueRemaining--;
        if (opaqueRemaining < 0)
            opaqueRemaining = 0;

        if (opaqueRemaining == 0)
            OnFabricCleared?.Invoke();

        return true;
    }

    private int GetLaneCount(ApproachSide side)
    {
        return side == ApproachSide.Bottom || side == ApproachSide.Top
            ? gridWidth
            : gridHeight;
    }

    private void CacheFabricWorldBounds()
    {
        if (gridWidth < 1 || gridHeight < 1)
        {
            boundsMinX = boundsMaxX = boundsMinZ = boundsMaxZ = 0f;
            return;
        }

        Vector3 c00 = ResolveWorldPosition(new Vector2Int(0, 0));
        Vector3 c10 = ResolveWorldPosition(new Vector2Int(gridWidth - 1, 0));
        Vector3 c01 = ResolveWorldPosition(new Vector2Int(0, gridHeight - 1));
        Vector3 c11 = ResolveWorldPosition(new Vector2Int(gridWidth - 1, gridHeight - 1));

        boundsMinX = Mathf.Min(Mathf.Min(c00.x, c10.x), Mathf.Min(c01.x, c11.x));
        boundsMaxX = Mathf.Max(Mathf.Max(c00.x, c10.x), Mathf.Max(c01.x, c11.x));
        boundsMinZ = Mathf.Min(Mathf.Min(c00.z, c10.z), Mathf.Min(c01.z, c11.z));
        boundsMaxZ = Mathf.Max(Mathf.Max(c00.z, c10.z), Mathf.Max(c01.z, c11.z));
    }

    private ApproachSide ResolveApproachSide(float spoolX, float spoolZ)
    {
        float outBottom = boundsMinZ - spoolZ;
        float outTop = spoolZ - boundsMaxZ;
        float outLeft = boundsMinX - spoolX;
        float outRight = spoolX - boundsMaxX;

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

    private int ResolvePreferredLane(Vector3 spoolWorldPos, ApproachSide side)
    {
        ProjectSpoolToGrid(spoolWorldPos, out int col, out int row);
        return side == ApproachSide.Bottom || side == ApproachSide.Top ? col : row;
    }

    private void ProjectSpoolToGrid(Vector3 spoolWorldPos, out int col, out int row)
    {
        float spacing = fabricGridRenderer != null ? fabricGridRenderer.CellSpacing : 0.25f;
        if (spacing < 0.0001f)
            spacing = 0.25f;

        Vector3 anchor = fabricGridRenderer != null
            ? fabricGridRenderer.transform.position
            : Vector3.zero;
        Quaternion rot = fabricGridRenderer != null
            ? fabricGridRenderer.transform.rotation
            : Quaternion.identity;

        Vector3 local = Quaternion.Inverse(rot) * (spoolWorldPos - anchor);
        float halfW = (gridWidth - 1) * 0.5f;
        float halfH = (gridHeight - 1) * 0.5f;

        col = Mathf.Clamp(Mathf.RoundToInt(local.x / spacing + halfW), 0, Mathf.Max(0, gridWidth - 1));
        row = Mathf.Clamp(Mathf.RoundToInt(local.z / spacing + halfH), 0, Mathf.Max(0, gridHeight - 1));
    }

    /// <summary>
    /// Kenar + seritteki en dis opak piksel; id uyuyorsa aday, degilse ice deşilmez.
    /// </summary>
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
                if (paletteIds[flat] == EmptyPaletteId)
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
            if (paletteIds[flat] == EmptyPaletteId)
                continue;
            if (paletteIds[flat] != targetPaletteId)
                return false;

            gridPos = new Vector2Int(x, row);
            pixelWorldPos = worldPositions[flat];
            return true;
        }

        return false;
    }

    private void BuildSpoolColorLookup()
    {
        spoolColorCount = 0;
        spoolColorsByLevel = null;
        spoolLevels = null;

        if (projectSettings == null)
            return;

        IList<FacadeProperty> facades = projectSettings.GetOriginalFacadeProperties();
        if (facades == null || facades.Count == 0)
            return;

        int facadeCount = facades.Count;
        spoolColorsByLevel = new Color32[facadeCount];
        spoolLevels = new int[facadeCount];

        for (int i = 0; i < facadeCount; i++)
        {
            FacadeProperty facade = facades[i];
            if (facade == null || facade.Level <= 0)
                continue;

            Color c = facade.Color;
            spoolColorsByLevel[spoolColorCount] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
                255);
            spoolLevels[spoolColorCount] = facade.Level;
            spoolColorCount++;
        }
    }

    private Vector3 ResolveWorldPosition(Vector2Int gridPos)
    {
        if (fabricGridRenderer != null &&
            fabricGridRenderer.TryGetWorldPosition(gridPos, out Vector3 fromRenderer))
        {
            return fromRenderer;
        }

        float spacing = fabricGridRenderer != null ? fabricGridRenderer.CellSpacing : 0.25f;
        Vector3 anchor = fabricGridRenderer != null
            ? fabricGridRenderer.transform.position
            : Vector3.zero;
        Quaternion rot = fabricGridRenderer != null
            ? fabricGridRenderer.transform.rotation
            : Quaternion.identity;

        float halfW = (gridWidth - 1) * 0.5f;
        float halfH = (gridHeight - 1) * 0.5f;
        float localX = (gridPos.x - halfW) * spacing;
        float localZ = (gridPos.y - halfH) * spacing;
        return anchor + rot * new Vector3(localX, 0f, localZ);
    }

    private bool IsWithinRadiusSqr(Vector3 a, Vector3 b, float radiusSqr)
    {
        return SqrDistanceXZ(a, b) <= radiusSqr;
    }

    private float SqrDistanceXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private bool IsInsideGrid(Vector2Int gridPos)
    {
        return gridPos.x >= 0 && gridPos.y >= 0 &&
               gridPos.x < gridWidth && gridPos.y < gridHeight;
    }

    private int ToFlatIndex(int x, int y) => x + y * gridWidth;
}
