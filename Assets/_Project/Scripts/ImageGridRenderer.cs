using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LevelManagement;
using UnityEngine;
using UnityEngine.Rendering;

public class ImageGridRenderer : MonoBehaviour
{
    private const int MaxHidePops = 64;
    private const int MaxPaletteLevel = 64;
    private const float HidePunchDuration = 0.08f;
    private const float HideCollapseDuration = 0.12f;
    private const float HidePopY = 1.2f;

    private readonly int ColorPropertyId = Shader.PropertyToID("_BaseColor");

    [Header("Render Assets")]
    [SerializeField] private GameObject pixelCubePrefab;
    [SerializeField] private Material instanceMaterial;

    [Header("Grid Layout")]
    [SerializeField] private float cellSpacing = 0.25f;
    [SerializeField] private float cellScale = 0.22f;

    public float CellSpacing => cellSpacing;

    private bool isReady;
    private int motionVersion;
    private readonly List<PixelData> opaqueScratch = new List<PixelData>(1024);
    private readonly List<PixelCube> cubes = new List<PixelCube>(1024);
    private Dictionary<Vector2Int, int> gridToInstanceMap;
    private readonly int[] activeHideKeys = new int[MaxHidePops];
    private readonly Color32[] tintByLevel = new Color32[MaxPaletteLevel + 1];
    private readonly bool[] tintLevelReady = new bool[MaxPaletteLevel + 1];
    private int activeHideCount;
    private int visibleCount;

    public void BuildPixelGrid(LevelData levelData)
    {
        motionVersion++;
        activeHideCount = 0;
        isReady = false;
        visibleCount = 0;

        if (gridToInstanceMap != null)
            gridToInstanceMap.Clear();

        if (levelData == null || pixelCubePrefab == null)
            return;

        opaqueScratch.Clear();
        levelData.CopyOpaquePixelsTo(opaqueScratch);
        PlacePixels(opaqueScratch, levelData.PixelSize);
    }

    private void PlacePixels(IList<PixelData> pixels, Vector2Int textureSize)
    {
        if (pixels == null || pixels.Count == 0)
            return;

        int pixelCount = pixels.Count;

        if (textureSize.x < 1)
            textureSize.x = 1;

        if (textureSize.y < 1)
            textureSize.y = 1;

        float halfW = (textureSize.x - 1) * 0.5f;
        float halfH = (textureSize.y - 1) * 0.5f;
        Vector3 scale = new Vector3(cellScale, cellScale, cellScale);

        if (gridToInstanceMap == null)
            gridToInstanceMap = new Dictionary<Vector2Int, int>(pixelCount);

        for (int i = 0; i < pixelCount; i++)
        {
            PixelData pixel = pixels[i];
            PixelCube cube = GetCube(i);

            Transform cubeTransform = cube.root.transform;
            float localX = (pixel.position.x - halfW) * cellSpacing;
            float localZ = (pixel.position.y - halfH) * cellSpacing;
            cubeTransform.localPosition = new Vector3(localX, 0f, localZ);
            cubeTransform.localRotation = Quaternion.identity;
            cubeTransform.localScale = scale;
            cube.renderer.enabled = true;
            cube.renderer.shadowCastingMode = ShadowCastingMode.On;

            if (instanceMaterial != null)
                cube.renderer.sharedMaterial = instanceMaterial;

            cube.paletteId = pixel.paletteId;
            cube.block.SetColor(ColorPropertyId, pixel.color);
            cube.renderer.SetPropertyBlock(cube.block);
            gridToInstanceMap[pixel.position] = i;
        }

        for (int i = pixelCount; i < cubes.Count; i++)
        {
            cubes[i].renderer.enabled = false;
            cubes[i].root.SetActive(false);
        }

        visibleCount = pixelCount;
        isReady = true;
    }

    private PixelCube GetCube(int index)
    {
        if (index < cubes.Count)
        {
            PixelCube existing = cubes[index];
            existing.root.SetActive(true);
            return existing;
        }

        GameObject cubeObject = Instantiate(pixelCubePrefab, transform);
        MeshRenderer cubeRenderer = cubeObject.GetComponentInChildren<MeshRenderer>(true);
        Transform visual = cubeRenderer != null ? cubeRenderer.transform : cubeObject.transform;

        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;

        PixelCube cube = new PixelCube
        {
            root = cubeObject,
            renderer = cubeRenderer,
            block = new MaterialPropertyBlock()
        };

        cubes.Add(cube);

        return cube;
    }

