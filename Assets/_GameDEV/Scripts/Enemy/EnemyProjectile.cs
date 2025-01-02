using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public class EnemyProjectile : NetworkBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float arcHeight = 2f;
    [SerializeField] private bool rotateTowardsVelocity = true;
    [SerializeField] private float rotationSpeed = 15f;

    private NetworkVariable<Vector3> networkStartPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Vector3> networkTargetPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<float> networkProjectileSpeed = new NetworkVariable<float>();
    private NetworkVariable<bool> networkIsInitialized = new NetworkVariable<bool>();

    private float totalTime;
    private float elapsedTime;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Destroy(gameObject, lifeTime);
        }

        networkIsInitialized.OnValueChanged += OnInitializedChanged;
    }

    public override void OnNetworkDespawn()
    {
        networkIsInitialized.OnValueChanged -= OnInitializedChanged;
    }

    private void OnInitializedChanged(bool previousValue, bool newValue)
    {
        if (newValue)
        {
            totalTime = Vector3.Distance(networkStartPosition.Value, networkTargetPosition.Value) / networkProjectileSpeed.Value;
            elapsedTime = 0f;
        }
    }

    public void Initialize(Vector3 start, Vector3 target, float speed)
    {
        if (!IsServer) return;

        networkStartPosition.Value = start;
        networkTargetPosition.Value = target;
        networkProjectileSpeed.Value = speed;
        networkIsInitialized.Value = true;
    }

    private void Update()
    {
        if (!networkIsInitialized.Value) return;

        elapsedTime += Time.deltaTime;
        float normalizedTime = elapsedTime / totalTime;

        if (normalizedTime >= 1f)
        {
            if (IsServer)
            {
                NetworkObject.Despawn(true);
            }
            return;
        }

        Vector3 currentPosition = Vector3.Lerp(networkStartPosition.Value, networkTargetPosition.Value, normalizedTime);
        float height = Mathf.Sin(normalizedTime * Mathf.PI) * arcHeight;
        currentPosition.y += height;

        transform.position = currentPosition;

        if (rotateTowardsVelocity)
        {
            Vector3 moveDirection;
            if (normalizedTime < 1f)
            {
                Vector3 nextPosition = Vector3.Lerp(networkStartPosition.Value, networkTargetPosition.Value, Mathf.Min(1f, normalizedTime + 0.1f));
                nextPosition.y += Mathf.Sin((normalizedTime + 0.1f) * Mathf.PI) * arcHeight;
                moveDirection = (nextPosition - transform.position).normalized;
            }
            else
            {
                moveDirection = (networkTargetPosition.Value - transform.position).normalized;
            }

            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        IHealth health = other.GetComponent<IHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
            NetworkObject.Despawn(true);
        }
        else if (!other.CompareTag("Enemy") && !other.CompareTag("Projectile"))
        {
            NetworkObject.Despawn(true);
        }
    }
} 