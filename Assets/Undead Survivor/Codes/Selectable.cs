using UnityEngine;

public class Selectable : MonoBehaviour
{
    public SpriteRenderer outline;
    public Color selectedColor = Color.cyan;
    private Color originalColor;
    public bool IsSelected { get; private set; }

    void Awake()
    {
        if (outline) originalColor = outline.color;
    }

    public void SetSelected(bool on)
    {
        IsSelected = on;
        if (outline)
            outline.color = on ? selectedColor : originalColor;
    }
}
