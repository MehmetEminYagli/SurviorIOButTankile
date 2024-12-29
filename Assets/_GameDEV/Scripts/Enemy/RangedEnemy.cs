using UnityEngine;

public class RangedEnemy : BaseEnemy
{
    [Header("Ranged Settings")]
    [SerializeField] private Transform projectileSpawnPoint; // Mermi çıkış noktası

    protected override void Awake()
    {
        base.Awake();

        // Attack strategy'yi al veya oluştur
        attackStrategy = GetComponent<RangedAttackStrategy>();
        if (attackStrategy == null)
        {
            attackStrategy = gameObject.AddComponent<RangedAttackStrategy>();
            Debug.Log("Added RangedAttackStrategy to " + gameObject.name);
        }

        // Projectile spawn point kontrolü
        if (projectileSpawnPoint == null)
        {
            // Eğer spawn point atanmamışsa, otomatik oluştur
            GameObject spawnPoint = new GameObject("ProjectileSpawnPoint");
            spawnPoint.transform.SetParent(transform);
            spawnPoint.transform.localPosition = Vector3.forward + Vector3.up; // Düşmanın önünde ve biraz yukarıda
            projectileSpawnPoint = spawnPoint.transform;
            Debug.Log("Created ProjectileSpawnPoint for " + gameObject.name);
        }

        // RangedAttackStrategy'ye spawn point'i ata
        var rangedStrategy = attackStrategy as RangedAttackStrategy;
        if (rangedStrategy != null)
        {
            var field = typeof(RangedAttackStrategy).GetField("projectileSpawnPoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(rangedStrategy, projectileSpawnPoint);
            }
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
        }
    }

    public override void Attack()
    {
        if (target == null || Time.time - lastAttackTime < attackCooldown) return;

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        if (distanceToTarget <= attackRange)
        {
           
            
            // Hedefe dön
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

            // Saldırıyı gerçekleştir
            attackStrategy.Attack(target, accuracy);
            lastAttackTime = Time.time;
        }
    }
} 