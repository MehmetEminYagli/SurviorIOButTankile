using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class EnemyProjectile : NetworkBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float projectileSpeed = 15f;
    [SerializeField] private bool rotateTowardsVelocity = true;
    [SerializeField] private float rotationSpeed = 15f;

    private Rigidbody rb;
    private NetworkTransform netTransform;
    private Vector3 moveDirection;
    private bool isInitialized = false;
    private NetworkVariable<bool> shouldDestroy = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    private NetworkVariable<Vector3> targetPosition = new NetworkVariable<Vector3>(default, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);
    private NetworkVariable<Vector3> currentVelocity = new NetworkVariable<Vector3>(default,
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        netTransform = GetComponent<NetworkTransform>();
        
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;

        ConfigureNetworkTransform();
    }

    private void ConfigureNetworkTransform()
    {
        if (netTransform != null)
        {
            netTransform.InLocalSpace = false;
            netTransform.Interpolate = true;
            netTransform.SyncPositionX = true;
            netTransform.SyncPositionY = true;
            netTransform.SyncPositionZ = true;
            netTransform.SyncRotAngleX = true;
            netTransform.SyncRotAngleY = true;
            netTransform.SyncRotAngleZ = true;
            netTransform.UseQuaternionSynchronization = true;
            netTransform.UseQuaternionCompression = false;
            netTransform.UseHalfFloatPrecision = false;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Invoke(nameof(DestroyProjectile), lifeTime);
        }

        shouldDestroy.OnValueChanged += OnShouldDestroyChanged;
        targetPosition.OnValueChanged += OnTargetPositionChanged;
        currentVelocity.OnValueChanged += OnVelocityChanged;
    }

    public override void OnNetworkDespawn()
    {
        shouldDestroy.OnValueChanged -= OnShouldDestroyChanged;
        targetPosition.OnValueChanged -= OnTargetPositionChanged;
        currentVelocity.OnValueChanged -= OnVelocityChanged;
    }

    private void OnVelocityChanged(Vector3 prev, Vector3 next)
    {
        if (!IsServer)
        {
            rb.linearVelocity = next;
        }
    }

    private void OnTargetPositionChanged(Vector3 prev, Vector3 next)
    {
        if (!isInitialized)
        {
            moveDirection = (next - transform.position).normalized;
            Vector3 velocity = moveDirection * projectileSpeed;
            rb.linearVelocity = velocity;
            
            if (IsServer)
            {
                currentVelocity.Value = velocity;
            }
            
            if (rotateTowardsVelocity)
            {
                transform.rotation = Quaternion.LookRotation(moveDirection);
            }
            
            isInitialized = true;
        }
    }

    private void OnShouldDestroyChanged(bool prev, bool next)
    {
        if (next)
        {
            if (IsServer)
            {
                NetworkObject.Despawn(true);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    private void DestroyProjectile()
    {
        if (!IsServer) return;
        shouldDestroy.Value = true;
    }

    public void Initialize(Vector3 target)
    {
        if (!IsServer) return;

        targetPosition.Value = target;
        moveDirection = (target - transform.position).normalized;
        Vector3 velocity = moveDirection * projectileSpeed;
        rb.linearVelocity = velocity;
        currentVelocity.Value = velocity;
        isInitialized = true;

        if (rotateTowardsVelocity)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }

        ForceVisibilityClientRpc();
    }

    [ClientRpc]
    private void ForceVisibilityClientRpc()
    {
        if (!IsOwner)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.enabled = true;
            }
            
            if (rb != null)
            {
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
        }
    }

    private void FixedUpdate()
    {
        if (!isInitialized) return;

        if (IsServer)
        {
            currentVelocity.Value = rb.linearVelocity;
        }
        else
        {
            rb.linearVelocity = currentVelocity.Value;
        }

        if (rotateTowardsVelocity)
        {
            Vector3 velocityDirection = (IsServer ? rb.linearVelocity : currentVelocity.Value).normalized;
            if (velocityDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(velocityDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        IHealth health = collision.gameObject.GetComponent<IHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
        }
        
        DestroyProjectile();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        IHealth health = other.GetComponent<IHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
            DestroyProjectile();
        }
        else if (other.gameObject.layer != gameObject.layer)
        {
            DestroyProjectile();
        }
    }
} 