using UnityEngine;

[RequireComponent(typeof(Camera))]
public class GridCameraFitter : MonoBehaviour
{
    [Header("Play Area (world AABB)")]
    [SerializeField] private Vector3 areaCenter = new Vector3(0f, 0f, 8f);
    [SerializeField] private float areaWidth = 10f;
    [SerializeField] private float areaDepth = 18.5f;
    [SerializeField] private float areaHeight = 2f;
    [SerializeField] private float padding = 1f;

    [Header("Tasar�m Referans� (Kritik)")]
    [SerializeField] private Vector2 targetNarrowestResolution = new Vector2(1080f, 2400f);

    [SerializeField] private bool centerOnArea = true;

    private Camera cam;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        FitToGrid();
    }

    private void LateUpdate()
    {
        int width = Screen.width;
        int height = Screen.height;
        if (width == lastScreenWidth && height == lastScreenHeight)
            return;

        FitToGrid();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        FitToGrid();
    }
#endif

    private void FitToGrid()
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        if (cam == null)
            return;

        cam.orthographic = true;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        GetCameraSpaceBounds(out float minX, out float maxX, out float minY, out float maxY);

        if (centerOnArea)
            CenterCameraOnBounds(minX, maxX, minY, maxY);

        float halfWidth = (maxX - minX) * 0.5f;
        float currentAspect = lastScreenHeight > 0 ? (float)lastScreenWidth / lastScreenHeight : 1f;

        float targetAspect = targetNarrowestResolution.x / targetNarrowestResolution.y;

        float fixedHeightSize = halfWidth / targetAspect;

        float minWidthSize = halfWidth / currentAspect;

        cam.orthographicSize = Mathf.Max(fixedHeightSize, minWidthSize);
    }

    private void CenterCameraOnBounds(float minX, float maxX, float minY, float maxY)
    {
        float shiftX = (minX + maxX) * 0.5f;
        float shiftY = (minY + maxY) * 0.5f;

        if (Mathf.Abs(shiftX) < 0.0001f && Mathf.Abs(shiftY) < 0.0001f)
            return;

        Transform camTransform = cam.transform;
        camTransform.position += camTransform.right * shiftX + camTransform.up * shiftY;
    }

    private void GetCameraSpaceBounds(out float minX, out float maxX, out float minY, out float maxY)
    {
        float pad = padding * 0.5f;
        float hx = areaWidth * 0.5f + pad;
        float hy = areaHeight * 0.5f;
        float hz = areaDepth * 0.5f + pad;

        Transform camTransform = cam.transform;
        minX = float.MaxValue;
        maxX = float.MinValue;
        minY = float.MaxValue;
        maxY = float.MinValue;

        for (int i = 0; i < 8; i++)
        {
            float x = ((i & 1) == 0) ? -hx : hx;
            float y = ((i & 2) == 0) ? -hy : hy;
            float z = ((i & 4) == 0) ? -hz : hz;

            Vector3 worldPoint;
            worldPoint.x = areaCenter.x + x;
            worldPoint.y = areaCenter.y + y;
            worldPoint.z = areaCenter.z + z;

            Vector3 localPoint = camTransform.InverseTransformPoint(worldPoint);
            if (localPoint.x < minX)
                minX = localPoint.x;

            if (localPoint.x > maxX)
                maxX = localPoint.x;

            if (localPoint.y < minY)
                minY = localPoint.y;

            if (localPoint.y > maxY)
                maxY = localPoint.y;
        }
    }
}