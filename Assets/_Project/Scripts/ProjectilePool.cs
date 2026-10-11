using UnityEngine;

public class ProjectilePool : MonoBehaviour
{
    private const int PrewarmCount = 48;

    private Projectile bulletPrefab;
    private Projectile[] slots;
    private int availableCount;
    private Material sharedMaterial;
    private bool isBuilt;

    public void Initialize(Projectile prefab)
    {
        bulletPrefab = prefab;
        Build();
    }

    private void Build()
    {
        if (isBuilt || bulletPrefab == null)
            return;

        isBuilt = true;
        slots = new Projectile[PrewarmCount];
        availableCount = PrewarmCount;

        for (int i = 0; i < PrewarmCount; i++)
        {
            Projectile projectile = Instantiate(bulletPrefab, transform);
            projectile.name = "Projectile";

            MeshRenderer meshRenderer = projectile.GetComponent<MeshRenderer>();
            if (meshRenderer != null && sharedMaterial != null)
                meshRenderer.sharedMaterial = sharedMaterial;

            projectile.Prepare();
            projectile.Hide();
            slots[i] = projectile;
        }
    }

    public Projectile Rent()
    {
        if (slots == null || availableCount <= 0)
            return null;

        availableCount--;
        return slots[availableCount];
    }

    public void Return(Projectile projectile)
    {
        if (projectile == null || slots == null || availableCount >= slots.Length)
            return;

        projectile.Hide();
        slots[availableCount] = projectile;
        availableCount++;
    }
}
