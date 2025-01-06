using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class EnemyProjectile : NetworkBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float maxLifetime = 5f;
    [SerializeField] private float collisionCheckRadius = 0.1f;
    [SerializeField] private LayerMask collisionMask;
    
    private Rigidbody rb;
    private Vector3 targetPosition;
    private float projectileSpeed;
    private float currentLifetime;
    private bool isInitialized = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        currentLifetime = 0f;
    }

    public void Initialize(Vector3 startPosition, Vector3 targetPos, float speed)
    {
        if (!IsServer) return;

        transform.position = startPosition;
        targetPosition = targetPos;
        projectileSpeed = speed;
        currentLifetime = 0f;
        isInitialized = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Vector3 direction = (targetPosition - startPosition).normalized;
            rb.linearVelocity = direction * projectileSpeed;
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer || !isInitialized) return;

        currentLifetime += Time.fixedDeltaTime;
        if (currentLifetime >= maxLifetime)
        {
            ReturnToPool();
            return;
        }

        // Çarpışma kontrolü
        Collider[] hits = Physics.OverlapSphere(transform.position, collisionCheckRadius, collisionMask);
        foreach (Collider hit in hits)
        {
            if (hit.TryGetComponent<IHealth>(out var health))
            {
                health.TakeDamage(damage);
                ReturnToPool();
                return;
            }
            else if (hit.gameObject.layer != gameObject.layer) // Kendi layer'ımız hariç herhangi bir şeye çarpma
            {
                ReturnToPool();
                return;
            }
        }
    }

    private void ReturnToPool()
    {
        if (!IsServer) return;

        isInitialized = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        ObjectPool.Instance.ReturnToPoolServerRpc(new NetworkObjectReference(NetworkObject));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, collisionCheckRadius);
    }
} 