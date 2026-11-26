using Cinemachine;
using Fusion;
using Fusion.Sockets;
#if FUSION_WEAVER
using Photon.Voice.Fusion;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 포톤 방 생성 없이 GameMode.Single(오프라인 로컬 모드)로 Player.prefab을 즉시 스폰해
/// 이동/등반/스태미나/래그돌 조작을 빠르게 테스트하기 위한 단독 샌드박스 부트스트랩.
/// </summary>
public class ClimbingSandboxBootstrap : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Spawn")]
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Vector3 spawnEulerAngles = Vector3.zero;

    [Header("Test Options")]
    [SerializeField] private bool lockCursorOnStart = true;
    [SerializeField] private bool showDebugHud = true;

    private NetworkRunner _runner;
    private NetworkObject _spawnedPlayer;
    private Un _un;
    private PlayerCondition _condition;
    private FallDamage _fallDamage;
    private string _status = "초기화 중...";

    private async void Start()
    {
        foreach (NetworkRunner existing in NetworkRunner.Instances)
        {
            if (existing != null && existing.IsRunning)
            {
                Debug.Log("[ClimbingSandbox] 이미 실행 중인 NetworkRunner가 있어 샌드박스 부트스트랩을 비활성화합니다.");
                enabled = false;
                return;
            }
        }

        if (playerPrefab == null)
        {
            _status = "오류: playerPrefab이 할당되지 않았습니다.";
            Debug.LogError("[ClimbingSandbox] playerPrefab을 인스펙터에 연결해 주세요.");
            return;
        }

        EnsureMainCameraBrain();

        _runner = GetComponent<NetworkRunner>();
        if (_runner == null)
            _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        if (GetComponent<NetworkSceneManagerDefault>() == null)
            gameObject.AddComponent<NetworkSceneManagerDefault>();
        if (GetComponent<NetworkObjectProviderDefault>() == null)
            gameObject.AddComponent<NetworkObjectProviderDefault>();

#if FUSION_WEAVER
        FusionVoiceClient voiceClient = GetComponent<FusionVoiceClient>();
        if (voiceClient == null)
            voiceClient = gameObject.AddComponent<FusionVoiceClient>();
        voiceClient.AutoConnectAndJoin = false;
        voiceClient.PrimaryRecorder = null;
        voiceClient.SpeakerPrefab = null;
        voiceClient.ApplyDontDestroyOnLoad = false;
#endif

        _runner.AddCallbacks(this);
        _status = "Single 모드 시작 중...";

        StartGameResult result = await _runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Single,
            SessionName = "ClimbingSandbox"
        });

        if (this == null)
            return;

        if (!result.Ok)
        {
            _status = $"시작 실패: {result.ShutdownReason}";
            Debug.LogError($"[ClimbingSandbox] StartGame 실패: {result.ShutdownReason}");
            return;
        }

        _status = "실행 중 (오프라인 Single)";
        if (lockCursorOnStart)
            SetCursorLocked(true);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame)
            SetCursorLocked(Cursor.visible);

        if (keyboard.rKey.wasPressedThisFrame)
            ResetTestPlayer();

        if (keyboard.tKey.wasPressedThisFrame)
            _condition?.ResetStamina();
    }

    private void OnDestroy()
    {
        SetCursorLocked(false);
        if (_runner != null && _runner.IsRunning)
            _runner.Shutdown();
    }

    private void ResetTestPlayer()
    {
        if (_un == null)
            return;

        Quaternion spawnRotation = Quaternion.Euler(spawnEulerAngles);
        _condition?.ResetForNextRound();
        _un.ResetForNextRound(spawnPosition, spawnRotation);
        _fallDamage?.ResetForNextRound();
    }

    private static void EnsureMainCameraBrain()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null && mainCam.GetComponent<CinemachineBrain>() == null)
            mainCam.gameObject.AddComponent<CinemachineBrain>();
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnGUI()
    {
        if (!showDebugHud)
            return;

        GUI.Box(new Rect(14f, 14f, 430f, 150f), "등반 / 래그돌 단독 테스트 (Single 오프라인 모드)");
        GUI.Label(new Rect(26f, 38f, 405f, 22f), $"상태: {_status}");

        if (_un != null && _condition != null)
        {
            float maxStamina = Mathf.Max(1f, _condition.CurrentMaxStamina);
            float ratio = Mathf.Clamp01(_condition.CurrentStamina / maxStamina);
            string stateLabel = _un.IsLunging ? $"{_un.statemachine} (Lunge)" : _un.statemachine.ToString();
            GUI.Label(
                new Rect(26f, 60f, 405f, 22f),
                $"State: {stateLabel}  |  Grounded: {_un.IsGrounded}  |  Y: {_un.transform.position.y:F2}m");

            GUI.Label(
                new Rect(26f, 82f, 215f, 22f),
                $"Stamina: {_condition.CurrentStamina:F0}/{maxStamina:F0}  |  Dmg: {_condition.PermanentDamage:F1}");

            Rect barBg = new Rect(245f, 86f, 180f, 14f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(barBg, Texture2D.whiteTexture);
            GUI.color = ratio > 0.25f ? new Color(0.2f, 0.85f, 0.35f, 1f) : new Color(0.95f, 0.25f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(barBg.x + 2f, barBg.y + 2f, (barBg.width - 4f) * ratio, barBg.height - 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (_fallDamage != null)
            {
                GUI.Label(
                    new Rect(26f, 104f, 405f, 22f),
                    $"LastFall: {_fallDamage.LastFallDistance:F1}m  ->  Dmg: {_fallDamage.LastFallDamage:F1}");
            }
        }

        GUI.Label(
            new Rect(26f, 128f, 405f, 22f),
            "좌클릭: 등반 | Space(등반중): 도약 | R: 리셋 | T: 스태미나 | ESC: 커서");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer || playerPrefab == null)
            return;

        if (runner.TryGetPlayerObject(player, out NetworkObject existing) && existing != null)
            return;

        _spawnedPlayer = runner.Spawn(
            playerPrefab,
            spawnPosition,
            Quaternion.Euler(spawnEulerAngles),
            player);
        runner.SetPlayerObject(player, _spawnedPlayer);

        _un = _spawnedPlayer.GetComponent<Un>();
        _condition = _spawnedPlayer.GetComponent<PlayerCondition>();
        _fallDamage = _spawnedPlayer.GetComponent<FallDamage>();
        if (_fallDamage == null)
            _fallDamage = _spawnedPlayer.gameObject.AddComponent<FallDamage>();
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (_un != null)
            input.Set(_un.GetNetworkInput());
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
