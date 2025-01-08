using UnityEngine;
using Unity.Netcode;

public class ShootingSystem : NetworkBehaviour, IShooter
{
    [Header("Spawn Settings")]
    [SerializeField] private NetworkObject projectilePrefab;
    [SerializeField] private Transform shootSpawnPoint; // Mermi çıkış noktası

    [Header("Shooting Settings")]
    [SerializeField] private float shootCooldown = 0.5f;
    [SerializeField] private float projectileSpeed = 20f;

    [Header("Turret Settings")]
    [SerializeField] private Transform turretTransform;
    
    private float lastShootTime;
    private ITurretRotation turretRotation;

    private void Awake()
    {
        turretRotation = GetComponent<ITurretRotation>();
        if (turretRotation == null)
        {
            turretRotation = gameObject.AddComponent<NetworkTurretRotation>();
        }
        
        if (turretTransform != null)
        {
            turretRotation.SetTurretTransform(turretTransform);
        }
        else
        {
            Debug.LogError("Turret transform is not assigned in ShootingSystem!");
        }

        if (shootSpawnPoint == null)
        {
            Debug.LogError("ShootSpawn point is not assigned in ShootingSystem!");
        }
    }

    public bool CanShoot => Time.time - lastShootTime >= shootCooldown;

    public void Shoot(Vector3 direction, Vector3 spawnPosition)
    {
        if (!CanShoot) return;

        // Turret'in baktığı yönü ve mermi çıkış noktasını kullan
        Vector3 actualSpawnPosition = shootSpawnPoint != null ? shootSpawnPoint.position : spawnPosition;
        Vector3 shootDirection = turretTransform != null ? turretTransform.forward : direction;

        if (IsServer)
        {
            HandleShoot(shootDirection, actualSpawnPosition);
        }
        else if (IsClient)
        {
            ShootServerRpc(shootDirection, actualSpawnPosition);
        }

        PlayShootEffectsLocally(shootDirection, actualSpawnPosition);
        lastShootTime = Time.time;
    }

    private void PlayShootEffectsLocally(Vector3 direction, Vector3 spawnPosition)
    {
        // Burada lokal efektler eklenebilir (ses, partikül vb.)
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
        if (IsServer) return;
        PlayShootEffectsLocally(direction, spawnPosition);
    }

    private void SpawnProjectile(Vector3 direction, Vector3 spawnPosition)
    {
        if (!IsServer) return;

        // Turret'in rotasyonunu kullanarak mermiyi oluştur
        Quaternion projectileRotation = turretTransform != null ? 
            turretTransform.rotation : 
            Quaternion.LookRotation(direction);

        NetworkObject projectileInstance = Instantiate(projectilePrefab, spawnPosition, projectileRotation);
        
        if (projectileInstance != null)
        {
            NetworkProjectile networkProjectile = projectileInstance.GetComponent<NetworkProjectile>();
            if (networkProjectile != null)
            {
                projectileInstance.Spawn();
                // Turret'in forward yönünü kullanarak mermi hızını ayarla
                networkProjectile.Initialize(direction.normalized * projectileSpeed);
                
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