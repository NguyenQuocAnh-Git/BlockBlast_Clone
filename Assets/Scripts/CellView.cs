using UnityEngine;

public class CellView : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite emptySprite;
    public Sprite filledSprite;

    private SpriteRenderer spriteRenderer;

    // tọa độ logic
    public int x;
    public int y;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// Set trạng thái cell theo giá trị logic
    /// </summary>
    public void SetValue(int value)
    {
        if (value == 0)
        {
            spriteRenderer.sprite = emptySprite;
        }
        else if (value == 1)
        {
            spriteRenderer.sprite = filledSprite;
        }
    }
}
