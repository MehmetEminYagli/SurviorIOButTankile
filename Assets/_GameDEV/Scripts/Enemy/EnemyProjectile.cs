using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float arcHeight = 2f;
    [SerializeField] private bool rotateTowardsVelocity = true;
    [SerializeField] private float rotationSpeed = 15f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float totalTime;
    private float elapsedTime;
    private Rigidbody rb;
    private bool isInitialized = false;
    private float projectileSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    public void Initialize(Vector3 target, float speed)
    {
        startPosition = transform.position;
        targetPosition = target;
        projectileSpeed = speed;

        float distance = Vector3.Distance(startPosition, targetPosition);
        totalTime = distance / speed;
        elapsedTime = 0f;

        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized) return;

        elapsedTime += Time.deltaTime;
        float normalizedTime = elapsedTime / totalTime;

        if (normalizedTime >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 currentPosition = Vector3.Lerp(startPosition, targetPosition, normalizedTime);

        float height = Mathf.Sin(normalizedTime * Mathf.PI) * arcHeight;
        currentPosition.y += height;

        transform.position = currentPosition;

        if (rotateTowardsVelocity)
        {
            Vector3 moveDirection;
            if (normalizedTime < 1f)
            {
                Vector3 nextPosition = Vector3.Lerp(startPosition, targetPosition, Mathf.Min(1f, normalizedTime + 0.1f));
                nextPosition.y += Mathf.Sin((normalizedTime + 0.1f) * Mathf.PI) * arcHeight;
                moveDirection = (nextPosition - transform.position).normalized;
            }
            else
            {
                moveDirection = (targetPosition - transform.position).normalized;
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
        IHealth health = other.GetComponent<IHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
            Destroy(gameObject);
        }
        else if (!other.CompareTag("Enemy") && !other.CompareTag("Projectile"))
        {
            Destroy(gameObject);
        }
    }
} 