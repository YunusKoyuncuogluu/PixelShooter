using UnityEngine;

/// <summary>
/// Level grid world AABB'sine gore U seklinde Environment dolgusunu mevcut panel Transform'lariyla yeniden konumlar.
/// Dis sinirlar sabit world koordinati degil; grid kenarina gore padding ile uretilir.
/// Yeni GameObject uretmez; Left/Bottom/Right referanslarini kullanir.
/// </summary>
public class EnvironmentFrameBuilder : MonoBehaviour
{
    private const float MinPanelSize = 0.001f;
    private const float DefaultCellHalfExtent = 0.5f;

    [Header("Panels (mevcut cube Transform'lari)")]
    [SerializeField] private Transform leftPanel;
    [SerializeField] private Transform rightPanel;
    [SerializeField] private Transform bottomPanel;

    [Header("Padding (grid kenarina gore, world)")]
    [SerializeField] private float paddingLeft = 1.5f;
    [SerializeField] private float paddingRight = 1.5f;
    [SerializeField] private float paddingBottom = 1.5f;
    [SerializeField] private float paddingTop = 0f;

    [Header("Panel Visual")]
    [SerializeField] private float panelY = -0.3206f;
    [SerializeField] private float panelHeight = 0.35887f;
    [SerializeField] private float cellHalfExtent = DefaultCellHalfExtent;

    /// <summary>
    /// CellManager Construct sonrasi cagirilir. Grid world AABB + padding ile U cerceveyi yerlestirir.
    /// </summary>
    public void Rebuild(CellManager cellManager)
    {
        if (cellManager == null || cellManager.Cells == null)
        {
            Debug.LogWarning("[EnvironmentFrameBuilder] CellManager hazir degil.");
            return;
        }

        if (leftPanel == null || rightPanel == null || bottomPanel == null)
        {
            Debug.LogWarning("[EnvironmentFrameBuilder] Left/Right/Bottom panel referanslari atanmadi.");
            return;
        }

        if (!TryGetGridBounds(cellManager, out float gridLeft, out float gridRight, out float gridBottom, out float gridTop))
            return;

        float outerLeft = gridLeft - paddingLeft;
        float outerRight = gridRight + paddingRight;
        float outerBottom = gridBottom - paddingBottom;
        float outerTop = gridTop + paddingTop;

        PlaceLeftPanel(outerLeft, gridLeft, outerBottom, outerTop);
        PlaceRightPanel(gridRight, outerRight, outerBottom, outerTop);
        PlaceBottomPanel(gridLeft, gridRight, outerBottom, gridBottom);
    }

    private bool TryGetGridBounds(
        CellManager cellManager,
        out float gridLeft,
        out float gridRight,
        out float gridBottom,
        out float gridTop)
    {
        gridLeft = 0f;
        gridRight = 0f;
        gridBottom = 0f;
        gridTop = 0f;

        int width = cellManager.Width;
        int height = cellManager.Height;
        if (width <= 0 || height <= 0)
            return false;

        float half = cellHalfExtent > 0f ? cellHalfExtent : DefaultCellHalfExtent;

        Vector3 bottomLeft = cellManager.GetPosition(new Vector2Int(0, 0));
        Vector3 bottomRight = cellManager.GetPosition(new Vector2Int(width - 1, 0));
        Vector3 topLeft = cellManager.GetPosition(new Vector2Int(0, height - 1));

        gridLeft = bottomLeft.x - half;
        gridRight = bottomRight.x + half;
        gridBottom = bottomLeft.z - half;
        gridTop = topLeft.z + half;
        return true;
    }

    private void PlaceLeftPanel(float outerLeft, float gridLeft, float outerBottom, float outerTop)
    {
        float width = gridLeft - outerLeft;
        float depth = outerTop - outerBottom;
        if (width <= MinPanelSize || depth <= MinPanelSize)
            return;

        leftPanel.position = new Vector3((outerLeft + gridLeft) * 0.5f, panelY, (outerBottom + outerTop) * 0.5f);
        leftPanel.localScale = new Vector3(width, panelHeight, depth);
        leftPanel.rotation = Quaternion.identity;
    }

    private void PlaceRightPanel(float gridRight, float outerRight, float outerBottom, float outerTop)
    {
        float width = outerRight - gridRight;
        float depth = outerTop - outerBottom;
        if (width <= MinPanelSize || depth <= MinPanelSize)
            return;

        rightPanel.position = new Vector3((gridRight + outerRight) * 0.5f, panelY, (outerBottom + outerTop) * 0.5f);
        rightPanel.localScale = new Vector3(width, panelHeight, depth);
        rightPanel.rotation = Quaternion.identity;
    }

    private void PlaceBottomPanel(float gridLeft, float gridRight, float outerBottom, float gridBottom)
    {
        float width = gridRight - gridLeft;
        float depth = gridBottom - outerBottom;
        if (width <= MinPanelSize || depth <= MinPanelSize)
            return;

        bottomPanel.position = new Vector3((gridLeft + gridRight) * 0.5f, panelY, (outerBottom + gridBottom) * 0.5f);
        bottomPanel.localScale = new Vector3(width, panelHeight, depth);
        bottomPanel.rotation = Quaternion.identity;
    }
}
