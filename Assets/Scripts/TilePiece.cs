using UnityEngine;

public class TilePiece : MonoBehaviour
{
    public int GridX { get; private set; }
    public int GridY { get; private set; }
    public int TypeId { get; private set; }

    private SpriteRenderer spriteRenderer;
    private Renderer meshRenderer;
    private Color baseColor = Color.white;
    private Vector3 defaultScale;
    private bool isSelected;
    private bool isHinted;

    public void Initialize(int x, int y, int typeId, Sprite sprite, SpriteRenderer rendererRef)
    {
        GridX = x;
        GridY = y;
        TypeId = typeId;
        spriteRenderer = rendererRef;
        spriteRenderer.sprite = sprite;
        baseColor = Color.white;
        spriteRenderer.color = baseColor;
        defaultScale = transform.localScale;
    }

    public void InitializeFallback(int x, int y, int typeId, Color color, Renderer rendererRef)
    {
        GridX = x;
        GridY = y;
        TypeId = typeId;
        meshRenderer = rendererRef;
        baseColor = color;
        meshRenderer.material.color = baseColor;
        defaultScale = transform.localScale;
    }

    public void SetGridPosition(int x, int y)
    {
        GridX = x;
        GridY = y;
    }

    public void SnapToCell(Vector3 worldPosition, Vector3 targetScale)
    {
        isSelected = false;
        isHinted = false;
        defaultScale = targetScale;
        transform.position = worldPosition;
        transform.localScale = targetScale;
        RefreshVisualState();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RefreshVisualState();
    }

    public void SetHint(bool hinted)
    {
        isHinted = hinted;
        RefreshVisualState();
    }

    private void RefreshVisualState()
    {
        Color targetColor = baseColor;
        if (isHinted)
        {
            targetColor = Color.Lerp(baseColor, Color.white, 0.6f);
        }
        if (isSelected)
        {
            targetColor = Color.Lerp(baseColor, Color.white, 0.9f);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = targetColor;
        }
        else if (meshRenderer != null)
        {
            meshRenderer.material.color = targetColor;
        }

        transform.localScale = isSelected ? defaultScale * 1.35f : defaultScale;
    }
}
