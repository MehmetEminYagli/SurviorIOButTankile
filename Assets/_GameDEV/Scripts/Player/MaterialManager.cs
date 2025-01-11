using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class MaterialRendererData
{
    public MeshRenderer meshRenderer;
    public int materialIndex;
}

public interface IMaterialApplier
{
    void ApplyMaterial(Material material, int rendererIndex);
}

public class MaterialManager : MonoBehaviour
{
    [SerializeField] private PlayerMaterialsData materialsData;
    [SerializeField] private List<MaterialRendererData> materialRenderers = new List<MaterialRendererData>();
    private Material currentMaterial;
    private int currentMaterialIndex = 0;

    private void Awake()
    {
        ValidateMaterialRenderers();
        // İlk materyal varsa onu currentMaterial olarak ayarla
        if (materialsData != null && materialsData.availableMaterials.Count > 0)
        {
            currentMaterial = materialsData.availableMaterials[0].material;
        }
    }

    private void ValidateMaterialRenderers()
    {
        foreach (var rendererData in materialRenderers)
        {
            if (rendererData.meshRenderer == null)
            {
                Debug.LogError("One of the MeshRenderers in MaterialManager is not assigned!");
                continue;
            }

            if (rendererData.materialIndex >= rendererData.meshRenderer.materials.Length)
            {
                Debug.LogError($"Material index {rendererData.materialIndex} is out of range for MeshRenderer {rendererData.meshRenderer.name} which has {rendererData.meshRenderer.materials.Length} materials!");
            }
        }
    }

    public void AddMaterialRenderer(MeshRenderer renderer, int materialIndex)
    {
        if (renderer == null)
        {
            Debug.LogError("Cannot add null MeshRenderer!");
            return;
        }

        if (materialIndex < 0 || materialIndex >= renderer.materials.Length)
        {
            Debug.LogError($"Invalid material index {materialIndex} for renderer {renderer.name}!");
            return;
        }

        materialRenderers.Add(new MaterialRendererData 
        { 
            meshRenderer = renderer, 
            materialIndex = materialIndex 
        });
    }

    public Material GetMaterialByIndex(int index)
    {
        if (materialsData != null && index >= 0 && index < materialsData.availableMaterials.Count)
        {
            return materialsData.availableMaterials[index].material;
        }
        return null;
    }

    public Color GetPreviewColorByIndex(int index)
    {
        if (materialsData != null && index >= 0 && index < materialsData.availableMaterials.Count)
        {
            return materialsData.availableMaterials[index].previewColor;
        }
        return Color.white;
    }

    public void ApplyMaterialByIndex(int index)
    {
        if (materialsData == null || materialsData.availableMaterials == null || 
            materialsData.availableMaterials.Count == 0)
        {
            Debug.LogError("No materials data available!");
            return;
        }

        if (index < 0 || index >= materialsData.availableMaterials.Count)
        {
            Debug.LogError($"Material index {index} is out of range!");
            return;
        }

        currentMaterialIndex = index;
        Material material = materialsData.availableMaterials[index].material;
        ApplyMaterial(material);
    }

    public int GetMaterialCount()
    {
        return materialsData != null ? materialsData.availableMaterials.Count : 0;
    }

    public Color GetCurrentMaterialColor()
    {
        Color resultColor = Color.white;

        // Eğer materialsData varsa ve içinde materyal varsa
        if (materialsData != null && materialsData.availableMaterials.Count > 0)
        {
            // Eğer currentMaterial seçilmişse
            if (currentMaterial != null)
            {
                // MaterialsData içinde currentMaterial'ı ara
                for (int i = 0; i < materialsData.availableMaterials.Count; i++)
                {
                    if (materialsData.availableMaterials[i].material == currentMaterial)
                    {
                        resultColor = materialsData.availableMaterials[i].previewColor;
                        Debug.Log($"GetCurrentMaterialColor: Found material in materialsData. Color: {resultColor}");
                        return resultColor;
                    }
                }
            }
            
            // Eğer currentMaterial null ise veya bulunamadıysa, aktif materyalin rengini al
            foreach (var rendererData in materialRenderers)
            {
                if (rendererData.meshRenderer != null && rendererData.meshRenderer.materials.Length > rendererData.materialIndex)
                {
                    Material activeMaterial = rendererData.meshRenderer.materials[rendererData.materialIndex];
                    if (activeMaterial != null)
                    {
                        if (activeMaterial.HasProperty("_Color"))
                        {
                            resultColor = activeMaterial.GetColor("_Color");
                            Debug.Log($"GetCurrentMaterialColor: Using _Color from active material: {resultColor}");
                        }
                        else if (activeMaterial.HasProperty("_BaseColor"))
                        {
                            resultColor = activeMaterial.GetColor("_BaseColor");
                            Debug.Log($"GetCurrentMaterialColor: Using _BaseColor from active material: {resultColor}");
                        }
                        return resultColor;
                    }
                }
            }
        }

        Debug.LogWarning($"GetCurrentMaterialColor: Using default color: {resultColor}");
        return resultColor;
    }

    public void ForceUpdateMaterials()
    {
        if (materialsData == null || materialsData.availableMaterials.Count == 0)
        {
            Debug.LogWarning("No materials data available!");
            return;
        }

        // Aktif renderer'ları bul
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("No renderers found!");
            return;
        }

        // Tüm renderer'ların materyallerini güncelle
        foreach (Renderer renderer in renderers)
        {
            if (renderer.sharedMaterial != null)
            {
                renderer.material.CopyPropertiesFromMaterial(renderer.sharedMaterial);
            }
        }

        Debug.Log($"Force updated materials for index: {currentMaterialIndex}");
    }

    private void ApplyMaterial(Material material)
    {
        if (material == null) return;

        currentMaterial = material; // Seçilen materyali güncelle

        foreach (var rendererData in materialRenderers)
        {
            if (rendererData.meshRenderer != null)
            {
                // Mevcut materials array'ini al
                Material[] materials = rendererData.meshRenderer.materials;
                
                // Sadece belirtilen indeksteki material'i değiştir
                if (rendererData.materialIndex < materials.Length)
                {
                    materials[rendererData.materialIndex] = material;
                    rendererData.meshRenderer.materials = materials;
                }
                else
                {
                    Debug.LogError($"Material index {rendererData.materialIndex} is out of range for renderer {rendererData.meshRenderer.name}!");
                }
            }
        }
    }
} 