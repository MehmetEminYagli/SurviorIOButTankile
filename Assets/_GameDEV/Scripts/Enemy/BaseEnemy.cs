using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public abstract class BaseEnemy : MonoBehaviour, IEnemy
{
    [Header("Base Stats")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float moveSpeed = 5f;
    [SerializeField] protected float stoppingDistance = 5f;
    [SerializeField] protected float attackRange = 10f;
    [SerializeField] protected float attackCooldown = 2f;
    [SerializeField] protected float accuracy = 0.8f;
    
    [Header("Movement Settings")]
    [SerializeField] protected float rotationSpeed = 5f;
    [SerializeField] protected float updatePathInterval = 0.1f;
    [SerializeField] protected float accelerationSpeed = 8f; // Hızlanma oranı
    [SerializeField] protected float turnDampening = 0.5f; // Dönüş yumuşatma
    [SerializeField] protected float movementSmoothing = 0.1f; // Hareket yumuşatma
    [SerializeField] protected float pathEndThreshold = 0.5f; // Hedef noktaya yakınlık eşiği

    protected float currentHealth;
    protected Transform target;
    protected NavMeshAgent agent;
    protected IAttackStrategy attackStrategy;
    protected float lastAttackTime;
    protected float nextPathUpdate;
    
    protected Vector3 currentVelocity;
    protected Vector3 smoothDampVelocity;
    protected float currentSpeed;
    protected float velocitySmoothing;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        currentHealth = maxHealth;
        if (agent != null)
        {
            ConfigureAgent();
        }
        velocitySmoothing = movementSmoothing;
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
        currentHealth = maxHealth;
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
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public virtual void Die()
    {
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
        Destroy(gameObject);
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