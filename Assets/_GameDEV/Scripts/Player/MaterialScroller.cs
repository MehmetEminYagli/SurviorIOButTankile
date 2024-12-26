using UnityEngine;

public class MaterialScroller : MonoBehaviour, IMaterialScroller
{
    [SerializeField] private Material[] materials;
    [SerializeField] private float offsetSpeed = -0.01f;

    public void ScrollMaterial(Vector3 moveDirection)
    {
        foreach (var material in materials)
        {
            if (material != null)
            {
                Vector2 offset = material.mainTextureOffset;
                offset.y += Time.time * offsetSpeed * moveDirection.magnitude;
                material.mainTextureOffset = offset;
            }
        }
    }
}