using UnityEngine;
using UnityEngine.UI;

// 기존 HUD보다 뒤에서 게임 화면만 가린다. 소리나 카메라는 끄지 않는다.
public class SeekerVisionMask : MonoBehaviour
{
    [SerializeField, Range(0.01f, 0.2f)] private float slitHeight = 0.06f;
    [SerializeField, Range(0.1f, 1f)] private float slitWidth = 0.5f;

    private PlayerGameState state;
    private PlayerCondition condition;
    private Un movement;
    private Canvas maskCanvas;
    private RectTransform top;
    private RectTransform bottom;
    private RectTransform left;
    private RectTransform right;

    private void Awake()
    {
        state = GetComponent<PlayerGameState>();
        condition = GetComponent<PlayerCondition>();
        movement = GetComponent<Un>();
    }

    private void LateUpdate()
    {
        bool show = state != null && state.Object != null && state.Object.IsValid &&
            state.Object.HasInputAuthority && state.IsSeeker && state.IsInPlayground;

        if (!show)
        {
            if (maskCanvas != null) maskCanvas.enabled = false;
            return;
        }

        if (maskCanvas == null)
        {
            GameObject canvasObject = new GameObject("Seeker vision mask", typeof(Canvas));
            maskCanvas = canvasObject.GetComponent<Canvas>();
            maskCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            maskCanvas.sortingOrder = -32768;
            top = CreatePanel("Top");
            bottom = CreatePanel("Bottom");
            left = CreatePanel("Left");
            right = CreatePanel("Right");
        }

        maskCanvas.enabled = true;
        // 버튼을 놓은 프레임에는 서버 응답을 기다리지 않고 바로 가린다.
        bool open = movement != null && movement.OpenEyesHeld && state.CanMove &&
            condition.EyesOpen && condition.CurrentStamina > 0f;
        float halfHeight = open ? slitHeight * 0.5f : 0f;
        float halfWidth = open ? slitWidth * 0.5f : 0f;
        SetRect(top, new Vector2(0f, 0.5f + halfHeight), Vector2.one);
        SetRect(bottom, Vector2.zero, new Vector2(1f, 0.5f - halfHeight));
        SetRect(left, new Vector2(0f, 0.5f - halfHeight), new Vector2(0.5f - halfWidth, 0.5f + halfHeight));
        SetRect(right, new Vector2(0.5f + halfWidth, 0.5f - halfHeight), new Vector2(1f, 0.5f + halfHeight));
    }

    private RectTransform CreatePanel(string panelName)
    {
        GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(maskCanvas.transform, false);
        Image image = panel.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return panel.GetComponent<RectTransform>();
    }

    private void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDisable()
    {
        if (maskCanvas != null) maskCanvas.enabled = false;
    }

    private void OnDestroy()
    {
        if (maskCanvas != null) Destroy(maskCanvas.gameObject);
    }
}
