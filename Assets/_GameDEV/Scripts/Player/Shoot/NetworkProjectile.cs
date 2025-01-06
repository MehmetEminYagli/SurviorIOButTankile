using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class NetworkProjectile : NetworkBehaviour
{
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private GameObject hitEffectPrefab;

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

        Debug.Log($"[Projectile] Çarpışma tespit edildi: {collision.gameObject.name}");

        // Çarpışma noktasında efekt oluştur
        Vector3 hitPoint = collision.contacts[0].point;
        SpawnHitEffectClientRpc(hitPoint);

        // Prevent friendly fire
        if (collision.gameObject.CompareTag("Player"))
        {
            NetworkObject networkObject = collision.gameObject.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.OwnerClientId == shooterClientId)
            {
                Debug.Log("[Projectile] Dost ateşi engellendi");
                return;
            }
        }

        // Handle damage
        BaseEnemy enemy = collision.gameObject.GetComponent<BaseEnemy>();
        if (enemy != null)
        {
            Debug.Log($"[Projectile] Düşmana hasar veriliyor - Hasar: {damage}");
            enemy.TakeDamage(damage);
            DespawnProjectile();
        }
        else if (!collision.gameObject.CompareTag("Projectile"))
        {
            Debug.Log("[Projectile] Hedef düşman değil, mermi yok ediliyor");
            DespawnProjectile();
        }
    }

    [ClientRpc]
    private void SpawnHitEffectClientRpc(Vector3 hitPoint)
    {
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, hitPoint, Quaternion.identity);
            Destroy(effect, 2f); // 2 saniye sonra efekti yok et
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