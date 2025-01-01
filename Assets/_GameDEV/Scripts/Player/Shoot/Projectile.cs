using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float damage = 10f;

    private IProjectileStrategy _movementStrategy;
    private bool _hasCollided = false;
    private bool _isInitialized = false;

    public void Initialize(IProjectileStrategy strategy)
    {
        _movementStrategy = strategy;
        _hasCollided = false;
        _isInitialized = true;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (!_isInitialized || _hasCollided) return;

        if (_movementStrategy != null)
        {
            _movementStrategy.UpdateProjectileMovement(gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_hasCollided)
        {
            _hasCollided = true;
            HandleCollision(collision);
        }
    }

    private void HandleCollision(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player")) return;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        transform.position = collision.contacts[0].point;
        Vector3 normal = collision.contacts[0].normal;
        transform.rotation = Quaternion.LookRotation(-normal);

        Destroy(gameObject, 2f);
    }
}