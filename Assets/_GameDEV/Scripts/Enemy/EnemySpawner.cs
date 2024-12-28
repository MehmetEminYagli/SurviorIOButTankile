using UnityEngine;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemyFactory enemyFactory;
    [SerializeField] private int maxEnemies = 10;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float minSpawnDistance = 20f;
    [SerializeField] private float maxSpawnDistance = 30f;
    [SerializeField] private Transform[] players;  // Assign player transforms in inspector
    [SerializeField] private LayerMask groundLayer; // Ground layer for spawn check
    [SerializeField] private float maxRayDistance = 50f; // Maximum distance for ground check
    [SerializeField] private int maxSpawnAttempts = 5; // Maximum attempts to find valid spawn position

    private Camera mainCamera;
    private float lastSpawnTime;
    private List<GameObject> activeEnemies = new List<GameObject>();

    private void Start()
    {
        mainCamera = Camera.main;
        if (enemyFactory == null)
        {
            enemyFactory = GetComponent<EnemyFactory>();
        }
    }

    private void Update()
    {
        // Check if we can spawn more enemies
        if (Time.time - lastSpawnTime >= spawnInterval && activeEnemies.Count < maxEnemies)
        {
            SpawnEnemy();
            lastSpawnTime = Time.time;
        }

        // Clean up destroyed enemies from the list
        activeEnemies.RemoveAll(enemy => enemy == null);
    }

    private void SpawnEnemy()
    {
        Vector3? validSpawnPosition = GetValidSpawnPosition();
        
        if (!validSpawnPosition.HasValue)
        {
            Debug.LogWarning("Could not find valid spawn position after maximum attempts");
            return;
        }

        // Randomly choose enemy type
        EnemyFactory.EnemyType enemyType = Random.value < 0.7f ? 
            EnemyFactory.EnemyType.Ranged : EnemyFactory.EnemyType.Melee;

        GameObject enemy = enemyFactory.CreateEnemy(enemyType, validSpawnPosition.Value);
        if (enemy != null)
        {
            activeEnemies.Add(enemy);
            
            // Assign nearest player as target
            Transform nearestPlayer = FindNearestPlayer(validSpawnPosition.Value);
            if (nearestPlayer != null)
            {
                IEnemy enemyComponent = enemy.GetComponent<IEnemy>();
                if (enemyComponent != null)
                {
                    enemyComponent.SetTarget(nearestPlayer);
                }
            }
        }
    }

    private Vector3? GetValidSpawnPosition()
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // Get random position outside camera view
            Vector3 randomDirection = Random.insideUnitCircle.normalized;
            float randomDistance = Random.Range(minSpawnDistance, maxSpawnDistance);
            Vector3 spawnPosition = mainCamera.transform.position + new Vector3(randomDirection.x, 0, randomDirection.y) * randomDistance;

            // Raycast from above to find ground
            RaycastHit hit;
            Vector3 rayStart = spawnPosition + Vector3.up * maxRayDistance;
            if (Physics.Raycast(rayStart, Vector3.down, out hit, maxRayDistance * 2f, groundLayer))
            {
                // Check if position is on NavMesh
                UnityEngine.AI.NavMeshHit navHit;
                if (UnityEngine.AI.NavMesh.SamplePosition(hit.point, out navHit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    return navHit.position;
                }
            }
        }

        return null;
    }

    private Transform FindNearestPlayer(Vector3 position)
    {
        if (players == null || players.Length == 0) return null;

        Transform nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Transform player in players)
        {
            if (player == null) continue;

            float distance = Vector3.Distance(position, player.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = player;
            }
        }

        return nearest;
    }
} 