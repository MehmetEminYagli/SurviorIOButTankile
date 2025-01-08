using UnityEngine;

public interface ITurretRotation
{
    void UpdateRotation();
    void SetTurretTransform(Transform turretTransform);
    Transform GetClosestEnemy();
} 