using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LevelManagement;
using PixelShooter.Pooling;
using UnityEngine;
using Zenject;

public class ImageGridRenderer : MonoBehaviour
{
    private const int MaxConcurrentPops = 64;
    private const int MaxPaletteLevel = 64;
    private const float PopPunchDuration = 0.08f;
    private const float PopCollapseDuration = 0.12f;
    private const float PopHeightScale = 1.2f;
    private const string OutlinePassName = "Outline";

    private readonly Vector2Int[] ExposedNeighbors =
    {
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(0, 1),
        new Vector2Int(1, -1)
    };

    [Header("Render Assets")]
    [SerializeField] private Material instanceMaterial;

    [Header("Grid Layout")]
    [SerializeField] private float cellSpacing = 0.25f;
    [SerializeField] private float cellScale = 0.22f;

    public float CellSpacing => cellSpacing;

    private Material outlineOffMaterial;
    private ObjectPool objectPool;
    private bool isReady;
    private int layoutVersion;
    private int visibleCount;
    private int poppingCount;
    private readonly int[] poppingCubes = new int[MaxConcurrentPops];
    private readonly List<PixelData> spawnBuffer = new List<PixelData>(1024);
    private readonly List<PixelCube> activeCubes = new List<PixelCube>(1024);
    private readonly Color32[] tintByLevel = new Color32[MaxPaletteLevel + 1];
    private readonly bool[] tintLevelReady = new bool[MaxPaletteLevel + 1];
    private Dictionary<Vector2Int, int> cellToCube;

    [Inject]
    public void Construct(ObjectPool pool)
    {
        objectPool = pool;
    }

    public void BuildGridFromLevelData(LevelData levelData)
    {
        layoutVersion++;
        poppingCount = 0;
        isReady = false;
        visibleCount = 0;
        ReturnCubesToPool();

        if (cellToCube != null)
            cellToCube.Clear();

        if (levelData == null || objectPool == null)
            return;

        spawnBuffer.Clear();
        levelData.CopyOpaquePixelsTo(spawnBuffer);
        PlaceCubesOnGrid(spawnBuffer, levelData.PixelSize);
    }

    public bool TryGetCubeWorldPosition(Vector2Int gridPosition, out Vector3 worldPosition)
    {
        worldPosition = default;

        if (!TryGetCubeIndexAtCell(gridPosition, out int cubeIndex))
            return false;

        worldPosition = activeCubes[cubeIndex].transform.position;
        return true;
    }

    public bool PlayHideCube(Vector2Int gridPosition)
    {
        if (!TryGetCubeIndexAtCell(gridPosition, out int cubeIndex))
            return false;

        if (IsCubePopAnimationPlaying(cubeIndex))
            return true;

        PixelCube cube = activeCubes[cubeIndex];

        if (cube == null || !cube.Filled || !cube.HasRenderer)
            return true;

        if (poppingCount >= MaxConcurrentPops)
        {
            HideCubeAndRefreshNeighbors(cube);
            return true;
        }

        poppingCubes[poppingCount] = cubeIndex;
        poppingCount++;
        PlayPopThenHideCube(cubeIndex).Forget();
        return true;
    }

    public void HideRemainingCubes()
    {
        layoutVersion++;
        int count = visibleCount < activeCubes.Count ? visibleCount : activeCubes.Count;

        for (int i = 0; i < count; i++)
        {
            PixelCube cube = activeCubes[i];

            if (cube == null || !cube.Filled || !cube.HasRenderer)
                continue;

            HideCubeAndRefreshNeighbors(cube);
        }

        poppingCount = 0;
    }

    public void PaintCubesByPaletteLevel(int[] levels, Color32[] paletteColors, int paletteCount)
    {
        if (!isReady || levels == null || paletteColors == null || paletteCount <= 0)
            return;

        int maxLevel = 0;

        for (int i = 0; i < paletteCount; i++)
        {
            if (levels[i] > maxLevel)
                maxLevel = levels[i];
        }

        if (maxLevel <= 0 || maxLevel > MaxPaletteLevel)
            return;

        for (int i = 0; i <= maxLevel; i++)
            tintLevelReady[i] = false;

        for (int i = 0; i < paletteCount; i++)
        {
            int level = levels[i];

            if (level <= 0 || level > maxLevel)
                continue;

            tintByLevel[level] = paletteColors[i];
            tintLevelReady[level] = true;
        }

        int count = visibleCount < activeCubes.Count ? visibleCount : activeCubes.Count;

        for (int i = 0; i < count; i++)
        {
            PixelCube cube = activeCubes[i];

            if (cube == null)
                continue;

            int level = cube.PaletteId;

            if (level <= 0 || level > maxLevel || !tintLevelReady[level])
                continue;

            cube.PaintCube(tintByLevel[level]);
        }
    }

