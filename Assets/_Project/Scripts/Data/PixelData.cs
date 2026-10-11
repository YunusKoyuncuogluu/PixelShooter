using System;
using UnityEngine;

[Serializable]
public struct PixelData
{
    [SerializeField] public Vector2Int position;
    [SerializeField] public Color32 color;

    [SerializeField] public short paletteId;
}
