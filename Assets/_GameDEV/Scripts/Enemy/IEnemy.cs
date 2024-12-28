using UnityEngine;

public interface IEnemy
{
    void Initialize(Vector3 spawnPosition);
    void Move();
    void Attack();
    void TakeDamage(float damage);
    void Die();
    void SetTarget(Transform target);
} 