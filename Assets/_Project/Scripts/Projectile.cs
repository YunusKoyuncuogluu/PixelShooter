using System.Threading;
using Cysharp.Threading.Tasks;
using PixelShooter.Pooling;
using UnityEngine;

public class Projectile : MonoBehaviour, IPoolObject
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Renderer cachedRenderer;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        Prepare();
    }

    public void Prepare()
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    public void Reset()
    {
        Hide();
    }

    public void SetColor(Color color)
    {
        if (cachedRenderer == null || propertyBlock == null)
            return;

        propertyBlock.Clear();
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        cachedRenderer.SetPropertyBlock(propertyBlock);
    }

    public async UniTask FlyAsync(Vector3 start, Vector3 target, CancellationToken cancellationToken)
    {
        transform.position = start;
        gameObject.SetActive(true);

        const float safeDuration = 0.12f;
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            elapsed += Time.deltaTime;
            float progress = elapsed / safeDuration;
            if (progress > 1f)
                progress = 1f;

            transform.position = Vector3.LerpUnclamped(start, target, progress);

            Vector3 direction = target - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        transform.position = target;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
