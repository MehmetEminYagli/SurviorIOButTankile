using UnityEngine;

public class RangedAttackStrategy : MonoBehaviour, IAttackStrategy
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private float maxSpreadAngle = 30f;

    public void Attack(Transform target, float accuracy)
    {
        if (target == null) return;

        Vector3 direction = (target.position - transform.position).normalized;
        
        // Calculate spread based on accuracy (higher accuracy = less spread)
        float spread = maxSpreadAngle * (1f - accuracy);
        float randomSpread = Random.Range(-spread, spread);
        Quaternion rotation = Quaternion.Euler(0, randomSpread, 0) * Quaternion.LookRotation(direction);

        GameObject projectile = Instantiate(projectilePrefab, transform.position, rotation);
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = rotation * Vector3.forward * projectileSpeed;
        }

        Destroy(projectile, 5f); // Destroy projectile after 5 seconds if it doesn't hit anything
    }
} 