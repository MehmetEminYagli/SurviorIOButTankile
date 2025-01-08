using UnityEngine;
using Unity.Netcode;

public class NetworkTurretRotation : NetworkBehaviour, ITurretRotation
{
    [Header("Turret Settings")]
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float detectionRange = 20f;
    [SerializeField] private LayerMask enemyLayer;

    private Transform turretTransform;
    private NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            networkRotation.Value = turretTransform != null ? turretTransform.rotation : Quaternion.identity;
        }
    }

    private void Update()
    {
        if (IsOwner)
        {
            UpdateRotation();
        }
        else
        {
            // Client tarafında smooth rotation
            if (turretTransform != null)
            {
                turretTransform.rotation = Quaternion.Lerp(turretTransform.rotation, networkRotation.Value, Time.deltaTime * rotationSpeed);
            }
        }
    }

    public void UpdateRotation()
    {
        if (turretTransform == null) return;

        Transform target = GetClosestEnemy();
        if (target != null)
        {
            Vector3 targetDirection = target.position - turretTransform.position;
            targetDirection.y = 0; // Y eksenini sıfırla (sadece yatay düzlemde dönüş)

            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            Quaternion newRotation = Quaternion.Lerp(turretTransform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

            // Server tarafında rotasyonu güncelle
            if (IsServer)
            {
                networkRotation.Value = newRotation;
            }

            // ServerRpc ile server'a bildir
            if (IsOwner && !IsServer)
            {
                UpdateTurretRotationServerRpc(newRotation);
            }

            turretTransform.rotation = newRotation;
        }
    }

    [ServerRpc]
    private void UpdateTurretRotationServerRpc(Quaternion newRotation)
    {
        networkRotation.Value = newRotation;
    }

    public Transform GetClosestEnemy()
    {
        Transform closestEnemy = null;
        float closestDistance = detectionRange;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRange, enemyLayer);
        
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.TryGetComponent<BaseEnemy>(out _))
            {
                float distance = Vector3.Distance(transform.position, hitCollider.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = hitCollider.transform;
                }
            }
        }

        return closestEnemy;
    }

    public void SetTurretTransform(Transform newTurretTransform)
    {
        turretTransform = newTurretTransform;
    }
} 