using TMPro;
using UnityEngine;
using UnityEngine.UI;

//ui는 fusion, 네트워크를 직접 건드리지 않는대요.
public class PrototypeLobbyUI : MonoBehaviour
{
    [Header("Network")]
    [SerializeField] private PrototypeLobbyBootstrap lobby;

    [Header("Panels")]
    [SerializeField] private GameObject rolePanel; //이름 입력, hostclient 선택화면
    [SerializeField] private GameObject joinPanel; //client 방 코드 입력화면
    [SerializeField] private GameObject roomPanel; //접속 후 방 코드, 접속 인원, 상태를 표시하는 화면

    [Header("Entry Decorations")]
    [SerializeField] private TMP_Text matchingTitle;
    [SerializeField] private GameObject sideShade;

    [Header("Input")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField roomCodeInput;

    [Header("Display")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text roomText;
    [SerializeField] private TMP_Text playerCountText;

    private float copyMessageUntil;

    private void Awake()
    {
        ConfigureRoomHud();

        //마지막으로 사용한 이름 불러와서 저장해서 매번 입력 안해도되게.
        nameInput.text = PlayerPrefs.GetString("PlayerName", "");
        //매칭 씬에 처음들어오면 host/client 선택화면부터 보여줌
        ShowRolePanel();
    }

    private void ConfigureRoomHud()
    {
        if (roomPanel == null)
            return;

        RectTransform panelRect = roomPanel.GetComponent<RectTransform>();
        SetTopLeftRect(panelRect, new Vector2(20f, -20f), new Vector2(380f, 220f));

        Image background = roomPanel.GetComponent<Image>();
        if (background == null)
            background = roomPanel.AddComponent<Image>();

        background.color = new Color(0f, 0f, 0f, 0.62f);
        background.raycastTarget = false;

        ConfigureHudText(roomText, new Vector2(16f, -14f), new Vector2(348f, 34f), 24f);
        ConfigureHudText(playerCountText, new Vector2(16f, -50f), new Vector2(348f, 30f), 20f);
        ConfigureHudText(statusText, new Vector2(16f, -82f), new Vector2(348f, 48f), 16f);

        Button[] buttons = roomPanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            RectTransform buttonRect = buttons[i].GetComponent<RectTransform>();
            SetTopLeftRect(
                buttonRect,
                new Vector2(16f + i * 112f, -140f),
                new Vector2(100f, 36f));

            TMP_Text label = buttons[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.fontSize = 16f;
        }

        foreach (TMP_Text text in roomPanel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == roomText || text == playerCountText || text == statusText ||
                text.GetComponentInParent<Button>() != null)
                continue;

            ConfigureHudText(text, new Vector2(16f, -184f), new Vector2(348f, 24f), 14f);
        }
    }

    private static void ConfigureHudText(
        TMP_Text text,
        Vector2 position,
        Vector2 size,
        float fontSize)
    {
        if (text == null)
            return;

        SetTopLeftRect(text.rectTransform, position, size);
        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void SetTopLeftRect(
        RectTransform rect,
        Vector2 position,
        Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void Update()
    {
        if (lobby == null)
            return;

        //bootstrap이 관리하는 네트워크 상태를 ui에 반영 룸 코드, 호스트 이름 이런거.
        roomText.text = string.IsNullOrEmpty(lobby.RoomCode)
            ? "ROOM: ------"
            : $"ROOM: {lobby.RoomCode}";
        playerCountText.text = $"PLAYERS: {lobby.PlayerCount} / 5";
        
        //copy 완료 안내
        if (Time.unscaledTime >= copyMessageUntil)
            statusText.text = lobby.Status;

        //접속이 성공해야 보여줌
        if (lobby.IsConnected && !roomPanel.activeSelf)
            ShowRoomPanel();

        //로비케릭터 시점 조작, 버튼조작
        if (lobby.IsConnected && Input.GetKeyDown(KeyCode.Escape))
            ToggleCursor();
    }

    //bootstrap에 host 생성요청
    public void Host()
    {
        if (!string.IsNullOrWhiteSpace(nameInput.text))
            lobby.StartHost(nameInput.text);
    }

    //client 방 코드 입력 화면 열기
    public void OpenJoinPanel()
    {
        if (!string.IsNullOrWhiteSpace(nameInput.text))
            ShowOnly(joinPanel);
    }


    //Bootstrap에 Client 접속을 요청
    //접속 성공전까지 join panel 유지
    public void Join()
    {
        if (string.IsNullOrWhiteSpace(nameInput.text) ||
            string.IsNullOrWhiteSpace(roomCodeInput.text) ||
            roomCodeInput.text.Trim().Length != 6)
            return;

        lobby.JoinClient(nameInput.text, roomCodeInput.text);
    }


    public void CopyRoomCode()
    {
        //// 운영체제 클립보드에 현재 Fusion SessionName인 방 코드를 복사 << thx..
        GUIUtility.systemCopyBuffer = lobby.RoomCode;
        statusText.text = "방 코드 복사 완료!";
        copyMessageUntil = Time.unscaledTime + 1.5f;
    }

    public void ShowRolePanel()
    {
        ShowOnly(rolePanel);
    }

    public void Leave()
    {
        lobby.LeaveToMainMenu();
    }

    private void ShowRoomPanel()
    {
        ShowOnly(roomPanel);
    }

    private void ShowOnly(GameObject target)
    {
        rolePanel.SetActive(target == rolePanel);
        joinPanel.SetActive(target == joinPanel);
        roomPanel.SetActive(target == roomPanel);

        bool showEntryDecorations = target != roomPanel;
        if (matchingTitle != null)
            matchingTitle.gameObject.SetActive(showEntryDecorations);
        if (sideShade != null)
            sideShade.SetActive(showEntryDecorations);
    }

    //커서 잠겨있으면 ui 조작모드,
    //풀려있으면 캐릭터 시점 조작
    private static void ToggleCursor()
    {
        bool unlock = Cursor.lockState == CursorLockMode.Locked;
        Cursor.lockState = unlock ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = unlock;
    }

}
