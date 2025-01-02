using UnityEngine;
using Unity.Netcode;

public class RangedAttackStrategy : NetworkBehaviour, IAttackStrategy
{
    [Header("Projectile Settings")]
    [SerializeField] private NetworkObject projectilePrefab;
    [SerializeField] private float projectileSpeed = 15f;
    [SerializeField] private float maxSpreadAngle = 30f;
    [SerializeField] private float targetHeightOffset = 1f;

    private Transform projectileSpawnPoint;

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

    public void SetProjectileSpawnPoint(Transform spawnPoint)
    {
        projectileSpawnPoint = spawnPoint;
    }

    public void Attack(Transform target, float accuracy)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            // Client tarafında attack çağrıldıysa, server'a RPC gönder
            AttackServerRpc(target.position, accuracy);
            return;
        }

        SpawnProjectile(target.position, accuracy);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AttackServerRpc(Vector3 targetPosition, float accuracy)
    {
        SpawnProjectile(targetPosition, accuracy);
    }

    private void SpawnProjectile(Vector3 targetPosition, float accuracy)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (projectilePrefab == null)
        {
            Debug.LogError("Cannot attack: Projectile Prefab is missing!", this);
            return;
        }

        if (projectileSpawnPoint == null)
        {
            Debug.LogError("Cannot attack: Projectile Spawn Point is missing!", this);
            return;
        }

        // Hedef pozisyonunu hesapla (yükseklik ofseti ile)
        Vector3 targetPos = targetPosition + Vector3.up * targetHeightOffset;

        // Doğruluk oranına göre saçılmayı hesapla
        float spread = maxSpreadAngle * (1f - accuracy);
        Vector3 randomSpread = new Vector3(
            Random.Range(-spread, spread),
            Random.Range(-spread * 0.5f, spread * 0.5f),
            Random.Range(-spread, spread)
        );

        // Saçılmayı hedef pozisyonuna uygula
        targetPos += randomSpread * 0.1f;

        // Mermi rotasyonunu hesapla
        Vector3 direction = (targetPos - projectileSpawnPoint.position).normalized;
        Quaternion rotation = Quaternion.LookRotation(direction);

        try
        {
            // Mermiyi spawn et
            NetworkObject projectileObj = Instantiate(projectilePrefab, projectileSpawnPoint.position, rotation);
            
            if (projectileObj != null)
            {
                projectileObj.Spawn(true);

                // Mermi bileşenini al ve başlat
                EnemyProjectile projectile = projectileObj.GetComponent<EnemyProjectile>();
                if (projectile != null)
                {
                    projectile.Initialize(projectileSpawnPoint.position, targetPos, projectileSpeed);
                }
                else
                {
                    Debug.LogError("Projectile prefab does not have EnemyProjectile component!", this);
                    projectileObj.Despawn(true);
                }
            }
            else
            {
                Debug.LogError("Failed to instantiate projectile!", this);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error spawning projectile: {e.Message}", this);
        }
    }
} 