using UnityEngine;

public class RangedAttackStrategy : MonoBehaviour, IAttackStrategy
{
    [Header("Projectile Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private float projectileSpeed = 15f;
    [SerializeField] private float maxSpreadAngle = 30f;
    [SerializeField] private float targetHeightOffset = 1f; // Hedef yükseklik ofseti

    private void Awake()
    {
        if (projectileSpawnPoint == null)
        {
            projectileSpawnPoint = transform;
        }

        if (projectilePrefab == null)
        {
            Debug.LogError("Projectile Prefab is not assigned on " + gameObject.name, this);
        }
    }

    public void Attack(Transform target, float accuracy)
    {
        if (target == null)
        {
            Debug.LogWarning("Attack target is null!", this);
            return;
        }

        if (projectilePrefab == null)
        {
            Debug.LogError("Cannot attack: Projectile Prefab is missing!", this);
            return;
        }

        // Hedef pozisyonunu hesapla (yükseklik ofseti ile)
        Vector3 targetPosition = target.position + Vector3.up * targetHeightOffset;

        // Doğruluk oranına göre saçılmayı hesapla
        float spread = maxSpreadAngle * (1f - accuracy);
        Vector3 randomSpread = new Vector3(
            Random.Range(-spread, spread),
            Random.Range(-spread * 0.5f, spread * 0.5f),
            Random.Range(-spread, spread)
        );

        // Saçılmayı hedef pozisyonuna uygula
        targetPosition += randomSpread * 0.1f;

        // Mermi rotasyonunu hesapla
        Vector3 direction = (targetPosition - projectileSpawnPoint.position).normalized;
        Quaternion rotation = Quaternion.LookRotation(direction);

        // Mermiyi oluştur
        GameObject projectileObj = Instantiate(
            projectilePrefab,
            projectileSpawnPoint.position,
            rotation
        );

        // Mermi bileşenini al ve başlat
        EnemyProjectile projectile = projectileObj.GetComponent<EnemyProjectile>();
        if (projectile != null)
        {
            projectile.Initialize(targetPosition, projectileSpeed);
        }
        else
        {
            Debug.LogError("Projectile prefab does not have EnemyProjectile component!", this);
            Destroy(projectileObj);
        }

       
    }
} 