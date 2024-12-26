using UnityEngine;

public interface IProjectileStrategy
{
    void InitializeProjectile(GameObject projectile, Vector3 startPosition, Vector3 direction);
    void UpdateProjectileMovement(GameObject projectile);
}