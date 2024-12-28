using UnityEngine;

public class EnemyFactory : MonoBehaviour
{
    [SerializeField] private GameObject rangedEnemyPrefab;
    [SerializeField] private GameObject meleeEnemyPrefab;

    public enum EnemyType
    {
        Ranged,
        Melee
    }

    public GameObject CreateEnemy(EnemyType type, Vector3 position)
    {
        GameObject prefab = type switch
        {
            EnemyType.Ranged => rangedEnemyPrefab,
            EnemyType.Melee => meleeEnemyPrefab,
            _ => null
        };

        if (prefab == null) return null;

        GameObject enemy = Instantiate(prefab, position, Quaternion.identity);
        IEnemy enemyComponent = enemy.GetComponent<IEnemy>();
        if (enemyComponent != null)
        {
            enemyComponent.Initialize(position);
        }

        return enemy;
    }
} 