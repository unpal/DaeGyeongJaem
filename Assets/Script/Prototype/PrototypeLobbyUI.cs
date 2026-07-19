using TMPro;
using UnityEngine;

//ui는 fusion, 네트워크를 직접 건드리지 않는대요.
public class PrototypeLobbyUI : MonoBehaviour
{
    [Header("Network")]
    [SerializeField] private PrototypeLobbyBootstrap lobby;

    [Header("Panels")]
    [SerializeField] private GameObject rolePanel; //이름 입력, hostclient 선택화면
    [SerializeField] private GameObject joinPanel; //client 방 코드 입력화면
    [SerializeField] private GameObject roomPanel; //접속 후 방 코드, 접속 인원, 상태를 표시하는 화면

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
        //마지막으로 사용한 이름 불러와서 저장해서 매번 입력 안해도되게.
        nameInput.text = PlayerPrefs.GetString("PlayerName", "");
        //매칭 씬에 처음들어오면 host/client 선택화면부터 보여줌
        ShowRolePanel();
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
    }

    //커서 잠겨있으면 ui 조작모드,
    //풀려있으면 캐릭터 시점 조작
    private static void ToggleCursor()
    {
        bool unlock = Cursor.lockState == CursorLockMode.Locked;
        Cursor.lockState = unlock ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = unlock;
    }

#if UNITY_EDITOR
    public void Configure(
        PrototypeLobbyBootstrap bootstrap,
        GameObject role,
        GameObject join,
        GameObject room,
        TMP_InputField playerName,
        TMP_InputField roomCode,
        TMP_Text status,
        TMP_Text roomLabel,
        TMP_Text playerCount)
    {
        lobby = bootstrap;
        rolePanel = role;
        joinPanel = join;
        roomPanel = room;
        nameInput = playerName;
        roomCodeInput = roomCode;
        statusText = status;
        roomText = roomLabel;
        playerCountText = playerCount;
    }
#endif
}
