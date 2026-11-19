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

    [Header("Lobby Controls")]
    [SerializeField] private Button roomCopyButton;
    [SerializeField] private Button escCopyButton;
    [SerializeField] private TMP_Text lobbyHintText;

    [Header("ESC Menu")]
    [SerializeField] private GameObject escMenuPanel;
    [SerializeField] private GameObject escMenuContainer;
    [SerializeField] private GameObject escSettingsPanel;
    [SerializeField] private TMP_Text escRoomCodeText;
    [SerializeField] private TMP_Text escCopyButtonText;

    [Header("Audio Settings")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_Text masterVolumeText;

    private const string MasterVolumeKey = "MasterVolume";
    private float copyMessageUntil;
    private bool volumeChanged;
    private bool cursorUnlocked;

    public bool IsEscMenuOpen => escMenuPanel != null && escMenuPanel.activeSelf;

    private void Awake()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(AudioListener.volume);
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            UpdateVolumeLabel(AudioListener.volume);
        }

        //마지막으로 사용한 이름 불러와서 저장해서 매번 입력 안해도되게.
        nameInput.text = PlayerPrefs.GetString("PlayerName", "");
        //매칭 씬에 처음들어오면 host/client 선택화면부터 보여줌
        ShowRolePanel();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RestoreMasterVolume()
    {
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
    }

    private void SetMasterVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterVolumeKey, AudioListener.volume);
        volumeChanged = true;
        UpdateVolumeLabel(AudioListener.volume);
    }

    private void UpdateVolumeLabel(float value)
    {
        if (masterVolumeText != null)
            masterVolumeText.text = $"전체 음량  {Mathf.RoundToInt(value * 100f)}%";
    }

    private void SaveVolume()
    {
        if (!volumeChanged) return;
        PlayerPrefs.Save();
        volumeChanged = false;
    }

    private void OnDestroy()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
        SaveVolume();
    }

    private void Update()
    {
        if (lobby == null)
            return;

        //bootstrap이 관리하는 네트워크 상태를 ui에 반영 룸 코드, 호스트 이름 이런거.
        string roomCodeLabel = string.IsNullOrEmpty(lobby.RoomCode)
            ? "ROOM: ------"
            : $"ROOM: {lobby.RoomCode}";
        roomText.text = roomCodeLabel;
        if (escRoomCodeText != null)
            escRoomCodeText.text = roomCodeLabel;
        if (escCopyButtonText != null)
        {
            escCopyButtonText.text = Time.unscaledTime < copyMessageUntil
                ? "복사 완료!"
                : "코드 복사";
        }
        playerCountText.text = $"PLAYERS: {lobby.PlayerCount} / 7";
        bool canCopy = lobby.IsConnected && !string.IsNullOrEmpty(lobby.RoomCode);
        if (roomCopyButton != null) roomCopyButton.interactable = canCopy;
        if (escCopyButton != null) escCopyButton.interactable = canCopy;
        if (lobbyHintText != null)
            lobbyHintText.text = "ESC 메뉴 · Tab 커서";
        if (escRoomCodeText != null && Time.unscaledTime < copyMessageUntil)
            escRoomCodeText.text = "방 코드 복사 완료!";
        
        //copy 완료 안내
        if (Time.unscaledTime >= copyMessageUntil)
            statusText.text = lobby.Status;

        //접속이 성공해야 보여줌
        if (lobby.IsConnected && !roomPanel.activeSelf)
            ShowRoomPanel();

        // 호스트와 참가자 모두 같은 메뉴를 사용한다.
        if (lobby.IsConnected && Input.GetKeyDown(KeyCode.Escape))
        {
            if (escSettingsPanel != null && escSettingsPanel.activeSelf)
                ToggleEscSettings();
            else
                SetEscMenuOpen(!IsEscMenuOpen);
        }
        if (lobby.IsConnected && !IsEscMenuOpen && Input.GetKeyDown(KeyCode.Tab))
        {
            cursorUnlocked = !cursorUnlocked;
            SetCursorLocked(!cursorUnlocked);
        }
    }

    private void LateUpdate()
    {
        if (lobby == null || !lobby.IsConnected || roomPanel == null || !roomPanel.activeSelf)
            return;

        if (Input.GetKey(KeyCode.Escape))
            return;

        SetCursorLocked(!IsEscMenuOpen && !cursorUnlocked);
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
        {
            SetEscMenuOpen(false);
            SetCursorLocked(false);
            ShowOnly(joinPanel);
        }
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
        if (lobby == null || !lobby.IsConnected || string.IsNullOrEmpty(lobby.RoomCode))
            return;

        //// 운영체제 클립보드에 현재 Fusion SessionName인 방 코드를 복사 << thx..
        GUIUtility.systemCopyBuffer = lobby.RoomCode;
        statusText.text = "방 코드 복사 완료!";
        copyMessageUntil = Time.unscaledTime + 1.5f;
    }

    public void CloseEscMenu()
    {
        SetEscMenuOpen(false);
    }

    public void ToggleEscSettings()
    {
        if (escSettingsPanel == null)
            return;

        bool open = !escSettingsPanel.activeSelf;
        if (open && !IsEscMenuOpen)
            SetEscMenuOpen(true);
        escSettingsPanel.SetActive(open);
        if (escMenuContainer != null)
            escMenuContainer.SetActive(!open);
        if (!open)
            SaveVolume();
    }

    public void ShowRolePanel()
    {
        SetEscMenuOpen(false);
        SetCursorLocked(false);
        ShowOnly(rolePanel);
    }

    public void Leave()
    {
        SetEscMenuOpen(false);
        SetCursorLocked(false);
        lobby.LeaveToMainMenu();
    }

    private void ShowRoomPanel()
    {
        ShowOnly(roomPanel);
        SetEscMenuOpen(false);
        SetCursorLocked(true);
    }

    private void SetEscMenuOpen(bool open)
    {
        cursorUnlocked = false;
        if (escSettingsPanel != null && !open)
            escSettingsPanel.SetActive(false);
        if (escMenuContainer != null)
            escMenuContainer.SetActive(true);
        if (!open)
            SaveVolume();

        if (escMenuPanel != null)
            escMenuPanel.SetActive(open);

        SetCursorLocked(!open && lobby != null && lobby.IsConnected);
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
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

}
