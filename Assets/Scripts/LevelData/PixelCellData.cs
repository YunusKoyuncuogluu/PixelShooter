using System;
using UnityEngine;

namespace Grids
{
    /// <summary>
    /// Fabric piksel grid hucresi.
    /// Color32: GPU render. paletteId: edit-time bake edilmis Facade Level.
    /// </summary>
    [Serializable]
    public class PixelCellData
    {
        [SerializeField] private Color32 m_Color;
        [SerializeField] private short m_PaletteId = -1;

        public Color32 Color
        {
            get => m_Color;
            set => m_Color = value;
        }

        /// <summary> FacadeProperty.Level (GeneralColor). -1 = bos / atanmamis. </summary>
        public short PaletteId
        {
            get => m_PaletteId;
            set => m_PaletteId = value;
        }

        public bool HasPixel => m_Color.a >= PixelBakeUtility.AlphaThreshold;

        public void Clear()
        {
            m_Color = new Color32(0, 0, 0, 0);
            m_PaletteId = -1;
        }

        public void SetPixel(Color32 color, short paletteId)
        {
            m_Color = color;
            m_PaletteId = paletteId;
        }
    }
}
