using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class RangedEnemy : BaseEnemy
{
    [Header("Ranged Settings")]
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private float turnSpeed = 5f;

    [Header("Attack Settings")]
    [SerializeField] private float minAttackInterval = 3f;
    [SerializeField] private float maxAttackInterval = 5f;
    [SerializeField] private float firstShotDelay = 2f;

    private Quaternion targetRotation;
    private RangedAttackStrategy rangedStrategy;
    private Coroutine attackCoroutine;
    private bool isAttacking = false;

    protected override void Awake()
    {
        base.Awake();

        // Attack strategy'yi al veya oluştur
        rangedStrategy = GetComponent<RangedAttackStrategy>();
        if (rangedStrategy == null)
        {
            rangedStrategy = gameObject.AddComponent<RangedAttackStrategy>();
            Debug.Log("Added RangedAttackStrategy to " + gameObject.name);
        }
        attackStrategy = rangedStrategy;

        // Projectile spawn point kontrolü
        if (projectileSpawnPoint == null)
        {
            GameObject spawnPoint = new GameObject("ProjectileSpawnPoint");
            spawnPoint.transform.SetParent(transform);
            spawnPoint.transform.localPosition = Vector3.forward + Vector3.up;
            projectileSpawnPoint = spawnPoint.transform;
        }

        // RangedAttackStrategy'ye spawn point'i ata
        if (rangedStrategy != null)
        {
            rangedStrategy.SetProjectileSpawnPoint(projectileSpawnPoint);
        }

        // Varsayılan değerleri ayarla
        stoppingDistance = 10f;
        attackRange = 15f;
        accuracy = 0.7f;
        moveSpeed = 5f;
        
        // Agent ayarlarını güncelle
        if (agent != null)
        {
            agent.stoppingDistance = stoppingDistance;
            agent.speed = moveSpeed;
            agent.updateRotation = false;
        }
    }

    private void Start()
    {
        // Atış coroutine'ini başlat
        StartAttackSequence();
    }

    private void StartAttackSequence()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
        }
        attackCoroutine = StartCoroutine(AttackSequence());
    }

    private IEnumerator AttackSequence()
    {
        // İlk atış için bekle
        yield return new WaitForSeconds(firstShotDelay);

        while (true)
        {
            if (target != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, target.position);
                if (distanceToTarget <= attackRange)
                {
                    // Hedefe yeterince dönmüş müyüz kontrol et
                    Vector3 directionToTarget = (target.position - transform.position).normalized;
                    float angle = Vector3.Angle(transform.forward, directionToTarget);
                    
                    // Eğer hedefle aramızdaki açı 30 dereceden azsa ateş et
                    if (angle < 30f && !isAttacking)
                    {
                        isAttacking = true;
                        Attack();
                        
                        // Rastgele bir süre bekle
                        float waitTime = Random.Range(minAttackInterval, maxAttackInterval);
                        yield return new WaitForSeconds(waitTime);
                        isAttacking = false;
                    }
                }
            }
            // Her frame'de kontrol etmek yerine kısa bir süre bekle
            yield return new WaitForSeconds(0.1f);
        }
    }

    protected override void Update()
    {
        if (target != null)
        {
            LookAtTarget();
        }
    }

    private void LookAtTarget()
    {
        if (target == null) return;

        // Hedef yönünü hesapla
        Vector3 directionToTarget = (target.position - transform.position).normalized;
        directionToTarget.y = 0; // Y eksenindeki rotasyonu sıfırla (sadece yatayda dönüş)

        if (directionToTarget != Vector3.zero)
        {
            // Hedef rotasyonu hesapla
            targetRotation = Quaternion.LookRotation(directionToTarget);

            // Yumuşak dönüş uygula
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * turnSpeed
            );
        }
    }

    public override void Attack()
    {
        if (target == null) return;

        lastAttackTime = Time.time;
        if (rangedStrategy != null)
        {
            rangedStrategy.Attack(target, accuracy);
        }
    }

    public override void Move()
    {
        base.Move();
        
        if (target != null)
        {
            LookAtTarget();
        }
    }

    private void OnDisable()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
        }
    }
} 