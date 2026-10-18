using System.Collections.Generic;
using Grids;
using UnityEngine;

public class PixelBakeUtility
{

    public const byte AlphaThreshold = 10;

    public List<PixelData> BakeDownsampled(
        Color32[] rawPixels,
        int width,
        int height,
        Vector2Int resolution)
    {
        return BakeDownsampled(rawPixels, width, height, resolution, null, 0);
    }

    public List<PixelData> BakeDownsampled(
        Color32[] rawPixels,
        int width,
        int height,
        Vector2Int resolution,
        Color32[] ignoredColors,
        int ignoreDistanceSqr)
    {
        int gridWidth = resolution.x;
        int gridHeight = resolution.y;
        int originX = 0;
        int originY = 0;
        int sampleWidth = width;
        int sampleHeight = height;

        if (ignoredColors != null && ignoredColors.Length > 0)
        {
            if (!TryGetKeptBounds(
                    rawPixels,
                    width,
                    height,
                    ignoredColors,
                    ignoreDistanceSqr,
                    out int minX,
                    out int minY,
                    out int maxX,
                    out int maxY))
                return new List<PixelData>(0);

            originX = minX;
            originY = minY;

            sampleWidth = maxX - minX + 1;
            sampleHeight = maxY - minY + 1;
        }

        float stepX = (float)sampleWidth / gridWidth;
        float stepY = (float)sampleHeight / gridHeight;

        List<PixelData> bakedPixels = new List<PixelData>(gridWidth * gridHeight);
        int[] bucketKeys = new int[48];
        int[] bucketCounts = new int[48];
        int[] bucketRed = new int[48];
        int[] bucketGreen = new int[48];
        int[] bucketBlue = new int[48];

        for (int gy = 0; gy < gridHeight; gy++)
        {
            for (int gx = 0; gx < gridWidth; gx++)
            {
                int x0 = originX + Mathf.FloorToInt(gx * stepX);
                int x1 = originX + Mathf.FloorToInt((gx + 1) * stepX);
                int y0 = originY + Mathf.FloorToInt(gy * stepY);
                int y1 = originY + Mathf.FloorToInt((gy + 1) * stepY);

                if (x1 <= x0)
                    x1 = x0 + 1;

                if (y1 <= y0)
                    y1 = y0 + 1;

                if (x0 < 0)
                    x0 = 0;

                if (y0 < 0)
                    y0 = 0;

                if (x1 > width)
                    x1 = width;

                if (y1 > height)
                    y1 = height;

                bool filterIgnored = ignoredColors != null && ignoredColors.Length > 0;
                int sampled = 0;
                int kept = 0;
                int bucketCount = 0;

                for (int sy = y0; sy < y1; sy++)
                {
                    int row = sy * width;
                    for (int sx = x0; sx < x1; sx++)
                    {
                        Color32 sample = rawPixels[row + sx];
                        if (filterIgnored)
                            sampled++;
                        if (IsDropped(sample, ignoredColors, ignoreDistanceSqr))
                            continue;

                        if (filterIgnored)
                            kept++;

                        int key = ((sample.r >> 3) << 10) | ((sample.g >> 3) << 5) | (sample.b >> 3);
                        int slot = -1;

                        for (int i = 0; i < bucketCount; i++)
                        {
                            if (bucketKeys[i] != key)
                                continue;
                            slot = i;

                            break;
                        }

                        if (slot < 0)
                        {

                            if (bucketCount >= bucketKeys.Length)
                                continue;

                            slot = bucketCount;
                            bucketKeys[slot] = key;
                            bucketCounts[slot] = 0;
                            bucketRed[slot] = 0;
                            bucketGreen[slot] = 0;
                            bucketBlue[slot] = 0;
                            bucketCount++;

                        }

                        bucketCounts[slot]++;
                        bucketRed[slot] += sample.r;
                        bucketGreen[slot] += sample.g;
                        bucketBlue[slot] += sample.b;

                    }
                }

                if (bucketCount == 0)
                    continue;

                if (filterIgnored && kept * 2 <= sampled)
                    continue;

                int best = 0;

                for (int i = 1; i < bucketCount; i++)
                {
                    if (bucketCounts[i] > bucketCounts[best])
                        best = i;
                }

                int votes = bucketCounts[best];

                Color32 winner = new Color32(
                    (byte)(bucketRed[best] / votes),
                    (byte)(bucketGreen[best] / votes),
                    (byte)(bucketBlue[best] / votes),
                    255);

                if (filterIgnored && IsDropped(winner, ignoredColors, ignoreDistanceSqr))
                    continue;

                bakedPixels.Add(new PixelData
                {
                    position = new Vector2Int(gx, gy),
                    color = winner,
                    paletteId = -1
                });
            }
        }

        return bakedPixels;
    }

