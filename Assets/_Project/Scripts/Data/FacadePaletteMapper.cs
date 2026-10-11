using System.Collections.Generic;
using UnityEngine;

public class FacadePaletteMapper
{
    private Color32[] colors;
    private short[] levels;
    private float[] hues;
    private float[] sats;
    private float[] vals;
    private int count;

    public int Count => count;

    public void BuildFromFacades(IList<FacadeProperty> facades)
    {
        if (facades == null || facades.Count == 0)
        {
            colors = System.Array.Empty<Color32>();
            levels = System.Array.Empty<short>();
            hues = System.Array.Empty<float>();
            sats = System.Array.Empty<float>();
            vals = System.Array.Empty<float>();
            count = 0;
            return;
        }

        int facadeCount = facades.Count;
        colors = new Color32[facadeCount];
        levels = new short[facadeCount];
        hues = new float[facadeCount];
        sats = new float[facadeCount];
        vals = new float[facadeCount];
        count = 0;

        for (int i = 0; i < facadeCount; i++)
        {
            FacadeProperty facade = facades[i];
            if (facade == null)
                continue;

            if (facade.Level <= 0)
                continue;

            Color c = facade.Color;
            Color32 c32 = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
                255);

            colors[count] = c32;
            levels[count] = (short)facade.Level;
            Color.RGBToHSV(c32, out hues[count], out sats[count], out vals[count]);
            count++;
        }
    }

    public short ResolveLevel(Color32 color)
    {
        if (count == 0)
            return -1;

        Color.RGBToHSV(
            new Color(color.r * (1f / 255f), color.g * (1f / 255f), color.b * (1f / 255f), 1f),
            out float h,
            out float s,
            out float v);

        float bestScore = float.MaxValue;
        short bestLevel = levels[0];

        bool useHue = s >= 0.18f;

        for (int i = 0; i < count; i++)
        {
            Color32 paletteColor = colors[i];
            int dr = color.r - paletteColor.r;
            int dg = color.g - paletteColor.g;
            int db = color.b - paletteColor.b;
            float rgbSqr = dr * dr + dg * dg + db * db;

            float score;
            if (!useHue || sats[i] < 0.12f)
            {
                score = rgbSqr;
            }
            else
            {
                float hueDeg = HueDistance01(h, hues[i]) * 360f;
                float ds = (s - sats[i]) * 255f;
                float dv = (v - vals[i]) * 255f;

                score = hueDeg * hueDeg * 18f + ds * ds * 0.4f + dv * dv * 0.55f + rgbSqr * 0.04f;
            }

            if (score >= bestScore)
                continue;

            bestScore = score;
            bestLevel = levels[i];
            if (rgbSqr == 0f)
                break;
        }

        return bestLevel;
    }

    public bool TryGetColor(short level, out Color32 color)
    {
        color = default;
        if (level <= 0 || colors == null)
            return false;

        for (int i = 0; i < count; i++)
        {
            if (levels[i] != level)
                continue;

            color = colors[i];
            return true;
        }

        return false;
    }

    public void AssignPaletteIds(List<PixelData> pixels)
    {
        if (pixels == null || count == 0)
            return;

        List<Color32> distinct = new List<Color32>(8);
        List<int> distinctCounts = new List<int>(8);
        for (int i = 0; i < pixels.Count; i++)
        {
            Color32 color = pixels[i].color;
            int slot = -1;
            for (int c = 0; c < distinct.Count; c++)
            {
                if (distinct[c].r != color.r || distinct[c].g != color.g || distinct[c].b != color.b)
                    continue;
                slot = c;
                break;
            }

            if (slot < 0)
            {
                distinct.Add(color);
                distinctCounts.Add(1);
            }
            else
            {
                distinctCounts[slot]++;
            }
        }

        for (int a = 0; a < distinct.Count - 1; a++)
        {
            for (int b = a + 1; b < distinct.Count; b++)
            {
                if (distinctCounts[b] <= distinctCounts[a])
                    continue;

                Color32 swapColor = distinct[a];
                distinct[a] = distinct[b];
                distinct[b] = swapColor;
                int swapCount = distinctCounts[a];
                distinctCounts[a] = distinctCounts[b];
                distinctCounts[b] = swapCount;
            }
        }

        bool[] used = new bool[count];
        short[] assigned = new short[distinct.Count];
        for (int c = 0; c < distinct.Count; c++)
            assigned[c] = ResolveUnusedLevel(distinct[c], used);

        for (int i = 0; i < pixels.Count; i++)
        {
            PixelData pixel = pixels[i];
            short level = assigned[0];
            for (int c = 0; c < distinct.Count; c++)
            {
                if (distinct[c].r != pixel.color.r || distinct[c].g != pixel.color.g || distinct[c].b != pixel.color.b)
                    continue;
                level = assigned[c];
                break;
            }

            pixel.paletteId = level;
            pixels[i] = pixel;
        }
    }

    private short ResolveUnusedLevel(Color32 color, bool[] used)
    {
        float bestScore = float.MaxValue;
        int bestIndex = -1;
        float reuseScore = float.MaxValue;
        int reuseIndex = 0;

        Color.RGBToHSV(
            new Color(color.r * (1f / 255f), color.g * (1f / 255f), color.b * (1f / 255f), 1f),
            out float h,
            out float s,
            out float v);
        bool useHue = s >= 0.18f;

        for (int i = 0; i < count; i++)
        {
            float score = ScoreColor(color, h, s, v, useHue, i);
            if (score < reuseScore)
            {
                reuseScore = score;
                reuseIndex = i;
            }

            if (used[i] || score >= bestScore)
                continue;

            bestScore = score;
            bestIndex = i;
        }

        int chosen = bestIndex >= 0 ? bestIndex : reuseIndex;
        used[chosen] = true;
        return levels[chosen];
    }

    private float ScoreColor(Color32 color, float h, float s, float v, bool useHue, int index)
    {
        Color32 paletteColor = colors[index];
        int dr = color.r - paletteColor.r;
        int dg = color.g - paletteColor.g;
        int db = color.b - paletteColor.b;
        float rgbSqr = dr * dr + dg * dg + db * db;

        if (!useHue || sats[index] < 0.12f)
            return rgbSqr;

        float hueDeg = HueDistance01(h, hues[index]) * 360f;
        float ds = (s - sats[index]) * 255f;
        float dv = (v - vals[index]) * 255f;
        return hueDeg * hueDeg * 18f + ds * ds * 0.4f + dv * dv * 0.55f + rgbSqr * 0.04f;
    }

    private float HueDistance01(float a, float b)
    {
        float d = Mathf.Abs(a - b);
        if (d > 0.5f)
            d = 1f - d;
        return d;
    }
}
