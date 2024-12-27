using UnityEngine;

public class ShootingSystem : MonoBehaviour, IShooter
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float spawnHeight = -8.7f;
    [SerializeField] private float spawnForwardOffset = 1.5f;

    [Header("Shooting Settings")]
    [SerializeField] private float shootCooldown = 0.5f;

    private float lastShootTime;
    private Transform playerTransform;

    private void Awake()
    {
        playerTransform = transform;
    }

    public bool CanShoot => Time.time - lastShootTime >= shootCooldown;

    public void Shoot(Vector3 direction)
    {
        if (!CanShoot) return;

        Vector3 spawnPosition = CalculateSpawnPosition(direction);

        GameObject projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        Physics.IgnoreCollision(projectile.GetComponent<Collider>(), GetComponent<Collider>());

        var projectileComponent = projectile.GetComponent<Projectile>();
        if (projectileComponent != null)
        {
            // Her mermi için yeni bir strateji örneði oluþtur
            IProjectileStrategy projectileStrategy = new ArrowProjectileStrategy();
            projectileComponent.Initialize(projectileStrategy);
            projectileStrategy.InitializeProjectile(projectile, spawnPosition, direction);
        }

        lastShootTime = Time.time;
    }

    private Vector3 CalculateSpawnPosition(Vector3 direction)
    {
        Vector3 spawnPosition = playerTransform.position;
        spawnPosition.y = spawnHeight;
        Vector3 normalizedDirection = direction.normalized;
        spawnPosition += normalizedDirection * spawnForwardOffset;
        return spawnPosition;
    }
}