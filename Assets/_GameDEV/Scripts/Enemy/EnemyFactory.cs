using UnityEngine;
using Unity.Netcode;

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
        GameObject prefab = null;
        switch (type)
        {
            case EnemyType.Ranged:
                prefab = rangedEnemyPrefab;
                break;
            case EnemyType.Melee:
                prefab = meleeEnemyPrefab;
                break;
        }

        if (prefab == null)
        {
            Debug.LogError($"Enemy prefab for type {type} is not assigned!");
            return null;
        }

        // Prefab'ın NetworkObject bileşenine sahip olduğunu kontrol et
        if (prefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogError($"Enemy prefab {prefab.name} does not have a NetworkObject component!");
            return null;
        }

        Debug.Log($"Creating enemy of type {type} at position {position}");
        GameObject enemy = Instantiate(prefab, position, Quaternion.identity);
        
        // NetworkObject'i al ve ayarlarını kontrol et
        NetworkObject networkObject = enemy.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            Debug.Log($"NetworkObject found on enemy {enemy.name}");
            return enemy;
        }
        else
        {
            Debug.LogError($"NetworkObject component missing on instantiated enemy {enemy.name}");
            Destroy(enemy);
            return null;
        }
    }
} 