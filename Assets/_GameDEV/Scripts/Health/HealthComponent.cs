using UnityEngine;
using UnityEngine.Events;

public class HealthComponent : MonoBehaviour, IHealth
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private float destroyDelay = 0f;

    [Header("Events")]
    public UnityEvent<float> onHealthChanged;
    public UnityEvent<float> onDamageTaken;
    public UnityEvent<float> onHealed;
    public UnityEvent onDeath;

    private float currentHealth;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (currentHealth <= 0) return;

        Debug.Log($"[HealthComponent] Hasar alındı - Hasar: {damage}, Önceki Can: {currentHealth}");
        
        currentHealth = Mathf.Max(0, currentHealth - damage);
        
        // Event'leri tetikle
        onHealthChanged?.Invoke(currentHealth);
        onDamageTaken?.Invoke(damage);

        Debug.Log($"[HealthComponent] Yeni can durumu: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        float healthBefore = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0, amount));
        float actualHealAmount = currentHealth - healthBefore;

        if (actualHealAmount > 0)
        {
            onHealthChanged?.Invoke(currentHealth);
            onHealed?.Invoke(actualHealAmount);
        }
    }

    public void Die()
    {
        Debug.Log("[HealthComponent] Ölüm gerçekleşti");
        onDeath?.Invoke();

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    // Helper method to reset health
    public void ResetHealth()
    {
        isDead = false;
        currentHealth = maxHealth;
        onHealthChanged?.Invoke(currentHealth);
    }
} 