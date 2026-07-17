using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreboardUI : MonoBehaviour
{
    [SerializeField] private Sprite defaultFaceSprite;
    [SerializeField] private Sprite crownSprite;
    [SerializeField] private float refreshInterval = 0.25f;

    private readonly Dictionary<PlayerGameState, PlayerScoreRowUI> rows = new();
    private RectTransform listRoot;
    private float refreshTimer;

    private void Awake()
    {
        BuildUI();
    }

    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f)
            return;

        refreshTimer = refreshInterval;
        RefreshPlayers();
    }

    private void BuildUI()
    {
        GameObject panelObject = new GameObject("ScoreboardPanel", typeof(RectTransform));
        panelObject.transform.SetParent(transform, false);

        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(16f, -16f);
        panel.sizeDelta = new Vector2(300f, 500f);

        GameObject titleObject = new GameObject("Title", typeof(RectTransform));
        titleObject.transform.SetParent(panel, false);
        TMP_Text title = titleObject.AddComponent<TextMeshProUGUI>();
        title.text = "PLAYERS";
        title.fontSize = 24f;
        title.fontStyle = FontStyles.Bold;
        title.color = Color.white;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.raycastTarget = false;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(280f, 36f);

        GameObject listObject = new GameObject("PlayerList", typeof(RectTransform));
        listObject.transform.SetParent(panel, false);
        listRoot = listObject.GetComponent<RectTransform>();
        listRoot.anchorMin = new Vector2(0f, 1f);
        listRoot.anchorMax = new Vector2(0f, 1f);
        listRoot.pivot = new Vector2(0f, 1f);
        listRoot.anchoredPosition = new Vector2(0f, -40f);
        listRoot.sizeDelta = new Vector2(280f, 0f);

        VerticalLayoutGroup layout = listObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = listObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void RefreshPlayers()
    {
        PlayerGameState[] players =
            FindObjectsByType<PlayerGameState>(FindObjectsSortMode.None);
        HashSet<PlayerGameState> activePlayers = new(players);

        List<PlayerGameState> removedPlayers = new();
        foreach (KeyValuePair<PlayerGameState, PlayerScoreRowUI> entry in rows)
        {
            if (entry.Key == null || !activePlayers.Contains(entry.Key))
            {
                if (entry.Value != null)
                    Destroy(entry.Value.gameObject);

                removedPlayers.Add(entry.Key);
            }
        }

        foreach (PlayerGameState removedPlayer in removedPlayers)
            rows.Remove(removedPlayer);

        foreach (PlayerGameState player in players)
        {
            if (!rows.TryGetValue(player, out PlayerScoreRowUI row))
            {
                GameObject rowObject = new GameObject(
                    $"PlayerRow_{player.Object.InputAuthority.PlayerId}",
                    typeof(RectTransform));
                rowObject.transform.SetParent(listRoot, false);
                row = rowObject.AddComponent<PlayerScoreRowUI>();
                row.Initialize(defaultFaceSprite, crownSprite);
                row.Bind(player);
                rows.Add(player, row);
            }

            row.Refresh();
        }
    }
}
