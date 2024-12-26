using UnityEngine;

public class ShootingSystem : MonoBehaviour, IShooter
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float spawnHeight = -8.7f;
    [SerializeField] private float spawnForwardOffset = 1.5f; // Karakterden ne kadar ileride spawn olacak

    [Header("Shooting Settings")]
    [SerializeField] private float shootCooldown = 0.5f;

    private float lastShootTime;
    private IProjectileStrategy projectileStrategy;

    private void Awake()
    {
        projectileStrategy = new ArrowProjectileStrategy();
    }

    public bool CanShoot => Time.time - lastShootTime >= shootCooldown;

    public void Shoot(Vector3 direction)
    {
        if (!CanShoot) return;

        // Spawn pozisyonunu hesapla
        Vector3 spawnPosition = transform.position + (direction.normalized * spawnForwardOffset);
        spawnPosition.y = spawnHeight;

        // Mermiyi oluþtur ve Layer'ýný ayarla
        GameObject projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);

        // Player ve Projectile'ýn çarpýþmamasýný saðla
        Physics.IgnoreCollision(projectile.GetComponent<Collider>(), GetComponent<Collider>());

        var projectileComponent = projectile.GetComponent<Projectile>();
        if (projectileComponent != null)
        {
            projectileComponent.Initialize(projectileStrategy);
            projectileStrategy.InitializeProjectile(projectile, spawnPosition, direction);
        }

        lastShootTime = Time.time;
    }

    private void OnDrawGizmos()
    {
        Vector3 spawnPos = transform.position + (transform.forward * spawnForwardOffset);
        spawnPos.y = spawnHeight;

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(spawnPos, 0.2f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(spawnPos, spawnPos + transform.forward * 2f);
    }
}