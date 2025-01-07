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
    private bool isQuitting = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void OnEnable()
    {
        isQuitting = false;
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
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
        if (!IsServer || collision == null || collision.gameObject == null || isQuitting) return;

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
        NetworkHealthComponent healthComponent = collision.gameObject.GetComponent<NetworkHealthComponent>();
        if (healthComponent != null)
        {
            Debug.Log($"[Projectile] Hedefe hasar veriliyor - Hasar: {damage}");
            healthComponent.TakeDamage(damage);
            DespawnProjectile();
            return;
        }

        BaseEnemy enemy = collision.gameObject.GetComponent<BaseEnemy>();
        if (enemy != null)
        {
            Debug.Log($"[Projectile] Düşmana hasar veriliyor - Hasar: {damage}");
            enemy.TakeDamage(damage);
            DespawnProjectile();
            return;
        }

        // Eğer çarpılan obje mermi değilse mermiyi yok et
        try
        {
            if (!collision.gameObject.CompareTag("Projectile"))
            {
                Debug.Log("[Projectile] Hedef mermi değil, mermi yok ediliyor");
                DespawnProjectile();
            }
        }
        catch (UnityEngine.UnityException)
        {
            // Tag bulunamadıysa mermiyi yok et
            Debug.Log("[Projectile] Tag kontrolünde hata, mermi yok ediliyor");
            DespawnProjectile();
        }
    }

    [ClientRpc]
    private void SpawnHitEffectClientRpc(Vector3 hitPoint)
    {
        if (hitEffectPrefab != null && !isQuitting)
        {
            GameObject effect = Instantiate(hitEffectPrefab, hitPoint, Quaternion.identity);
            Destroy(effect, 2f); // 2 saniye sonra efekti yok et
        }
    }

    private void DespawnProjectile()
    {
        if (IsServer && !isQuitting)
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }
    }
} 