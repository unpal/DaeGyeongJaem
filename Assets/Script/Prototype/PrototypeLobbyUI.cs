using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Layout lives in the scene; this component presents session state and handles UI input.
public class PrototypeLobbyUI : MonoBehaviour
{
    [Header("Session")]
    [SerializeField] private PrototypeLobbyBootstrap lobby;

    [Header("Screens")]
    [SerializeField] private GameObject rolePanel;
    [SerializeField] private GameObject joinPanel;
    [SerializeField] private GameObject roomPanel;
    [SerializeField] private CanvasGroup screenGroup;
    [SerializeField] private TMP_Text matchingTitle;
    [SerializeField] private GameObject sideShade;

    [Header("Inputs")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private Button startGameButton;

    [Header("Room")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text roomText;
    [SerializeField] private TMP_Text playerCountText;

    [Header("Feedback")]
    [SerializeField] private TMP_Text validationText;
    [SerializeField] private TMP_Text toastText;
    [SerializeField] private GameObject loadingOverlay;
    [SerializeField] private RectTransform loadingSpinner;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private TMP_Text loadingHint;
    [SerializeField] private GameObject errorDialog;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private Button dismissErrorButton;

    private bool showingJoin;
    private bool wasBusy;
    private bool wasConnected;
    private int shownErrorVersion;
    private float busySince;
    private float toastUntil;
    private Selectable[] screenControls;

    private void Awake()
    {
        nameInput.text = PlayerPrefs.GetString("PlayerName", "");
        nameInput.characterLimit = 16;
        roomCodeInput.characterLimit = 6;
        screenControls = screenGroup.GetComponentsInChildren<Selectable>(true);
        nameInput.onValueChanged.AddListener(ClearValidation);
        roomCodeInput.onValueChanged.AddListener(ClearValidation);
        validationText.text = string.Empty;
        toastText.gameObject.SetActive(false);
        loadingOverlay.SetActive(false);
        errorDialog.SetActive(false);
        UnlockCursor();
        ShowScreen(rolePanel);
    }

    private void Start()
    {
        // Do not reopen an already handled error when returning from a round.
        if (lobby != null && lobby.State != LobbySessionState.Error)
            shownErrorVersion = lobby.ErrorVersion;
        Refresh();
    }

    private void OnDestroy()
    {
        nameInput.onValueChanged.RemoveListener(ClearValidation);
        roomCodeInput.onValueChanged.RemoveListener(ClearValidation);
    }

    private void Update()
    {
        Refresh();
        if (loadingOverlay.activeSelf)
            loadingSpinner.Rotate(0f, 0f, -180f * Time.unscaledDeltaTime);
        toastText.gameObject.SetActive(Time.unscaledTime < toastUntil);
        if (lobby != null && lobby.IsConnected && !lobby.IsBusy && !errorDialog.activeSelf &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            bool unlock = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = unlock ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = unlock;
        }
    }

    private void Refresh()
    {
        if (lobby == null) return;
        bool busy = lobby.IsBusy;
        bool connected = lobby.IsConnected;
        if (busy && !wasBusy)
        {
            busySince = Time.unscaledTime;
            validationText.text = string.Empty;
            EventSystem.current?.SetSelectedGameObject(null);
            UnlockCursor();
        }
        if (lobby.ErrorVersion != shownErrorVersion)
        {
            shownErrorVersion = lobby.ErrorVersion;
            errorText.text = lobby.LastError;
            errorDialog.SetActive(true);
            UnlockCursor();
            EventSystem.current?.SetSelectedGameObject(dismissErrorButton.gameObject);
        }
        if (connected != wasConnected)
        {
            validationText.text = string.Empty;
            ShowScreen(connected ? roomPanel : showingJoin ? joinPanel : rolePanel);
            UnlockCursor();
        }
        bool blocked = busy || errorDialog.activeSelf;
        screenGroup.interactable = !blocked;
        screenGroup.blocksRaycasts = !blocked;
        // Disable submit from keyboards/controllers as well as pointer input.
        foreach (Selectable control in screenControls)
            control.interactable = !blocked;
        startGameButton.gameObject.SetActive(connected && lobby.IsHost);
        startGameButton.interactable = !blocked && connected && lobby.IsHost;
        loadingOverlay.SetActive(busy);
        loadingText.text = lobby.Status;
        loadingHint.text = Time.unscaledTime - busySince >= 8f
            ? "연결이 평소보다 오래 걸리고 있어요. 잠시만 기다려 주세요."
            : "처리 중에는 버튼을 다시 누르지 않아도 돼요.";
        roomText.text = $"방 코드  {lobby.RoomCode}";
        playerCountText.text = $"참가자  {lobby.PlayerCount} / 5";
        statusText.text = lobby.Status;
        wasBusy = busy;
        wasConnected = connected;
    }

    private bool CanAct => lobby != null && !lobby.IsBusy && !errorDialog.activeSelf;

    private bool ValidateName()
    {
        if (!string.IsNullOrWhiteSpace(nameInput.text)) return true;
        validationText.text = "함께 플레이할 이름을 입력해 주세요.";
        nameInput.Select();
        nameInput.ActivateInputField();
        return false;
    }

    public void Host()
    {
        if (!CanAct || !ValidateName()) return;
        lobby.StartHost(nameInput.text);
        Refresh();
    }

    public void OpenJoinPanel()
    {
        if (!CanAct || !ValidateName()) return;
        showingJoin = true;
        validationText.text = string.Empty;
        ShowScreen(joinPanel);
        roomCodeInput.Select();
        roomCodeInput.ActivateInputField();
    }

    public void Join()
    {
        if (!CanAct || !ValidateName()) return;
        string code = roomCodeInput.text.Trim().ToUpperInvariant();
        if (!PrototypeLobbyBootstrap.IsValidRoomCode(code))
        {
            validationText.text = "영문과 숫자로 된 6자리 방 코드를 입력해 주세요.";
            roomCodeInput.Select();
            roomCodeInput.ActivateInputField();
            return;
        }
        roomCodeInput.SetTextWithoutNotify(code);
        lobby.JoinClient(nameInput.text, code);
        Refresh();
    }

    public void ShowRolePanel()
    {
        if (!CanAct) return;
        showingJoin = false;
        validationText.text = string.Empty;
        ShowScreen(rolePanel);
    }

    public void CopyRoomCode()
    {
        if (!CanAct || !lobby.IsConnected) return;
        GUIUtility.systemCopyBuffer = lobby.RoomCode;
        toastText.text = "방 코드를 복사했어요.";
        toastUntil = Time.unscaledTime + 2f;
    }

    public void StartGame()
    {
        if (!CanAct) return;
        lobby.StartGame();
        Refresh();
    }

    public void Leave()
    {
        if (!CanAct) return;
        lobby.LeaveToMainMenu();
        Refresh();
    }

    public void DismissError()
    {
        if (lobby == null || lobby.IsBusy) return;
        errorDialog.SetActive(false);
        Refresh();
        EventSystem.current?.SetSelectedGameObject(null);
        if (!lobby.IsConnected)
        {
            TMP_InputField field = showingJoin ? roomCodeInput : nameInput;
            field.Select();
            field.ActivateInputField();
        }
    }

    private void ShowScreen(GameObject target)
    {
        rolePanel.SetActive(target == rolePanel);
        joinPanel.SetActive(target == joinPanel);
        roomPanel.SetActive(target == roomPanel);
        matchingTitle.gameObject.SetActive(target != roomPanel);
        sideShade.SetActive(target != roomPanel);
    }

    private void ClearValidation(string _) => validationText.text = string.Empty;

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
