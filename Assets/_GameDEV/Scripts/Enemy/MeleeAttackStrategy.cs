using UnityEngine;

public class MeleeAttackStrategy : MonoBehaviour, IAttackStrategy
{
    [SerializeField] private float damage = 10f;
    [SerializeField] private float attackRadius = 1.5f;

    public void Attack(Transform target, float accuracy)
    {
        if (target == null) return;

        // Check if target is within melee range
        float distance = Vector3.Distance(transform.position, target.position);
        if (distance <= attackRadius)
        {
            // Apply damage based on accuracy
            float actualDamage = damage * accuracy;
            
            // Get health component and apply damage
            IHealth targetHealth = target.GetComponent<IHealth>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(actualDamage);
            }
        }
    }
} 