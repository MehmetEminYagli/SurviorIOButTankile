using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class EnemyHealthBar : NetworkBehaviour
{
    [SerializeField] private Image healthBarFill;
    [SerializeField] private float heightOffset = 2f;
    [SerializeField] private Canvas canvas;
    
    private Camera mainCamera;
    private HealthComponent healthComponent;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log("[HealthBar] OnNetworkSpawn çağrıldı");
        Initialize();
    }

    private void Initialize()
    {
        if (healthComponent == null)
        {
            healthComponent = GetComponent<HealthComponent>();
            Debug.Log($"[HealthBar] HealthComponent bulundu: {healthComponent != null}");
        }
            
        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>();
            Debug.Log($"[HealthBar] Canvas bulundu: {canvas != null}");
        }
            
        if (healthBarFill == null)
        {
            healthBarFill = GetComponentInChildren<Image>();
            Debug.Log($"[HealthBar] HealthBarFill bulundu: {healthBarFill != null}");
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            Debug.Log($"[HealthBar] MainCamera bulundu: {mainCamera != null}");
        }

        if (canvas != null && mainCamera != null)
            canvas.worldCamera = mainCamera;

        if (healthComponent != null)
        {
            healthComponent.onHealthChanged.RemoveListener(UpdateHealthBar);
            healthComponent.onHealthChanged.AddListener(UpdateHealthBar);
            
            Debug.Log($"[HealthBar] İlk health değeri: {healthComponent.CurrentHealth}");
            UpdateHealthBar(healthComponent.CurrentHealth);
        }
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void OnDisable()
    {
        if (healthComponent != null)
            healthComponent.onHealthChanged.RemoveListener(UpdateHealthBar);
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (canvas != null)
                canvas.worldCamera = mainCamera;
            return;
        }

        if (canvas != null)
        {
            canvas.transform.LookAt(mainCamera.transform.position);
            canvas.transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);
            
            canvas.transform.position = transform.position + Vector3.up * heightOffset;
        }
    }

    public void UpdateHealthBar(float currentHealth)
    {
        if (healthBarFill == null || healthComponent == null)
        {
            Debug.LogWarning($"[HealthBar] Eksik referans - HealthBarFill: {healthBarFill != null}, HealthComponent: {healthComponent != null}");
            return;
        }

        float fillAmount = currentHealth / healthComponent.MaxHealth;
        fillAmount = Mathf.Clamp01(fillAmount);
        healthBarFill.fillAmount = fillAmount;

        Debug.Log($"[HealthBar] Health güncellendi - Current: {currentHealth}, Max: {healthComponent.MaxHealth}, Fill: {fillAmount}");
    }
}