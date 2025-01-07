using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class PlayerHealthBar : NetworkBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image healthFillImage;
    [SerializeField] private Canvas healthBarCanvas;
    [SerializeField] private float heightOffset = 2f; // Can barının karakter üzerindeki yüksekliği

    private NetworkHealthComponent healthComponent;
    private bool isQuitting = false;

    private void Awake()
    {
        healthComponent = GetComponent<NetworkHealthComponent>();
        if (healthComponent != null)
        {
            healthComponent.onHealthChanged.AddListener(UpdateHealthBar);
        }

        // Canvas'ı world space olarak ayarla ve pozisyonunu düzenle
        if (healthBarCanvas != null)
        {
            healthBarCanvas.renderMode = RenderMode.WorldSpace;
            healthBarCanvas.transform.localPosition = Vector3.up * heightOffset;
            
            // Canvas ölçeğini ayarla
            healthBarCanvas.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        }
    }

    private void OnEnable()
    {
        isQuitting = false;
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void LateUpdate()
    {
        if (!IsSpawned || healthBarCanvas == null || Camera.main == null || isQuitting) return;

        try
        {
            // Can barını her zaman kameraya dönük tut
            healthBarCanvas.transform.rotation = Camera.main.transform.rotation;
        }
        catch (System.Exception)
        {
            // Oyun kapanırken oluşabilecek hataları görmezden gel
        }
    }

    private void UpdateHealthBar(float currentHealth)
    {
        if (healthFillImage != null && healthComponent != null)
        {
            float healthPercentage = currentHealth / healthComponent.MaxHealth;
            healthFillImage.fillAmount = healthPercentage;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!isQuitting && healthComponent != null)
        {
            healthComponent.onHealthChanged.RemoveListener(UpdateHealthBar);
        }

        if (healthBarCanvas != null)
        {
            Destroy(healthBarCanvas.gameObject);
        }

        base.OnNetworkDespawn();
    }

    private void OnDestroy()
    {
        if (!isQuitting && healthComponent != null)
        {
            healthComponent.onHealthChanged.RemoveListener(UpdateHealthBar);
        }

        if (healthBarCanvas != null)
        {
            Destroy(healthBarCanvas.gameObject);
        }
    }
} 