using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float damage = 10f;

    private IProjectileStrategy _movementStrategy;
    private bool _hasCollided = false;

    public void Initialize(IProjectileStrategy strategy)
    {
        _movementStrategy = strategy;
        _hasCollided = false;

        // Rigidbody ayarlar�n� yap
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        // Collider ayarlar�n� yap
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = false;
        }

        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (_movementStrategy != null && !_hasCollided)
        {
            _movementStrategy.UpdateProjectileMovement(gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Player ile �arp��may� yoksay
        if (collision.gameObject.CompareTag("Player"))
        {
            return;
        }

        if (!_hasCollided)
        {
            _hasCollided = true;
            HandleCollision(collision);
        }
    }

    private void HandleCollision(Collision collision)
    {
        // Rigidbody'yi devre d��� b�rak
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Collider'� devre d��� b�rak
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        // Ok'u �arpma noktas�na sabitle
        transform.position = collision.contacts[0].point;

        // Y�zeye dik olacak �ekilde rotasyonu ayarla
        Vector3 normal = collision.contacts[0].normal;
        transform.rotation = Quaternion.LookRotation(-normal);

        Destroy(gameObject, 2f);
    }
}