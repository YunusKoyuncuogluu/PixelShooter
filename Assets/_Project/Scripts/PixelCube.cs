using PixelShooter.Pooling;
using UnityEngine;

public class PixelCube : MonoBehaviour, IPoolObject
{
    private readonly int ColorPropertyId = Shader.PropertyToID("_BaseColor");

    [Header("References")]
    [SerializeField] private MeshRenderer cubeRenderer;
    [SerializeField] private GameObject fakeShadow;

    [Header("Pixel")]
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private Color32 color;
    [SerializeField] private short paletteId;
    [SerializeField] private bool filled;

    private MaterialPropertyBlock propertyBlock;

    public Vector2Int GridPosition => gridPosition;
    public int GridX => gridPosition.x;
    public int GridY => gridPosition.y;
    public Color32 Color => color;
    public short PaletteId => paletteId;
    public bool Filled => filled;
    public bool HasRenderer => cubeRenderer != null;

    public void StoreCellAndPaint(Vector2Int position, Color32 pixelColor, short pixelPaletteId)
    {
        gridPosition = position;
        paletteId = pixelPaletteId;
        filled = true;

        PaintCube(pixelColor);
    }

    public void PaintCube(Color32 pixelColor)
    {
        color = pixelColor;

        if (cubeRenderer == null)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        propertyBlock.SetColor(ColorPropertyId, pixelColor);
        cubeRenderer.SetPropertyBlock(propertyBlock);
    }

    public void ActivateCube()
    {
        if (gameObject.activeSelf)
            return;

        gameObject.SetActive(true);
    }

    public void DeactivateCube()
    {
        filled = false;
        transform.localScale = Vector3.zero;
        SetFakeShadowVisible(false);

        if (!gameObject.activeSelf)
            return;

        gameObject.SetActive(false);
    }

    public void SwitchOutlineMaterial(bool outlineEnabled, Material outlineOnMaterial, Material outlineOffMaterial)
    {
        if (cubeRenderer == null)
            return;

        Material next = outlineEnabled ? outlineOnMaterial : outlineOffMaterial;

        if (next == null || cubeRenderer.sharedMaterial == next)
            return;

        cubeRenderer.sharedMaterial = next;
    }

    public void PlaceOnGrid(Vector3 localPosition)
    {
        Transform cubeTransform = transform;
        cubeTransform.localPosition = localPosition;
        cubeTransform.localRotation = Quaternion.identity;
    }

    public void SetCubeScale(Vector3 localScale)
    {
        transform.localScale = localScale;
    }

    public void ResetMeshTransform()
    {
        if (cubeRenderer == null)
            return;

        Transform visual = cubeRenderer.transform;
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;
    }

    public void Reset()
    {
        filled = false;
        gridPosition = Vector2Int.zero;
        paletteId = 0;
        color = default;
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        SetFakeShadowVisible(false);
    }

    public void SetFakeShadowVisible(bool visible)
    {
        if (fakeShadow == null || fakeShadow.activeSelf == visible)
            return;

        fakeShadow.SetActive(visible);
    }
}
