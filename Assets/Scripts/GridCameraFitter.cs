using UnityEngine;

/// <summary>
/// Ana kamerayi ortografik moda alarak pixel grid'i her mobil en-boy oraninda ekrana tam sigdirir.
/// Awake'te bir kez ortalar ve fit eder (Update / coroutine yok).
/// </summary>
[RequireComponent(typeof(Camera))]
public class GridCameraFitter : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("32x32 piksel ve 0.25 spacing icin ornek: 8")]
    [SerializeField] private float gridWorldWidth = 8f;
    [Tooltip("32x32 piksel ve 0.25 spacing icin ornek: 8")]
    [SerializeField] private float gridWorldHeight = 8f;
    [SerializeField] private float padding = 1.5f;

    [Header("Auto Center")]
    [Tooltip("Izgarayi cizen ana obje buraya atanirsa, kamera kaymalari engellenir.")]
    [SerializeField] private Transform gridTransform;
    [SerializeField] private float cameraHeight = 10f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        CenterCamera();
        FitToGrid();
    }

    private void CenterCamera()
    {
        if (gridTransform == null)
            return;

        Vector3 targetPos = gridTransform.position;
        targetPos.y = cameraHeight;
        transform.position = targetPos;
    }

    private void FitToGrid()
    {
        if (cam == null)
            return;

        float targetWidth = gridWorldWidth + padding;
        float targetHeight = gridWorldHeight + padding;
        float targetRatio = targetWidth / targetHeight;
        float currentAspect = cam.aspect;

        if (currentAspect >= targetRatio)
            cam.orthographicSize = targetHeight * 0.5f;
        else
            cam.orthographicSize = (targetHeight * 0.5f) * (targetRatio / currentAspect);
    }
}
