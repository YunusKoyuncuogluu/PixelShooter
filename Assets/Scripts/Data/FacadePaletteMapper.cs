using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Edit-time facade palette eslestirici (bake / editor only).
/// Yakin tonlari ayni Level'e yazar (hue agirlikli). Runtime claim bu sinifi kullanmaz.
/// </summary>
public sealed class FacadePaletteMapper
{
    private Color32[] colors;
    private short[] levels;
    private float[] hues;
    private float[] sats;
    private float[] vals;
    private int count;

    public int Count => count;

    /// <summary> ProjectSettings facade listesinden kurulum. </summary>
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

            // Level &lt;= 0 (None vb.) bake hedefine dahil edilmez.
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

    /// <summary>
    /// En uygun facade Level'ini dondurur. Eslesme yoksa -1.
    /// Doygun kirmizilar RGB-nearest ile Brown/Orange'a kacmasin diye hue agirlikli skor kullanir.
    /// </summary>
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

        // Dusuk doygunlukta (gri/beyaz) hue guvenilmez → RGB.
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
                // Hue baskin: (186,2,2) -> Red, Brown'a degil.
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

    /// <summary> Opak pixel listesine paletteId yazar (edit-time). </summary>
    public void AssignPaletteIds(List<PixelData> pixels)
    {
        if (pixels == null)
            return;

        for (int i = 0; i < pixels.Count; i++)
        {
            PixelData pixel = pixels[i];
            pixel.paletteId = ResolveLevel(pixel.color);
            pixels[i] = pixel;
        }
    }

    /// <summary> Unity hue 0..1 dairesel mesafe → 0..0.5. </summary>
    private float HueDistance01(float a, float b)
    {
        float d = Mathf.Abs(a - b);
        if (d > 0.5f)
            d = 1f - d;
        return d;
    }
}
