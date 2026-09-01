using System.Collections.Generic;
using LevelManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zenject;

/// <summary>
/// LevelData.Fabric Pixel grid'ini GPU Instancing ile cizen hibrit render katmani.
///
/// Batching (OpenGLES3): DrawMeshInstanced cagri basina max 1023 instance.
/// Init'te opak pikseller batch'lenir; Tick/Update 0 GC.
///
/// gridToInstanceMap: unravel fazinda piksel gizleme / GameObject spawn icin.
/// Grid XZ duzlemine yerlestirilir (oyun hucreleriyle ayni).
/// </summary>
public class FabricGridRenderer : MonoBehaviour, IInitializable, ITickable
{
    public const int MaxInstancesPerBatch = 1023;

    // Custom/URP_InstancedToon: per-instance renk UNITY_INSTANCING_BUFFER icindeki _BaseColor
    private readonly int colorPropertyId = Shader.PropertyToID("_BaseColor");

    [Header("Render Assets")]
    [SerializeField] private Mesh instanceMesh;
    [SerializeField] private Material instanceMaterial;

    [Header("Grid Layout")]
    [SerializeField] private float cellSpacing = 0.25f;
    [SerializeField] private float cellScale = 0.22f;

    public float CellSpacing => cellSpacing;

    private InstanceBatch[] batches;
    private int batchCount;
    private bool isReady;
    private bool tickFromZenject;

    /// <summary> Init-only scratch buffer; Tick'te kullanilmaz. </summary>
    private readonly List<PixelData> opaqueScratch = new List<PixelData>(1024);

    private Dictionary<Vector2Int, (int batchIndex, int arrayIndex)> gridToInstanceMap;

    public void Initialize()
    {
        tickFromZenject = true;
        EnsureRenderAssets();
    }

    public void Tick()
    {
        RenderBatches();
    }

    private void Update()
    {
        if (!tickFromZenject)
            RenderBatches();
    }

    /// <summary>
    /// Aktif LevelData'nin Fabric Pixel grid'inden batch kurar.
    /// </summary>
    public void BuildBatches(LevelData levelData)
    {
        isReady = false;
        batches = null;
        batchCount = 0;
        gridToInstanceMap = null;

        EnsureRenderAssets();

        if (levelData == null)
        {
            Debug.LogWarning("[FabricGridRenderer] LevelData null.");
            return;
        }

        opaqueScratch.Clear();
        if (opaqueScratch.Capacity < 256)
            opaqueScratch.Capacity = 256;

        levelData.CopyOpaquePixelsTo(opaqueScratch);
        BuildFromOpaqueList(opaqueScratch, levelData.PixelSize);
    }

    private void BuildFromOpaqueList(IList<PixelData> pixels, Vector2Int textureSize)
    {
        if (pixels == null || pixels.Count == 0)
        {
            Debug.LogWarning("[FabricGridRenderer] Opak piksel yok; render atlandi.");
            return;
        }

        if (instanceMesh == null || instanceMaterial == null)
        {
            Debug.LogError("[FabricGridRenderer] Mesh veya Material atanamadi.");
            return;
        }

        if (!instanceMaterial.enableInstancing)
            instanceMaterial.enableInstancing = true;

        int pixelCount = pixels.Count;
        if (textureSize.x < 1) textureSize.x = 1;
        if (textureSize.y < 1) textureSize.y = 1;

        batchCount = (pixelCount + MaxInstancesPerBatch - 1) / MaxInstancesPerBatch;
        batches = new InstanceBatch[batchCount];
        gridToInstanceMap = new Dictionary<Vector2Int, (int batchIndex, int arrayIndex)>(pixelCount);

        float halfW = (textureSize.x - 1) * 0.5f;
        float halfH = (textureSize.y - 1) * 0.5f;
        Vector3 anchorPosition = transform.position;
        Quaternion anchorRotation = transform.rotation;
        Vector3 scale = new Vector3(cellScale, cellScale, cellScale);

        int pixelIndex = 0;
        for (int batchIndex = 0; batchIndex < batchCount; batchIndex++)
        {
            int remaining = pixelCount - pixelIndex;
            int countInBatch = remaining < MaxInstancesPerBatch ? remaining : MaxInstancesPerBatch;

            Matrix4x4[] matrices = new Matrix4x4[countInBatch];
            Vector4[] colors = new Vector4[countInBatch];
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

            for (int arrayIndex = 0; arrayIndex < countInBatch; arrayIndex++)
            {
                PixelData pixel = pixels[pixelIndex];
                Vector2Int gridPos = pixel.position;

                float localX = (gridPos.x - halfW) * cellSpacing;
                // y=0 = texture alti -> -Z (ekran alti, CellPositioner ile ayni).
                float localZ = (gridPos.y - halfH) * cellSpacing;
                Vector3 worldPos = anchorPosition + anchorRotation * new Vector3(localX, 0f, localZ);

                matrices[arrayIndex] = Matrix4x4.TRS(worldPos, anchorRotation, scale);
                colors[arrayIndex] = Color32ToVector4(pixel.color);

                gridToInstanceMap[gridPos] = (batchIndex, arrayIndex);
                pixelIndex++;
            }

            propertyBlock.SetVectorArray(colorPropertyId, colors);

            batches[batchIndex] = new InstanceBatch
            {
                matrices = matrices,
                colors = colors,
                propertyBlock = propertyBlock,
                count = countInBatch
            };
        }

        isReady = true;
    }

