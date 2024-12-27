using UnityEngine;

public class ArrowProjectileStrategy : IProjectileStrategy
{
    private readonly float maxHeight =10f;
    private readonly float projectileSpeed = 20f;
    private readonly float arcHeight = 2f;

    private Vector3 startPos;
    private Vector3 targetPos;
    private float journeyLength;
    private float startTime;
    private bool isMoving = true;

    public void InitializeProjectile(GameObject projectile, Vector3 startPosition, Vector3 direction)
    {
        startPos = startPosition;
        targetPos = startPosition + direction * 5f; // Mesafeyi artýrdým
        targetPos.y = 0;

        startPos.y += maxHeight;
        journeyLength = Vector3.Distance(startPos, targetPos);
        startTime = Time.time;
        isMoving = true;

        projectile.transform.position = startPos;
        Vector3 initialDirection = (targetPos - startPos).normalized;
        projectile.transform.rotation = Quaternion.LookRotation(initialDirection);

        // Rigidbody ayarlarýný güncelle
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }

    public void UpdateProjectileMovement(GameObject projectile)
    {
        if (!isMoving) return;

        float distanceCovered = (Time.time - startTime) * projectileSpeed;
        float fractionOfJourney = Mathf.Clamp01(distanceCovered / journeyLength);

        Vector3 currentPos = Vector3.Lerp(startPos, targetPos, fractionOfJourney);
        float parabola = Mathf.Sin(fractionOfJourney * Mathf.PI) * arcHeight;
        currentPos.y += parabola;

        projectile.transform.position = currentPos;

        // Hareket yönüne göre rotasyonu güncelle
        Vector3 direction = (currentPos - projectile.transform.position).normalized;
        if (direction != Vector3.zero)
        {
            projectile.transform.rotation = Quaternion.LookRotation(direction);
        }

        // Yere çok yakýnsa hareketi durdur
        if (fractionOfJourney >= 0.99f)
        {
            isMoving = false;
        }
    }
}