    public void CollapseToDominantColors(List<PixelData> pixels)
    {
        if (pixels == null || pixels.Count == 0)
            return;

        const int mergeDistanceSqr = 55 * 55;
        List<Color32> colors = new List<Color32>(16);
        List<int> counts = new List<int>(16);
        List<int> redSums = new List<int>(16);
        List<int> greenSums = new List<int>(16);
        List<int> blueSums = new List<int>(16);

        for (int i = 0; i < pixels.Count; i++)
        {
            Color32 color = pixels[i].color;
            int slot = -1;
            int bestDistance = mergeDistanceSqr;

            for (int c = 0; c < colors.Count; c++)
            {
                int distance = ColorDistanceSqr(color, colors[c]);

                if (distance > bestDistance)
                    continue;

                bestDistance = distance;
                slot = c;
            }

            if (slot < 0)
            {
                colors.Add(color);
                counts.Add(1);
                redSums.Add(color.r);
                greenSums.Add(color.g);
                blueSums.Add(color.b);
                continue;
            }

            counts[slot]++;
            redSums[slot] += color.r;
            greenSums[slot] += color.g;
            blueSums[slot] += color.b;
        }

        for (int c = 0; c < colors.Count; c++)
        {
            int votes = counts[c];

            colors[c] = new Color32(
                (byte)(redSums[c] / votes),
                (byte)(greenSums[c] / votes),
                (byte)(blueSums[c] / votes),
                255);
        }

        for (int a = 0; a < colors.Count; a++)
        {
            if (counts[a] <= 0)
                continue;

            for (int b = colors.Count - 1; b > a; b--)
            {
                if (counts[b] <= 0)
                    continue;

                if (ColorDistanceSqr(colors[a], colors[b]) > mergeDistanceSqr)
                    continue;

                int total = counts[a] + counts[b];
                int red = (colors[a].r * counts[a] + colors[b].r * counts[b]) / total;
                int green = (colors[a].g * counts[a] + colors[b].g * counts[b]) / total;
                int blue = (colors[a].b * counts[a] + colors[b].b * counts[b]) / total;

                colors[a] = new Color32((byte)red, (byte)green, (byte)blue, 255);
                counts[a] = total;
                counts[b] = 0;
            }
        }

        for (int c = colors.Count - 1; c >= 0; c--)
        {
            if (counts[c] > 0)
                continue;

            colors.RemoveAt(c);
        }

        for (int i = 0; i < pixels.Count; i++)
        {
            PixelData pixel = pixels[i];
            int best = 0;
            int bestDistance = int.MaxValue;

            for (int c = 0; c < colors.Count; c++)
            {
                int distance = ColorDistanceSqr(pixel.color, colors[c]);

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = c;
            }

            pixel.color = colors[best];
            pixels[i] = pixel;
        }
    }

    private bool TryGetKeptBounds(
        Color32[] rawPixels,
        int width,
        int height,
        Color32[] ignoredColors,
        int ignoreDistanceSqr,
        out int minX,
        out int minY,
        out int maxX,
        out int maxY)
    {
        minX = width;
        minY = height;
        maxX = -1;
        maxY = -1;

        int tileX = Mathf.Max(2, width / 48);
        int tileY = Mathf.Max(2, height / 48);

        for (int y0 = 0; y0 < height; y0 += tileY)
        {
            int y1 = y0 + tileY;

            if (y1 > height)
                y1 = height;

            for (int x0 = 0; x0 < width; x0 += tileX)
            {
                int x1 = x0 + tileX;

                if (x1 > width)
                    x1 = width;

                int total = 0;
                int kept = 0;

                for (int y = y0; y < y1; y++)
                {
                    int row = y * width;

                    for (int x = x0; x < x1; x++)
                    {
                        total++;

                        if (!IsDropped(rawPixels[row + x], ignoredColors, ignoreDistanceSqr))
                            kept++;
                    }
                }

                if (kept * 2 <= total)
                    continue;

                if (x0 < minX)
                    minX = x0;

                if (y0 < minY)
                    minY = y0;

                if (x1 - 1 > maxX)
                    maxX = x1 - 1;

                if (y1 - 1 > maxY)
                    maxY = y1 - 1;
            }
        }

        return maxX >= minX && maxY >= minY;
    }

    private bool IsDropped(Color32 sample, Color32[] ignoredColors, int ignoreDistanceSqr)
    {
        if (sample.a < AlphaThreshold)
            return true;

        if (ignoredColors == null || ignoredColors.Length == 0)
            return false;

        for (int i = 0; i < ignoredColors.Length; i++)
        {
            if (ColorDistanceSqr(sample, ignoredColors[i]) <= ignoreDistanceSqr)
                return true;
        }

        return false;
    }

    private int ColorDistanceSqr(Color32 a, Color32 b)
    {
        int dr = a.r - b.r;
        int dg = a.g - b.g;
        int db = a.b - b.b;

        return dr * dr + dg * dg + db * db;
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

    public PixelCellData[,] CreatePixelGrid(List<PixelData> opaquePixels, Vector2Int size)
    {
        int w = Mathf.Max(1, size.x);
        int h = Mathf.Max(1, size.y);

        PixelCellData[,] grid = new PixelCellData[w, h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
                grid[x, y] = new PixelCellData();
        }

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
