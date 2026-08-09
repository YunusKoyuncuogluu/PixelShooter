using System.Collections.Generic;
using Grids;
using UnityEngine;

/// <summary>
/// Texture -> opak PixelData listesi / PixelCellData[,] bake matematigi.
/// paletteId atamasi FacadePaletteMapper ile edit-time yapilir (runtime RGB yok).
///
/// Konvansiyon: grid y=0 = Unity GetPixels32 alti (texture bottom) = world -Z.
/// </summary>
public sealed class PixelBakeUtility
{
    /// <summary> Opak piksel esigi. PixelCellData.HasPixel ile paylasilir. </summary>
    public const byte AlphaThreshold = 10;

    public List<PixelData> BakeDownsampled(
        Color32[] rawPixels,
        int width,
        int height,
        Vector2Int resolution)
    {
        int gridWidth = resolution.x;
        int gridHeight = resolution.y;
        float stepX = (float)width / gridWidth;
        float stepY = (float)height / gridHeight;

        List<PixelData> bakedPixels = new List<PixelData>(gridWidth * gridHeight);

        for (int gy = 0; gy < gridHeight; gy++)
        {
            for (int gx = 0; gx < gridWidth; gx++)
            {
                int srcX = Mathf.FloorToInt(gx * stepX);
                int srcY = Mathf.FloorToInt(gy * stepY);
                if (srcX >= width) srcX = width - 1;
                if (srcY >= height) srcY = height - 1;

                Color32 pixelColor = rawPixels[srcY * width + srcX];
                if (pixelColor.a < AlphaThreshold)
                    continue;

                bakedPixels.Add(new PixelData
                {
                    position = new Vector2Int(gx, gy),
                    color = pixelColor,
                    paletteId = -1
                });
            }
        }

        return bakedPixels;
    }

    public List<PixelData> BakeFullResolution(Color32[] rawPixels, int width, int height)
    {
        int totalCount = rawPixels.Length;
        List<PixelData> bakedPixels = new List<PixelData>(totalCount);

        for (int index = 0; index < totalCount; index++)
        {
            Color32 pixelColor = rawPixels[index];
            if (pixelColor.a < AlphaThreshold)
                continue;

            int x = index % width;
            int y = index / width;
            bakedPixels.Add(new PixelData
            {
                position = new Vector2Int(x, y),
                color = pixelColor,
                paletteId = -1
            });
        }

        return bakedPixels;
    }

    /// <summary>
    /// Opak piksel listesini 2D PixelCellData grid'ine yazar (color + paletteId).
    /// </summary>
    public PixelCellData[,] CreatePixelGrid(List<PixelData> opaquePixels, Vector2Int size)
    {
        int w = Mathf.Max(1, size.x);
        int h = Mathf.Max(1, size.y);
        PixelCellData[,] grid = new PixelCellData[w, h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            grid[x, y] = new PixelCellData();

        if (opaquePixels == null)
            return grid;

        for (int i = 0; i < opaquePixels.Count; i++)
        {
            PixelData pixel = opaquePixels[i];
            int x = pixel.position.x;
            int y = pixel.position.y;
            if (x < 0 || y < 0 || x >= w || y >= h)
                continue;
            grid[x, y].SetPixel(pixel.color, pixel.paletteId);
        }

        return grid;
    }
}
