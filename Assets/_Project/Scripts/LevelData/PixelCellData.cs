using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Grids
{

    [Serializable]
    public class PixelCellData
    {
        [FormerlySerializedAs("m_Color")]
        [SerializeField] private Color32 color;
        [FormerlySerializedAs("m_PaletteId")]
        [SerializeField] private short paletteId = -1;

        public Color32 Color
        {
            get => this.color;
            set => this.color = value;
        }

        public short PaletteId
        {
            get => this.paletteId;
            set => this.paletteId = value;
        }

        public bool HasPixel => this.color.a >= PixelBakeUtility.AlphaThreshold;

        public void Clear()
        {
            this.color = new Color32(0, 0, 0, 0);
            this.paletteId = -1;
        }

        public void SetPixel(Color32 color, short paletteId)
        {
            this.color = color;
            this.paletteId = paletteId;
        }
    }
}
