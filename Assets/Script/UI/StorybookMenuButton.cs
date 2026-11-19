using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>A small paper-like lift for both pointer and keyboard selection.</summary>
[RequireComponent(typeof(Button))]
public sealed class StorybookMenuButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private Button button;
    private Vector3 restScale;
    private Quaternion restRotation;
    private bool hovered;
    private bool selected;
    private float emphasis;

    private void Awake()
    {
        button = GetComponent<Button>();
        restScale = transform.localScale;
        restRotation = transform.localRotation;
    }

    private void Update()
    {
        float target = button.IsInteractable() && (hovered || selected) ? 1f : 0f;
        emphasis = Mathf.MoveTowards(emphasis, target, Time.unscaledDeltaTime * 8f);
        transform.localScale = restScale * Mathf.Lerp(1f, 1.035f, emphasis);
        transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, -1.2f * emphasis);
    }

    private void OnDisable()
    {
        hovered = selected = false;
        emphasis = 0f;
        if (button == null) return;
        transform.localScale = restScale;
        transform.localRotation = restRotation;
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;
    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) => selected = false;
}
