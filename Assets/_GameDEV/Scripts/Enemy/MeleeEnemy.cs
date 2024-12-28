using UnityEngine;

public class MeleeEnemy : BaseEnemy
{
    protected override void Awake()
    {
        base.Awake();
        attackStrategy = GetComponent<MeleeAttackStrategy>();
        if (attackStrategy == null)
        {
            attackStrategy = gameObject.AddComponent<MeleeAttackStrategy>();
        }

        // Set default values for melee enemy
        stoppingDistance = 2f;
        attackRange = 2.5f;
        accuracy = 0.9f;
        moveSpeed = 7f;
        agent.stoppingDistance = stoppingDistance;
        agent.speed = moveSpeed;
    }
} 