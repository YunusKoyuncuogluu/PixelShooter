using System;
using UnityEngine;

/// <summary>
/// Tek piksel: render icin Color32 + edit-time bake edilmis paletteId (Facade Level).
/// Runtime'da RGB eslestirme yapilmaz; sadece paletteId kullanilir.
/// </summary>
[Serializable]
public struct PixelData
{
    [SerializeField] public Vector2Int position;
    [SerializeField] public Color32 color;

    /// <summary> FacadeProperty.Level / GeneralColor int (edit-time bake). Bos = -1. </summary>
    [SerializeField] public short paletteId;
}
