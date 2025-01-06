using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using DG.Tweening;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NetworkObject))]
public abstract class BaseEnemy : NetworkBehaviour, IEnemy
{
    [Header("Base Stats")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float moveSpeed = 5f;
    [SerializeField] protected float stoppingDistance = 5f;
    [SerializeField] protected float attackRange = 10f;
    [SerializeField] protected float attackCooldown = 2f;
    [SerializeField] protected float accuracy = 0.8f;
    
    [Header("Death Effects")]
    [SerializeField] protected GameObject deathEffectPrefab;
    [SerializeField] protected float fadeOutDuration = 1f;
    [SerializeField] protected float deathEffectDuration = 2f;
    
    [Header("Movement Settings")]
    [SerializeField] protected float rotationSpeed = 5f;
    [SerializeField] protected float updatePathInterval = 0.1f;
    [SerializeField] protected float accelerationSpeed = 8f;
    [SerializeField] protected float turnDampening = 0.5f;
    [SerializeField] protected float movementSmoothing = 0.1f;
    [SerializeField] protected float pathEndThreshold = 0.5f;

    protected NetworkVariable<float> currentHealth = new NetworkVariable<float>();
    protected Transform target;
    protected NavMeshAgent agent;
    protected IAttackStrategy attackStrategy;
    protected float lastAttackTime;
    protected float nextPathUpdate;
    protected MeshRenderer meshRenderer;
    
    protected Vector3 currentVelocity;
    protected Vector3 smoothDampVelocity;
    protected float currentSpeed;
    protected float velocitySmoothing;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (agent != null)
        {
            ConfigureAgent();
        }
        velocitySmoothing = movementSmoothing;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
    }

    protected virtual void ConfigureAgent()
    {
        agent.stoppingDistance = stoppingDistance;
        agent.speed = moveSpeed;
        agent.angularSpeed = rotationSpeed * 100;
        agent.acceleration = accelerationSpeed;
        agent.autoBraking = true;
        agent.updatePosition = true;
        agent.updateRotation = false; // Rotasyonu manuel kontrol edeceğiz
        agent.updateUpAxis = false;
        agent.radius = 0.5f; // Çarpışma yarıçapı
        agent.height = 2f; // Karakter yüksekliği
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.areaMask = NavMesh.AllAreas;
        agent.autoTraverseOffMeshLink = true;
    }

    public virtual void Initialize(Vector3 spawnPosition)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(spawnPosition, out hit, 10f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
        }
        else
        {
            Debug.LogWarning("Could not find valid NavMesh position near " + spawnPosition);
            transform.position = spawnPosition;
        }
        currentHealth.Value = maxHealth;
        nextPathUpdate = 0f;
        currentSpeed = 0f;
        currentVelocity = Vector3.zero;
        smoothDampVelocity = Vector3.zero;
    }

    public virtual void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            UpdatePath(); // Hedef değiştiğinde yolu hemen güncelle
        }
    }

    protected virtual void UpdatePath()
    {
        if (agent.isActiveAndEnabled && target != null)
        {
            agent.SetDestination(target.position);
        }
    }

    public virtual void Move()
    {
        if (target == null || agent == null) return;
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning("Agent is not on NavMesh!", gameObject);
            return;
        }

        // Yol güncelleme
        if (Time.time >= nextPathUpdate)
        {
            UpdatePath();
            nextPathUpdate = Time.time + updatePathInterval;
        }

        // Hedef mesafe kontrolü
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        if (distanceToTarget <= stoppingDistance)
        {
            currentSpeed = Mathf.SmoothDamp(currentSpeed, 0, ref velocitySmoothing, movementSmoothing);
            agent.velocity = Vector3.zero;
            return;
        }

        // Hız hesaplama ve yumuşatma
        Vector3 desiredVelocity = agent.desiredVelocity;
        float targetSpeed = agent.desiredVelocity.magnitude;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref velocitySmoothing, movementSmoothing);
        
        // Hareket yönü yumuşatma
        currentVelocity = Vector3.SmoothDamp(
            currentVelocity,
            desiredVelocity.normalized * currentSpeed,
            ref smoothDampVelocity,
            movementSmoothing
        );
        
        // Hareketi uygula
        if (currentVelocity.magnitude > 0.1f)
        {
            // Yumuşak dönüş
            Quaternion targetRotation = Quaternion.LookRotation(currentVelocity.normalized);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.fixedDeltaTime * rotationSpeed
            );

            // Pozisyonu güncelle
            if (!agent.isStopped)
            {
                agent.velocity = currentVelocity;
            }
        }
    }

    public virtual void Attack()
    {
        if (target == null || Time.time - lastAttackTime < attackCooldown) return;

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        if (distanceToTarget <= attackRange)
        {
            // Saldırı sırasında hedefe yumuşak dönüş
            Vector3 directionToTarget = (target.position - transform.position).normalized;
            if (directionToTarget != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.deltaTime * rotationSpeed * turnDampening
                );
            }

            attackStrategy.Attack(target, accuracy);
            lastAttackTime = Time.time;
        }
    }

    public virtual void TakeDamage(float damage)
    {
        if (!IsServer) return;

        Debug.Log($"[Enemy] Hasar alındı - Hasar: {damage}, Mevcut Can: {currentHealth.Value}");
        
        float newHealth = Mathf.Max(0, currentHealth.Value - damage);
        currentHealth.Value = newHealth;
        
        // Health Component'e hasarı bildir
        HealthComponent healthComp = GetComponent<HealthComponent>();
        if (healthComp != null)
        {
            healthComp.TakeDamage(damage);
            Debug.Log($"[Enemy] Yeni can durumu - Current: {currentHealth.Value}, Max: {maxHealth}");
        }

        // Health bar'ı güncelle (ClientRpc ile tüm clientlara bildir)
        UpdateHealthBarClientRpc(currentHealth.Value);

        if (currentHealth.Value <= 0)
        {
            Die();
        }
    }

    [ClientRpc]
    private void UpdateHealthBarClientRpc(float newHealth)
    {
        // Health bar'ı güncelle
        EnemyHealthBar healthBar = GetComponent<EnemyHealthBar>();
        if (healthBar != null)
        {
            healthBar.UpdateHealthBar(newHealth);
        }
    }

    public virtual void Die()
    {
        if (!IsServer) return;

        // Spawn death effect for all clients
        SpawnDeathEffectClientRpc(transform.position);
        
        // Start fade out animation for all clients
        StartFadeOutClientRpc();

        // Disable NavMeshAgent safely
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
        else
        {
            // NavMeshAgent zaten deaktif veya NavMesh üzerinde değil
            if (agent != null)
            {
                agent.enabled = false;
            }
        }

        // Schedule destruction after fade out
        StartCoroutine(DestroyAfterDelay());
    }

    private System.Collections.IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(fadeOutDuration);
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }

    [ClientRpc]
    private void SpawnDeathEffectClientRpc(Vector3 position)
    {
        if (deathEffectPrefab != null)
        {
            GameObject effect = Instantiate(deathEffectPrefab, position, Quaternion.identity);
            Destroy(effect, deathEffectDuration);
        }
    }

    [ClientRpc]
    private void StartFadeOutClientRpc()
    {
        if (meshRenderer != null)
        {
            // Fade out the enemy using DOTween
            Material material = meshRenderer.material;
            Color startColor = material.color;
            material.DOFade(0f, fadeOutDuration).SetEase(Ease.InOutQuad);
        }
    }

    protected virtual void Update()
    {
        if (target != null)
        {
            Attack();
        }
    }

    protected virtual void FixedUpdate()
    {
        if (target != null)
        {
            Move();
        }
    }
} 