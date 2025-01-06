using UnityEngine;
using Unity.Netcode;
using System.Linq;

public class RangedAttackStrategy : NetworkBehaviour, IAttackStrategy
{
    [Header("Projectile Settings")]
    [SerializeField] private NetworkObject projectilePrefab;
    [SerializeField] private float projectileSpeed = 15f;
    [SerializeField] private float maxSpreadAngle = 30f;
    [SerializeField] private float targetHeightOffset = 1f;
    [SerializeField] private int initialPoolSize = 30;

    private Transform projectileSpawnPoint;
    private const string PROJECTILE_POOL_TAG = "EnemyProjectile";
    private bool isPoolInitialized = false;

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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer && !isPoolInitialized)
        {
            InitializeProjectilePool();
        }
    }

    private void InitializeProjectilePool()
    {
        if (ObjectPool.Instance != null && projectilePrefab != null)
        {
            ObjectPool.Instance.RegisterPrefab(PROJECTILE_POOL_TAG, projectilePrefab, initialPoolSize);
            isPoolInitialized = true;
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
            // Object pool'dan mermi al ve NetworkObjectId'sini kaydet
            ulong lastSpawnedId = 0;
            ObjectPool.Instance.SpawnFromPoolServerRpc(
                PROJECTILE_POOL_TAG,
                projectileSpawnPoint.position,
                rotation,
                new ServerRpcParams
                {
                    Receive = new ServerRpcReceiveParams
                    {
                        SenderClientId = NetworkManager.Singleton.LocalClientId
                    }
                }
            );

            // Son spawn edilen objeyi bul
            var spawnedObjects = NetworkManager.Singleton.SpawnManager.SpawnedObjects;
            NetworkObject projectileObj = spawnedObjects.Values
                .Where(obj => obj.GetComponent<EnemyProjectile>() != null)
                .OrderBy(obj => obj.NetworkObjectId)
                .LastOrDefault();

            if (projectileObj != null)
            {
                EnemyProjectile projectile = projectileObj.GetComponent<EnemyProjectile>();
                if (projectile != null)
                {
                    projectile.Initialize(projectileSpawnPoint.position, targetPos, projectileSpeed);
                }
                else
                {
                    Debug.LogError("Projectile prefab does not have EnemyProjectile component!", this);
                    ObjectPool.Instance.ReturnToPoolServerRpc(projectileObj);
                }
            }
            else
            {
                Debug.LogError("Failed to find spawned projectile!", this);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error spawning projectile: {e.Message}", this);
        }
    }
} 