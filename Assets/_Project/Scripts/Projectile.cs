using System.Threading;
using Cysharp.Threading.Tasks;
using PixelShooter.Pooling;
using UnityEngine;

public class Projectile : MonoBehaviour, IPoolObject
{
    private readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private Renderer bulletRenderer;
    [SerializeField] private float flightDuration = 0.12f;

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    public void Reset()
    {
        Hide();
    }

    public void SetColor(Color color)
    {
        if (bulletRenderer == null || propertyBlock == null)
            return;

        propertyBlock.Clear();
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        bulletRenderer.SetPropertyBlock(propertyBlock);
    }

    public async UniTask FlyToTarget(Vector3 start, Vector3 target, CancellationToken cancellationToken)
    {
        transform.position = start;
        gameObject.SetActive(true);

        float elapsed = 0f;

        float maxStep = flightDuration * 0.34f;

        while (elapsed < flightDuration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            float step = Time.unscaledDeltaTime;

            if (step > maxStep)
                step = maxStep;

            elapsed += step;
            float progress = Mathf.Clamp01(elapsed / flightDuration);

            transform.position = Vector3.LerpUnclamped(start, target, progress);

            Vector3 direction = target - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        cancellationToken.ThrowIfCancellationRequested();
        transform.position = target;
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }
}
