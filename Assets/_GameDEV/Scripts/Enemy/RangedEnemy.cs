using UnityEngine;

public class RangedEnemy : BaseEnemy
{
    protected override void Awake()
    {
        base.Awake();
        attackStrategy = GetComponent<RangedAttackStrategy>();
        if (attackStrategy == null)
        {
            attackStrategy = gameObject.AddComponent<RangedAttackStrategy>();
        }

        // Set default values for ranged enemy
        stoppingDistance = 10f;
        attackRange = 15f;
        accuracy = 0.7f;
        agent.stoppingDistance = stoppingDistance;
    }
} 