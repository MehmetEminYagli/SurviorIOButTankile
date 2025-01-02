using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class NetworkProjectile : NetworkBehaviour
{
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private float damage = 10f;

    private NetworkVariable<Vector3> networkVelocity = new NetworkVariable<Vector3>();
    private Rigidbody rb;
    private ulong shooterClientId;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            shooterClientId = OwnerClientId;
            Invoke(nameof(DespawnProjectile), lifeTime);
        }
    }

    private void FixedUpdate()
    {
        if (IsServer)
        {
            // Sadece belirgin velocity değişimlerinde güncelle
            if (Vector3.Distance(networkVelocity.Value, rb.linearVelocity) > 0.1f)
            {
                networkVelocity.Value = rb.linearVelocity;
            }
        }
        else
        {
            rb.linearVelocity = networkVelocity.Value;
        }
    }

    public void Initialize(Vector3 velocity)
    {
        if (IsServer)
        {
            rb.linearVelocity = velocity;
            networkVelocity.Value = velocity;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        // Dost ateşini önle
        if (collision.gameObject.CompareTag("Player"))
        {
            NetworkObject networkObject = collision.gameObject.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.OwnerClientId == shooterClientId)
            {
                return;
            }
        }

        // Hasar verme işlemi
        IHealth health = collision.gameObject.GetComponent<IHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
            DespawnProjectile();
        }
        else if (!collision.gameObject.CompareTag("Projectile"))
        {
            // Sadece hasar verilemeyen ve mermi olmayan nesnelerde yok ol
            DespawnProjectile();
        }
    }

    private void DespawnProjectile()
    {
        if (IsServer)
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }
    }
} 