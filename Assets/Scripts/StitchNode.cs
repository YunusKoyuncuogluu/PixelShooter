using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class StitchNode : MonoBehaviour
{
    [Tooltip("Inspector'dan sürükle bırak: Parça GameObjects. Sırayla sökülür (Knit Out tarzı).")]
    [SerializeField] private List<GameObject> m_Pieces = new List<GameObject>();

    [Tooltip("Dizilim referansı: Bu objenin scale'i stitch aralığı olarak kullanılır. Arada boşluk kalmaz.")]
    [SerializeField] private Transform m_CellSizeReference;

    /// <summary> Referans objenin scale'i - hücre boyutu. Null ise (1,1,1). </summary>
    public Vector3 CellSize => m_CellSizeReference != null ? m_CellSizeReference.localScale : Vector3.one;

    private Vector2Int m_GridPosition;
    private GeneralColor m_Color;
    private bool m_IsActive = true;
    private int m_TotalPieces = 1;
    private int m_RemainingPieces = 1;

    public Vector2Int GridPosition => m_GridPosition;
    public int Column => m_GridPosition.x;
    public int Row => m_GridPosition.y;
    public GeneralColor Color => m_Color;
    public bool IsActive => m_IsActive;
    public int TotalPieces => m_TotalPieces;
    public int RemainingPieces => m_RemainingPieces;

    public void Construct(Vector3 position, Transform parent, GeneralColor color, Vector2Int gridPosition)
    {
        m_Color = color;
        m_GridPosition = gridPosition;
        m_IsActive = color != GeneralColor.None;

        transform.SetParent(parent);
        transform.position = position;
        gameObject.SetActive(m_IsActive);

        m_TotalPieces = (m_Pieces != null && m_Pieces.Count > 0) ? m_Pieces.Count : 1;
        m_RemainingPieces = m_TotalPieces;

        for (int i = 0; m_Pieces != null && i < m_Pieces.Count; i++)
        {
            if (m_Pieces[i] != null)
                m_Pieces[i].SetActive(true);
        }
    }

    /// <summary>
    /// Bir parça tüketir. Sırayla en alttan (index 0) başlar.
    /// </summary>
    /// <returns>Tüketilecek parça vardı ve tüketildiyse true.</returns>
    /// <summary>
    /// Şu an sökülen parçanın dünya pozisyonu. İpin uç noktası burada olmalı.
    /// </summary>
    public Vector3 GetCurrentPiecePosition()
    {
        if (m_Pieces == null || m_Pieces.Count == 0)
            return transform.position;
        int index = m_TotalPieces - m_RemainingPieces;
        if (index < 0) index = 0;
        if (index >= m_Pieces.Count) index = m_Pieces.Count - 1;
        if (m_Pieces[index] != null)
            return m_Pieces[index].transform.position;
        return transform.position;
    }

    public bool ConsumeOnePiece()
    {
        if (m_RemainingPieces <= 0)
            return false;

        int indexToConsume = m_TotalPieces - m_RemainingPieces;
        if (m_Pieces != null && indexToConsume < m_Pieces.Count && m_Pieces[indexToConsume] != null)
            m_Pieces[indexToConsume].SetActive(false);
        else if (m_Pieces == null || m_Pieces.Count == 0)
        {
            var rend = GetComponentInChildren<Renderer>(true);
            if (rend != null) rend.gameObject.SetActive(false);
        }

        m_RemainingPieces--;
        if (m_RemainingPieces <= 0)
            m_IsActive = false;

        return true;
    }

    /// <summary>
    /// Stitch rengini cube/renderer üzerinde gösterir. FabricManager spawn sırasında facade rengi ile çağırır.
    /// Parça visual'lar varsa onlara da uygular.
    /// </summary>
    public void SetVisualColor(UnityEngine.Color color)
    {
        if (m_Pieces != null)
        {
            foreach (var piece in m_Pieces)
            {
                if (piece == null) continue;
                var r = piece.GetComponent<Renderer>();
                if (r != null && r.material != null) r.material.color = color;
            }
        }
        var mainRenderer = GetComponentInChildren<Renderer>(true);
        if (mainRenderer != null && mainRenderer.material != null)
            mainRenderer.material.color = color;
    }

    public void SetActive(bool active)
    {
        m_IsActive = active;
        gameObject.SetActive(active);
    }

    public class Factory : PlaceholderFactory<StitchNode>
    {
    }
}