    private bool TryGetInstanceLocation(Vector2Int gridPosition, out int batchIndex, out int arrayIndex)
    {
        batchIndex = 0;
        arrayIndex = 0;

        if (gridToInstanceMap == null)
            return false;

        if (!gridToInstanceMap.TryGetValue(gridPosition, out (int batchIndex, int arrayIndex) location))
            return false;

        batchIndex = location.batchIndex;
        arrayIndex = location.arrayIndex;
        return true;
    }

    /// <summary>
    /// GPU instance world pozisyonu (matrix translation). Tuketim oncesi kullan.
    /// </summary>
    public bool TryGetWorldPosition(Vector2Int gridPosition, out Vector3 worldPosition)
    {
        worldPosition = default;
        if (!TryGetInstanceLocation(gridPosition, out int batchIndex, out int arrayIndex))
            return false;

        Matrix4x4[] matrices = batches[batchIndex].matrices;
        worldPosition = matrices[arrayIndex].GetColumn(3);
        return true;
    }

    /// <summary>
    /// Pikseli GPU batch'ten gizler (scale 0 + alpha 0). Hot-path allocation yok.
    /// </summary>
    public bool HidePixel(Vector2Int gridPosition)
    {
        if (!TryGetInstanceLocation(gridPosition, out int batchIndex, out int arrayIndex))
            return false;

        InstanceBatch batch = batches[batchIndex];
        Vector3 pos = batch.matrices[arrayIndex].GetColumn(3);
        batch.matrices[arrayIndex] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.zero);

        Vector4 color = batch.colors[arrayIndex];
        color.w = 0f;
        batch.colors[arrayIndex] = color;
        batch.propertyBlock.SetVectorArray(colorPropertyId, batch.colors);
        return true;
    }

    private void RenderBatches()
    {
        if (!isReady || instanceMesh == null || instanceMaterial == null)
            return;

        for (int i = 0; i < batchCount; i++)
        {
            InstanceBatch batch = batches[i];
            if (batch.count <= 0)
                continue;

            Graphics.DrawMeshInstanced(
                instanceMesh,
                0,
                instanceMaterial,
                batch.matrices,
                batch.count,
                batch.propertyBlock,
                ShadowCastingMode.Off,
                false);
        }
    }

    private void EnsureRenderAssets()
    {
        if (instanceMesh == null)
            instanceMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        if (instanceMaterial == null)
        {
            Shader shader = Shader.Find("Custom/URP_InstancedToon");
            if (shader == null)
                shader = Shader.Find("FabricInstancedToon");

            if (shader != null)
            {
                instanceMaterial = new Material(shader);
                instanceMaterial.enableInstancing = true;
            }
        }
    }

    private Vector4 Color32ToVector4(Color32 color)
    {
        const float inv = 1f / 255f;
        return new Vector4(color.r * inv, color.g * inv, color.b * inv, color.a * inv);
    }

    private struct InstanceBatch
    {
        public Matrix4x4[] matrices;
        public Vector4[] colors;
        public MaterialPropertyBlock propertyBlock;
        public int count;
    }
}
