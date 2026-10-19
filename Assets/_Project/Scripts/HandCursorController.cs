using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class HandCursorController : MonoBehaviour
{
    [Inject] private ProjectSettings projectSettings;

    [SerializeField] private RectTransform cursorRect;
    [SerializeField] private Image cursorImage;

    private bool shown;
    private bool pressed;

    private void Update()
    {
        bool show = projectSettings.UseHandCursor;
        if (show != shown)
        {
            shown = show;
            cursorImage.enabled = show;
            Cursor.visible = !show;
            if (show)
                Apply(projectSettings.HandIdle);
        }

        if (!show)
            return;

        bool nextPressed = Input.GetMouseButton(0);
        if (nextPressed != pressed)
        {
            pressed = nextPressed;
            Sprite sprite = projectSettings.HandIdle;
            if (pressed && projectSettings.HandPressed != null)
                sprite = projectSettings.HandPressed;

            Apply(sprite);
        }

        cursorRect.position = Input.mousePosition;
    }

    private void OnDestroy()
    {
        if (shown)
            Cursor.visible = true;
    }

    private void Apply(Sprite sprite)
    {
        if (sprite == null)
            return;

        cursorImage.sprite = sprite;

        Vector2 size = sprite.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            return;

        cursorRect.pivot = new Vector2(sprite.pivot.x / size.x, sprite.pivot.y / size.y);
    }
}
