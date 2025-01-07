using UnityEngine;
using Unity.Netcode;

public class NetworkHealthComponent : HealthComponent
{
    private NetworkVariable<float> networkHealth = new NetworkVariable<float>(
        100f, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    protected override void Awake()
    {
        base.Awake();
        currentHealth = networkHealth.Value;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsServer)
        {
            networkHealth.Value = MaxHealth;
            currentHealth = MaxHealth;
        }
        else
        {
            currentHealth = networkHealth.Value;
        }

        // Client'lar sadece server'dan gelen değeri takip eder
        if (!IsServer)
        {
            networkHealth.OnValueChanged += OnHealthChangedFromServer;
        }
    }

    private void OnHealthChangedFromServer(float previousValue, float newValue)
    {
        if (IsServer) return; // Server zaten güncel
        
        currentHealth = newValue;
        onHealthChanged?.Invoke(currentHealth);
    }

    public override void TakeDamage(float damage)
    {
        if (!IsSpawned) return;

        if (IsServer)
        {
            ServerTakeDamage(damage);
        }
        else
        {
            TakeDamageServerRpc(damage);
        }
    }

    private void ServerTakeDamage(float damage)
    {
        if (!IsServer || currentHealth <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        networkHealth.Value = currentHealth;
        
        onHealthChanged?.Invoke(currentHealth);
        onDamageTaken?.Invoke(damage);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public override void Heal(float amount)
    {
        if (!IsSpawned) return;

        if (IsServer)
        {
            ServerHeal(amount);
        }
        else
        {
            HealServerRpc(amount);
        }
    }

    private void ServerHeal(float amount)
    {
        if (!IsServer || isDead) return;

        float healthBefore = currentHealth;
        currentHealth = Mathf.Min(MaxHealth, currentHealth + Mathf.Max(0, amount));
        
        if (currentHealth > healthBefore)
        {
            networkHealth.Value = currentHealth;
            onHealthChanged?.Invoke(currentHealth);
            onHealed?.Invoke(currentHealth - healthBefore);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(float damage)
    {
        ServerTakeDamage(damage);
    }

    [ServerRpc(RequireOwnership = false)]
    private void HealServerRpc(float amount)
    {
        ServerHeal(amount);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer)
        {
            networkHealth.OnValueChanged -= OnHealthChangedFromServer;
        }
        base.OnNetworkDespawn();
    }
} 