using UnityEngine;

public class ConveyorMaterialScroller : MonoBehaviour
{
    [SerializeField] private int materialIndex = 2;
    [SerializeField] private float scrollSpeed = 0.35f;

    private Renderer targetRenderer;
    private MaterialPropertyBlock propertyBlock;
    private int baseMapStId;
    private Vector4 baseSt;
    private float offsetY;
    private float initialOffsetY;

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        baseMapStId = Shader.PropertyToID("_BaseMap_ST");

        Material material = targetRenderer.sharedMaterials[materialIndex];
        baseSt = material.GetVector(baseMapStId);
        initialOffsetY = baseSt.w;
        offsetY = initialOffsetY;
    }

    private void Update()
    {
        offsetY -= scrollSpeed * Time.deltaTime;
        if (offsetY <= initialOffsetY - 1f)
            offsetY += 1f;

        baseSt.w = offsetY;
        propertyBlock.SetVector(baseMapStId, baseSt);
        targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
    }
}