    private bool TryGetCubeIndex(Vector2Int gridPosition, out int cubeIndex)
    {
        cubeIndex = 0;

        if (gridToInstanceMap == null)
            return false;

        return gridToInstanceMap.TryGetValue(gridPosition, out cubeIndex);
    }

    public bool TryGetWorldPosition(Vector2Int gridPosition, out Vector3 worldPosition)
    {
        worldPosition = default;

        if (!TryGetCubeIndex(gridPosition, out int cubeIndex))
            return false;

        worldPosition = cubes[cubeIndex].root.transform.position;
        return true;
    }

    public bool HidePixel(Vector2Int gridPosition)
    {
        if (!TryGetCubeIndex(gridPosition, out int cubeIndex))
            return false;

        for (int i = 0; i < activeHideCount; i++)
        {
            if (activeHideKeys[i] == cubeIndex)
                return true;
        }

        PixelCube cube = cubes[cubeIndex];

        if (!cube.renderer.enabled)
            return true;

        if (activeHideCount >= MaxHidePops)
        {
            DisablePixel(cube);
            return true;
        }

        activeHideKeys[activeHideCount] = cubeIndex;
        activeHideCount++;

        PlayHide(cubeIndex).Forget();

        return true;
    }

    private async UniTaskVoid PlayHide(int cubeIndex)
    {
        int version = motionVersion;
        PixelCube cube = cubes[cubeIndex];

        if (cube.renderer != null)
            cube.renderer.shadowCastingMode = ShadowCastingMode.Off;

        float baseScale = cellScale;
        float poppedY = cellScale * HidePopY;
        float elapsed = 0f;

        while (elapsed < HidePunchDuration)
        {
            if (version != motionVersion)
                return;

            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / HidePunchDuration);
            float eased = progress * (2f - progress);
            float height = Mathf.Lerp(baseScale, poppedY, eased);

            ApplyHideScale(cubeIndex, new Vector3(baseScale, height, baseScale));

            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        elapsed = 0f;
        while (elapsed < HideCollapseDuration)
        {
            if (version != motionVersion)
                return;

            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / HideCollapseDuration);
            float remain = 1f - (progress * progress);
            ApplyHideScale(cubeIndex, new Vector3(baseScale * remain, poppedY * remain, baseScale * remain));

            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        if (version != motionVersion)
            return;

        CompleteHide(cubeIndex);
    }

    public void HideAllPixels()
    {
        motionVersion++;
        int count = visibleCount < cubes.Count ? visibleCount : cubes.Count;

        for (int i = 0; i < count; i++)
        {
            PixelCube cube = cubes[i];

            if (cube.root == null || cube.renderer == null || !cube.renderer.enabled)
                continue;

            DisablePixel(cube);
        }

        activeHideCount = 0;
    }

    private void CompleteHide(int cubeIndex)
    {
        RemoveHideKey(cubeIndex);

        if (cubeIndex < 0 || cubeIndex >= cubes.Count)
            return;

        DisablePixel(cubes[cubeIndex]);
    }

    private void ApplyHideScale(int cubeIndex, Vector3 scale)
    {
        if (cubeIndex < 0 || cubeIndex >= cubes.Count)
            return;

        cubes[cubeIndex].root.transform.localScale = scale;
    }

    private void RemoveHideKey(int key)
    {
        for (int i = 0; i < activeHideCount; i++)
        {
            if (activeHideKeys[i] != key)
                continue;

            activeHideCount--;
            activeHideKeys[i] = activeHideKeys[activeHideCount];
            return;
        }
    }

    private void DisablePixel(PixelCube cube)
    {
        cube.root.transform.localScale = Vector3.zero;
        cube.renderer.enabled = false;
    }

    private void OnDestroy()
    {
        motionVersion++;
        for (int i = 0; i < cubes.Count; i++)
        {
            if (cubes[i].root != null)
                Destroy(cubes[i].root);
        }

        cubes.Clear();
    }

    public void ApplyPaletteColors(int[] levels, Color32[] paletteColors, int paletteCount)
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

        int count = visibleCount < cubes.Count ? visibleCount : cubes.Count;

        for (int i = 0; i < count; i++)
        {
            PixelCube cube = cubes[i];
            int level = cube.paletteId;

            if (level <= 0 || level > maxLevel || !tintLevelReady[level])
                continue;

            cube.block.SetColor(ColorPropertyId, tintByLevel[level]);
            cube.renderer.SetPropertyBlock(cube.block);
        }
    }

    private class PixelCube
    {
        public GameObject root;
        public MeshRenderer renderer;
        public MaterialPropertyBlock block;
        public short paletteId;
    }
}
