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

    private void Awake()
    {
        ValidateMaterialRenderers();
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

    public void ApplyMaterialByIndex(int materialIndex)
    {
        var material = GetMaterialByIndex(materialIndex);
        if (material == null) return;

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

    public int GetMaterialCount()
    {
        return materialsData != null ? materialsData.availableMaterials.Count : 0;
    }
} 