    private void PlaceCubesOnGrid(IList<PixelData> pixels, Vector2Int textureSize)
    {
        if (pixels == null || pixels.Count == 0)
            return;

        int pixelCount = pixels.Count;

        if (textureSize.x < 1)
            textureSize.x = 1;

        if (textureSize.y < 1)
            textureSize.y = 1;

        float halfWidth = (textureSize.x - 1) * 0.5f;
        float halfHeight = (textureSize.y - 1) * 0.5f;
        Vector3 scale = new Vector3(cellScale, cellScale, cellScale);

        if (cellToCube == null)
            cellToCube = new Dictionary<Vector2Int, int>(pixelCount);

        CreateMaterialWithOutlineDisabled();

        for (int i = 0; i < pixelCount; i++)
        {
            PixelData pixel = pixels[i];
            PixelCube cube = TakeCubeFromPool();

            if (cube == null || !cube.HasRenderer)
            {
                if (cube != null)
                {
                    cube.DeactivateCube();
                    objectPool.DestroyToPool(PoolTags.PixelCube, cube.gameObject, false);
                }

                continue;
            }

            float localX = (pixel.position.x - halfWidth) * cellSpacing;
            float localZ = (pixel.position.y - halfHeight) * cellSpacing;
            cube.PlaceOnGrid(new Vector3(localX, 0f, localZ));
            cube.SetCubeScale(scale);
            cube.ActivateCube();
            cube.StoreCellAndPaint(pixel.position, pixel.color, pixel.paletteId);
            cellToCube[pixel.position] = activeCubes.Count;
            activeCubes.Add(cube);
        }

        for (int i = 0; i < activeCubes.Count; i++)
            UpdateCubeOutlineAndShadow(activeCubes[i]);

        visibleCount = activeCubes.Count;
        isReady = true;
    }

    private PixelCube TakeCubeFromPool()
    {
        if (objectPool == null)
            return null;

        GameObject cubeObject = objectPool.SpawnFromPool(PoolTags.PixelCube, Vector3.zero, Quaternion.identity, false);

        if (cubeObject == null)
            return null;

        cubeObject.transform.SetParent(transform, false);
        PixelCube cube = cubeObject.GetComponent<PixelCube>();

        if (cube == null)
        {
            objectPool.DestroyToPool(PoolTags.PixelCube, cubeObject);
            return null;
        }

        cube.ResetMeshTransform();
        return cube;
    }

    private void ReturnCubesToPool()
    {
        for (int i = 0; i < activeCubes.Count; i++)
        {
            PixelCube cube = activeCubes[i];

            if (cube == null)
                continue;

            if (objectPool != null)
            {
                cube.DeactivateCube();
                objectPool.DestroyToPool(PoolTags.PixelCube, cube.gameObject, false);
            }
            else
                Destroy(cube.gameObject);
        }

        activeCubes.Clear();
    }

    private bool TryGetCubeIndexAtCell(Vector2Int gridPosition, out int cubeIndex)
    {
        cubeIndex = 0;

        if (cellToCube == null)
            return false;

        return cellToCube.TryGetValue(gridPosition, out cubeIndex);
    }

    private bool IsCubePopAnimationPlaying(int cubeIndex)
    {
        for (int i = 0; i < poppingCount; i++)
        {
            if (poppingCubes[i] == cubeIndex)
                return true;
        }

        return false;
    }

