using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eski ayri pixel SO. Yeni akista piksel verisi LevelData.Fabric Pixel foldout'unda tutulur.
/// Import icin LevelData > Fabric Pixel > Import LevelPixelData Asset kullanin.
/// </summary>
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

    /// <summary>
    /// Bake sonrasi listeyi kapasite ile sifirdan kurar; gereksiz heap buyumesini onler.
    /// </summary>
    public void SetPixels(List<PixelData> bakedPixels, Vector2Int size)
    {
        textureSize = size;
        pixels = bakedPixels ?? new List<PixelData>();
    }
}
