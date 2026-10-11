using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelPixelData", menuName = "Pixel Flow/Level Pixel Data")]
public class LevelPixelData : ScriptableObject
{
    [SerializeField] private Vector2Int textureSize;
    [SerializeField] private List<PixelData> pixels = new List<PixelData>();

    public Vector2Int TextureSize
    {
        get => textureSize;
        set => textureSize = value;
    }

    public List<PixelData> Pixels => pixels;

    public void SetPixels(List<PixelData> bakedPixels, Vector2Int size)
    {
        textureSize = size;
        pixels = bakedPixels ?? new List<PixelData>();
    }
}