    private async UniTaskVoid PlayPopThenHideCube(int cubeIndex)
    {
        int version = layoutVersion;
        float baseScale = cellScale;
        float poppedHeight = cellScale * PopHeightScale;
        float elapsed = 0f;

        while (elapsed < PopPunchDuration)
        {
            if (version != layoutVersion)
                return;

            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / PopPunchDuration);
            float eased = progress * (2f - progress);
            float height = Mathf.Lerp(baseScale, poppedHeight, eased);
            SetCubeScaleDuringPop(cubeIndex, new Vector3(baseScale, height, baseScale));

            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        elapsed = 0f;

        while (elapsed < PopCollapseDuration)
        {
            if (version != layoutVersion)
                return;

            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / PopCollapseDuration);
            float remain = 1f - (progress * progress);
            SetCubeScaleDuringPop(cubeIndex, new Vector3(baseScale * remain, poppedHeight * remain, baseScale * remain));

            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        if (version != layoutVersion)
            return;

        HideCubeWhenPopEnds(cubeIndex);
    }

    private void HideCubeWhenPopEnds(int cubeIndex)
    {
        RemoveCubeFromPopAnimation(cubeIndex);

        if (cubeIndex < 0 || cubeIndex >= activeCubes.Count)
            return;

        HideCubeAndRefreshNeighbors(activeCubes[cubeIndex]);
    }

    private void SetCubeScaleDuringPop(int cubeIndex, Vector3 scale)
    {
        if (cubeIndex < 0 || cubeIndex >= activeCubes.Count)
            return;

        activeCubes[cubeIndex].SetCubeScale(scale);
    }

    private void RemoveCubeFromPopAnimation(int cubeIndex)
    {
        for (int i = 0; i < poppingCount; i++)
        {
            if (poppingCubes[i] != cubeIndex)
                continue;

            poppingCount--;
            poppingCubes[i] = poppingCubes[poppingCount];
            return;
        }
    }

    private void HideCubeAndRefreshNeighbors(PixelCube cube)
    {
        Vector2Int gridPosition = cube.GridPosition;
        cube.DeactivateCube();
        UpdateNeighborOutlineAndShadow(gridPosition);
    }

    private void UpdateCubeOutlineAndShadow(PixelCube cube)
    {
        if (cube == null || !cube.Filled)
            return;

        int x = cube.GridX;
        int y = cube.GridY;
        bool leftFilled = IsPixelFilledAtCell(new Vector2Int(x - 1, y));
        bool rightFilled = IsPixelFilledAtCell(new Vector2Int(x + 1, y));
        bool downFilled = IsPixelFilledAtCell(new Vector2Int(x, y - 1));
        bool upFilled = IsPixelFilledAtCell(new Vector2Int(x, y + 1));
        bool upLeftFilled = IsPixelFilledAtCell(new Vector2Int(x - 1, y + 1));
        bool enclosed = leftFilled && rightFilled && downFilled && upFilled;

        cube.SetFakeShadowVisible(!leftFilled || !upFilled || !upLeftFilled);
        cube.SwitchOutlineMaterial(!enclosed, instanceMaterial, outlineOffMaterial);
    }

    private void UpdateNeighborOutlineAndShadow(Vector2Int gridPosition)
    {
        for (int i = 0; i < ExposedNeighbors.Length; i++)
        {
            Vector2Int offset = ExposedNeighbors[i];
            UpdateOutlineAndShadowAtCell(new Vector2Int(gridPosition.x + offset.x, gridPosition.y + offset.y));
        }
    }

    private void UpdateOutlineAndShadowAtCell(Vector2Int gridPosition)
    {
        if (!TryGetCubeIndexAtCell(gridPosition, out int cubeIndex))
            return;

        UpdateCubeOutlineAndShadow(activeCubes[cubeIndex]);
    }

    private void CreateMaterialWithOutlineDisabled()
    {
        if (outlineOffMaterial != null || instanceMaterial == null)
            return;

        outlineOffMaterial = new Material(instanceMaterial);
        outlineOffMaterial.SetShaderPassEnabled(OutlinePassName, false);
    }

    private bool IsPixelFilledAtCell(Vector2Int gridPosition)
    {
        if (!TryGetCubeIndexAtCell(gridPosition, out int cubeIndex))
            return false;

        PixelCube cube = activeCubes[cubeIndex];
        return cube != null && cube.Filled;
    }

    private void OnDestroy()
    {
        layoutVersion++;
        ReturnCubesToPool();

        if (outlineOffMaterial != null)
            Destroy(outlineOffMaterial);
    }
}
