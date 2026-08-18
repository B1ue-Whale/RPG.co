using UnityEngine;

// Two-sprite (background + fill) progress bar rendered in world space. Generates its
// pixel sprite procedurally so it has no art-asset dependency and can be spawned
// entirely from script (e.g. above a character's head).
public class WorldSpaceProgressBar : MonoBehaviour
{
    private static Sprite _pixelSprite;

    private Vector2 _size;
    private Transform _fillTransform;

    public static WorldSpaceProgressBar Create(Transform parent, Vector3 localOffset, Vector2 size, Color backgroundColor, Color fillColor, int sortingOrder = 100)
    {
        var go = new GameObject("WorldSpaceProgressBar");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localOffset;

        var bar = go.AddComponent<WorldSpaceProgressBar>();
        bar.Init(size, backgroundColor, fillColor, sortingOrder);
        return bar;
    }

    private void Init(Vector2 size, Color backgroundColor, Color fillColor, int sortingOrder)
    {
        _size = size;

        SpriteRenderer background = CreateBar("Background", backgroundColor, sortingOrder);
        background.transform.localScale = new Vector3(_size.x, _size.y, 1f);

        SpriteRenderer fill = CreateBar("Fill", fillColor, sortingOrder + 1);
        _fillTransform = fill.transform;

        SetFill(0f);
        SetVisible(false);
    }

    private SpriteRenderer CreateBar(string barName, Color color, int sortingOrder)
    {
        if (_pixelSprite == null)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            _pixelSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        var go = new GameObject(barName);
        go.transform.SetParent(transform, false);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = _pixelSprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    public void SetFill(float value01)
    {
        value01 = Mathf.Clamp01(value01);
        _fillTransform.localScale = new Vector3(_size.x * value01, _size.y, 1f);
        _fillTransform.localPosition = new Vector3(-_size.x * 0.5f + (_size.x * value01) * 0.5f, 0f, 0f);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
