using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerScoreRowUI : MonoBehaviour
{
    private PlayerGameState player;
    private TMP_Text nameText;
    private Image firstCrown;
    private Image secondCrown;

    public PlayerGameState Player => player;

    public void Initialize(Sprite faceSprite, Sprite crownSprite)
    {
        RectTransform rowRect = GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(280f, 56f);

        LayoutElement layout = gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 56f;
        layout.minHeight = 56f;

        Image background = gameObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.45f);
        background.raycastTarget = false;

        Image face = CreateImage("Face", transform, faceSprite);
        face.color = faceSprite != null
            ? Color.white
            : new Color(0.35f, 0.7f, 1f, 1f);
        SetRect(face.rectTransform, new Vector2(8f, 8f), new Vector2(48f, 48f));

        GameObject nameObject = new GameObject("Name", typeof(RectTransform));
        nameObject.transform.SetParent(transform, false);
        nameText = nameObject.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 20f;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        nameText.raycastTarget = false;
        SetRect(
            nameText.rectTransform,
            new Vector2(58f, 0f),
            new Vector2(202f, 56f));

        firstCrown = CreateImage("Crown1", transform, crownSprite);
        secondCrown = CreateImage("Crown2", transform, crownSprite);

        Color crownColor = crownSprite != null
            ? Color.white
            : new Color(1f, 0.75f, 0.1f, 1f);
        firstCrown.color = crownColor;
        secondCrown.color = crownColor;
        SetRect(firstCrown.rectTransform, new Vector2(210f, 16f), new Vector2(234f, 40f));
        SetRect(secondCrown.rectTransform, new Vector2(242f, 16f), new Vector2(266f, 40f));
    }

    public void Bind(PlayerGameState playerState)
    {
        player = playerState;
        Refresh();
    }

    public void Refresh()
    {
        if (player == null)
            return;

        string displayName = player.DisplayName.ToString();
        nameText.text = string.IsNullOrWhiteSpace(displayName)
            ? player.Object.InputAuthority.ToString()
            : displayName;

        firstCrown.enabled = player.Crowns >= 1;
        secondCrown.enabled = player.Crowns >= 2;
    }

    private static Image CreateImage(
        string objectName,
        Transform parent,
        Sprite sprite)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 min,
        Vector2 max)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = min;
        rect.sizeDelta = max - min;
    }
}
