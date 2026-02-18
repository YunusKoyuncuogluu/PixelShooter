using UnityEngine;
using Zenject;

public class StitchNode : MonoBehaviour
{
    private Vector2Int m_GridPosition;
    private GeneralColor m_Color;
    private bool m_IsActive = true;

    public Vector2Int GridPosition => m_GridPosition;
    public int Column => m_GridPosition.x;
    public int Row => m_GridPosition.y;
    public GeneralColor Color => m_Color;
    public bool IsActive => m_IsActive;

    public void Construct(Vector3 position, Transform parent, GeneralColor color, Vector2Int gridPosition)
    {
        m_Color = color;
        m_GridPosition = gridPosition;
        m_IsActive = color != GeneralColor.None;

        transform.SetParent(parent);
        transform.position = position;
        gameObject.SetActive(m_IsActive);
    }

    /// <summary>
    /// Stitch rengini cube/renderer üzerinde gösterir. FabricManager spawn sırasında facade rengi ile çağırır.
    /// </summary>
    public void SetVisualColor(UnityEngine.Color color)
    {
        var renderer = GetComponentInChildren<Renderer>(true);
        if (renderer != null && renderer.material != null)
            renderer.material.color = color;
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
