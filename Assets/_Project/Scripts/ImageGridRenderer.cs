using System.Collections.Generic;
using DG.Tweening;
using LevelManagement;
using UnityEngine;
using UnityEngine.Rendering;

public class ImageGridRenderer : MonoBehaviour
{
    private const int MaxHidePops = 64;
    private const float HidePunchDuration = 0.08f;
    private const float HideCollapseDuration = 0.12f;
    private const float HidePopY = 1.2f;

    private readonly int colorPropertyId = Shader.PropertyToID("_BaseColor");

    [Header("Render Assets")]
    [SerializeField] private GameObject pixelCubePrefab;
    [SerializeField] private Material instanceMaterial;

    [Header("Grid Layout")]
    [SerializeField] private float cellSpacing = 0.25f;
    [SerializeField] private float cellScale = 0.22f;

    public float CellSpacing => cellSpacing;

    private bool isReady;
    private readonly List<PixelData> opaqueScratch = new List<PixelData>(1024);
    private readonly List<PixelCube> cubes = new List<PixelCube>(1024);
    private Dictionary<Vector2Int, int> gridToInstanceMap;
    private readonly int[] activeHideKeys = new int[MaxHidePops];
    private int activeHideCount;
    private int visibleCount;

    public void BuildBatches(LevelData levelData)
    {
        DOTween.Kill(this);
        activeHideCount = 0;
        isReady = false;
        visibleCount = 0;
        gridToInstanceMap = null;

        if (levelData == null || pixelCubePrefab == null)
            return;

        opaqueScratch.Clear();
        if (opaqueScratch.Capacity < 256)
            opaqueScratch.Capacity = 256;

        levelData.CopyOpaquePixelsTo(opaqueScratch);
        BuildFromOpaqueList(opaqueScratch, levelData.PixelSize);
    }

    private void BuildFromOpaqueList(IList<PixelData> pixels, Vector2Int textureSize)
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
        gridToInstanceMap = new Dictionary<Vector2Int, int>(pixelCount);

        for (int i = 0; i < pixelCount; i++)
        {
            PixelData pixel = pixels[i];
            PixelCube cube = RentCube(i);
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
            cube.block.SetColor(colorPropertyId, pixel.color);
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

    private PixelCube RentCube(int index)
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

    private bool TryGetInstanceLocation(Vector2Int gridPosition, out int cubeIndex)
    {
        cubeIndex = 0;
        if (gridToInstanceMap == null)
            return false;

        return gridToInstanceMap.TryGetValue(gridPosition, out cubeIndex);
    }

    public bool TryGetWorldPosition(Vector2Int gridPosition, out Vector3 worldPosition)
    {
        worldPosition = default;
        if (!TryGetInstanceLocation(gridPosition, out int cubeIndex))
            return false;

        worldPosition = cubes[cubeIndex].root.transform.position;
        return true;
    }

    public bool HidePixel(Vector2Int gridPosition)
    {
        if (!TryGetInstanceLocation(gridPosition, out int cubeIndex))
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
            FinishPixel(cube);
            return true;
        }

        activeHideKeys[activeHideCount] = cubeIndex;
        activeHideCount++;
        PlayHideTween(cubeIndex, cubeIndex);
        return true;
    }

    private void PlayHideTween(int cubeIndex, int key)
    {
        PixelCube cube = cubes[cubeIndex];
        if (cube.renderer != null)
        {
            cube.renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        float baseScale = cellScale;
        float poppedY = cellScale * HidePopY;
        float punch = baseScale;
        float collapse = 0f;

        Sequence sequence = DOTween.Sequence();
        sequence.SetTarget(this);
        sequence.Append(DOTween.To(() => punch, value =>
        {
            punch = value;
            ApplyHideScale(cubeIndex, new Vector3(baseScale, value, baseScale));
        }, poppedY, HidePunchDuration).SetEase(Ease.OutQuad));
        sequence.Append(DOTween.To(() => collapse, value =>
        {
            collapse = value;
            float remain = 1f - value;
            ApplyHideScale(cubeIndex, new Vector3(baseScale * remain, poppedY * remain, baseScale * remain));
        }, 1f, HideCollapseDuration).SetEase(Ease.InQuad));
        sequence.OnComplete(() => FinishHidden(cubeIndex, key));
        sequence.OnKill(() => FinishHidden(cubeIndex, key));
    }

    public void HideRemainingImmediate()
    {
        DOTween.Kill(this);
        int count = visibleCount < cubes.Count ? visibleCount : cubes.Count;
        for (int i = 0; i < count; i++)
        {
            PixelCube cube = cubes[i];
            if (cube.root == null || cube.renderer == null || !cube.renderer.enabled)
                continue;

            FinishPixel(cube);
        }

        activeHideCount = 0;
    }

    private void FinishHidden(int cubeIndex, int key)
    {
        RemoveHideKey(key);
        if (cubeIndex < 0 || cubeIndex >= cubes.Count)
            return;

        FinishPixel(cubes[cubeIndex]);
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

    private void FinishPixel(PixelCube cube)
    {
        cube.root.transform.localScale = Vector3.zero;
        cube.renderer.enabled = false;
    }

    private void OnDestroy()
    {
        DOTween.Kill(this);
        for (int i = 0; i < cubes.Count; i++)
        {
            if (cubes[i].root != null)
                Destroy(cubes[i].root);
        }

        cubes.Clear();
    }

    public void RetintToPalette(int[] levels, Color32[] paletteColors, int paletteCount)
    {
        if (!isReady || levels == null || paletteColors == null || paletteCount <= 0)
            return;

        int maxLevel = 0;
        for (int i = 0; i < paletteCount; i++)
        {
            if (levels[i] > maxLevel)
                maxLevel = levels[i];
        }

        if (maxLevel <= 0 || maxLevel > 64)
            return;

        Color32[] byLevel = new Color32[maxLevel + 1];
        bool[] hasLevel = new bool[maxLevel + 1];
        for (int i = 0; i < paletteCount; i++)
        {
            int level = levels[i];
            if (level <= 0 || level > maxLevel)
                continue;

            byLevel[level] = paletteColors[i];
            hasLevel[level] = true;
        }

        int count = visibleCount < cubes.Count ? visibleCount : cubes.Count;
        for (int i = 0; i < count; i++)
        {
            PixelCube cube = cubes[i];
            int level = cube.paletteId;
            if (level <= 0 || level > maxLevel || !hasLevel[level])
                continue;

            cube.block.SetColor(colorPropertyId, byLevel[level]);
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
