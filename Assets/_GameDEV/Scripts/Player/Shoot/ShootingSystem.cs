using UnityEngine;
using Unity.Netcode;

public class ShootingSystem : NetworkBehaviour, IShooter
{
    [Header("Spawn Settings")]
    [SerializeField] private NetworkObject projectilePrefab;

    [Header("Shooting Settings")]
    [SerializeField] private float shootCooldown = 0.5f;
    [SerializeField] private float projectileSpeed = 20f;

    private float lastShootTime;

    public bool CanShoot => Time.time - lastShootTime >= shootCooldown;

    public void Shoot(Vector3 direction, Vector3 spawnPosition)
    {
        if (!CanShoot) return;

        if (IsServer)
        {
            HandleShoot(direction, spawnPosition);
        }
        else if (IsClient)
        {
            ShootServerRpc(direction, spawnPosition);
        }

        // Local efektler için
        PlayShootEffectsLocally(direction, spawnPosition);
        lastShootTime = Time.time;
    }

    private void PlayShootEffectsLocally(Vector3 direction, Vector3 spawnPosition)
    {
        // Burada lokal efektler eklenebilir (ses, partikül vb.)
        // Bu metod hem client hem de server tarafında çalışır
    }

    [ServerRpc(RequireOwnership = false)]
    private void ShootServerRpc(Vector3 direction, Vector3 spawnPosition)
    {
        HandleShoot(direction, spawnPosition);
    }

    private void HandleShoot(Vector3 direction, Vector3 spawnPosition)
    {
        if (!IsServer) return;

        SpawnProjectile(direction, spawnPosition);
        ShootClientRpc(direction, spawnPosition);
    }

    [ClientRpc]
    private void ShootClientRpc(Vector3 direction, Vector3 spawnPosition)
    {
        if (IsServer) return; // Server zaten efektleri oynatmış olacak
        PlayShootEffectsLocally(direction, spawnPosition);
    }

    private void SpawnProjectile(Vector3 direction, Vector3 spawnPosition)
    {
        if (!IsServer) return;

        NetworkObject projectileInstance = Instantiate(projectilePrefab, spawnPosition, Quaternion.LookRotation(direction));
        
        if (projectileInstance != null)
        {
            NetworkProjectile networkProjectile = projectileInstance.GetComponent<NetworkProjectile>();
            if (networkProjectile != null)
            {
                projectileInstance.Spawn();
                networkProjectile.Initialize(direction.normalized * projectileSpeed);
                
                // Spawn olan oyuncunun collider'ını ignore et
                if (TryGetComponent<Collider>(out var playerCollider))
                {
                    Physics.IgnoreCollision(projectileInstance.GetComponent<Collider>(), playerCollider, true);
                }
            }
        }
        else
        {
            Debug.LogError("Failed to instantiate projectile!");
        }
    }
}