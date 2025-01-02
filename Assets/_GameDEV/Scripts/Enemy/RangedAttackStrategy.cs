using UnityEngine;
using Unity.Netcode;

public class RangedAttackStrategy : NetworkBehaviour, IAttackStrategy
{
    [Header("Projectile Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private float maxSpreadAngle = 30f;
    [SerializeField] private float targetHeightOffset = 1f;

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
        if (!IsServer)
        {
            return;
        }

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

        Vector3 targetPosition = target.position + Vector3.up * targetHeightOffset;

        float spread = maxSpreadAngle * (1f - accuracy);
        Vector3 randomSpread = new Vector3(
            Random.Range(-spread, spread),
            Random.Range(-spread * 0.5f, spread * 0.5f),
            Random.Range(-spread, spread)
        );

        targetPosition += randomSpread * 0.1f;

        Vector3 direction = (targetPosition - projectileSpawnPoint.position).normalized;
        Quaternion rotation = Quaternion.LookRotation(direction);

        GameObject projectileObj = Instantiate(
            projectilePrefab,
            projectileSpawnPoint.position,
            rotation
        );

        NetworkObject networkObject = projectileObj.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn(true);

            EnemyProjectile projectile = projectileObj.GetComponent<EnemyProjectile>();
            if (projectile != null)
            {
                projectile.Initialize(targetPosition);
            }
            else
            {
                Debug.LogError("Projectile prefab does not have EnemyProjectile component!", this);
                networkObject.Despawn();
            }
        }
        else
        {
            Debug.LogError("Projectile prefab does not have NetworkObject component!", this);
            Destroy(projectileObj);
        }
    }
} 