using System;
using System.Collections.Generic;
using Cinemachine;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;


//얘가 하는일
// 매칭, 로비scene의 네트워크 관련된 모든일?
//host client 접속, 플레이어 생성, 네트워크 입력 전달,
//화면 표시와 버튼 입력은 prototypelobbyui였던걸로
public class PrototypeLobbyBootstrap : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkObject playerPrefab;
    //하드코딩된 scene index
    [SerializeField] private int gameplaySceneBuildIndex = 2;
    //방 코드
    [SerializeField] private string sessionName = "";

    private NetworkRunner runner;
    private string status = "Host 또는 Client를 선택하세요.";
    private int lobbySceneBuildIndex;
    //runner.startgame 중복호출 방지용 
    private bool isConnecting;
    //이름
    //플레이어 networkobject spawn 됨 > playergamestate가 rpc 로 host에 전달
    public static string LocalPlayerName { get; private set; } = string.Empty;

    public string Status => status;
    public string RoomCode => sessionName;
    public bool IsConnecting => isConnecting;
    public bool IsConnected => runner != null && runner.IsRunning;
    public bool IsHost => IsConnected && runner.IsServer;
    public int PlayerCount => IsConnected ? CountPlayers(runner) : 0;


    //
    private void Awake()
    {
        EnsureLocalPlayerCameraOutput();

        //로비씬번호 저장?
        //Bootstrap 오브젝트가 처음 들어 있던 씬 번호를 저장 <<
        lobbySceneBuildIndex = SceneManager.GetActiveScene().buildIndex;

        //라운드 종료 > 로비 복귀시 기존 runner 가 살아있을수도 있음
        //이러면 runner 재사용하기
        foreach (NetworkRunner existing in NetworkRunner.Instances)
        {
            if (existing == null || !existing.IsRunning)
                continue;

            runner = existing;
            sessionName = existing.SessionInfo.Name;
            status = existing.IsServer
                ? "방 준비 완료 - Enter로 게임 시작"
                : "Host가 게임을 시작하기를 기다리는 중";
            enabled = false;
            return;
        }

        //최초 매칭 진입에서만 새 Runner를 만들기
        runner = gameObject.AddComponent<NetworkRunner>();
        gameObject.AddComponent<NetworkSceneManagerDefault>();
        gameObject.AddComponent<NetworkObjectProviderDefault>();

        //플레이어 접속과 입력 등의 Fusion 콜백을 이 Bootstrap이 받도록 등록
        runner.AddCallbacks(this);
    }

    private static void EnsureLocalPlayerCameraOutput()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[Lobby] MainCamera를 찾지 못했습니다.");
            return;
        }

        // 실제 화면을 렌더링하는 카메라는 씬에 하나만 두고,
        // 각 클라이언트의 로컬 플레이어 Virtual Camera가 이를 제어한다.
        if (mainCamera.GetComponent<CinemachineBrain>() == null)
            mainCamera.gameObject.AddComponent<CinemachineBrain>();
    }

    private void Update()
    {

        // Runner 오브젝트가 다른 씬에서도 유지될 수 있으므로> 로비에서 Enter 검사?
        //오브젝트가 살아있는채로(다른씬에서) enter 누르면 게임시작이 다시 호출될수도 있을거같긴한데
        if (!IsLobbySceneActive())
            return;

        // 네트워크 씬 전환은 모든 참가자를 이동시킬 수 있는 Host만 요청.
        if (runner != null && runner.IsRunning && runner.IsServer &&
            Input.GetKeyDown(KeyCode.Return))
            runner.LoadScene(SceneRef.FromIndex(gameplaySceneBuildIndex));
    }

    //이름을 저장하고 무작위 방 코드를 생성한 뒤 Host 세션을 시작
    public void StartHost(string playerName)
    {
        if (isConnecting || IsConnected || !TrySetPlayerName(playerName))
            return;

        sessionName = GenerateRoomCode();
        StartSession(GameMode.Host, sessionName);
    }

    //이름과 방 코드를 검사한 뒤 해당 SessionName의 방에 Client로 참가
    public void JoinClient(string playerName, string roomCode)
    {
        if (isConnecting || IsConnected || !TrySetPlayerName(playerName))
            return;

        string normalizedCode = NormalizeRoomCode(roomCode);
        if (normalizedCode.Length != 6)
        {
            status = "방 코드는 6자리입니다.";
            return;
        }

        sessionName = normalizedCode;
        StartSession(GameMode.Client, sessionName);
    }


    public async void LeaveToMainMenu()
    {

        // Runner를 남겨둔 채 메인으로 이동하면 다음 매칭 진입 시
        // 기존 세션이나 네트워크 콜백이 중복될 수 있으므로 먼저 종료한다.
        if (runner != null && runner.IsRunning)
            await runner.Shutdown();

        // 메인 화면은 네트워크 씬이 아니므로 일반 SceneManager로 이동
        SceneManager.LoadScene("MainMenuScene");
    }

    private async void StartSession(GameMode mode, string roomCode)
    {
        // 버튼을 여러 번 눌러 StartGame이 중복 호출되는 것을 방지
        isConnecting = true;
        status = mode == GameMode.Host ? "방을 만드는 중..." : "방에 접속하는 중...";

        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            // 같은 코드를 사용하는 Host와 Client가 동일한 세션에 접속
            SessionName = roomCode,
            //플레이어 수 조정가능
            PlayerCount = 5,

            // 세션 시작 후에도 현재 매칭 씬을 네트워크 씬으로 사용한다.
            Scene = SceneRef.FromIndex(gameObject.scene.buildIndex)
        });

        isConnecting = false;
        status = result.Ok
            ? (mode == GameMode.Host
                ? "방 준비 완료 - Enter로 게임 시작"
                : "접속 완료 - Host가 게임을 시작하기를 기다리는 중")
            : $"접속 실패: {result.ShutdownReason}";
    }

    //문자열 처리
    private bool TrySetPlayerName(string playerName)
    {
        string normalizedName = string.IsNullOrWhiteSpace(playerName) ? "" : playerName.Trim();
        if (normalizedName.Length == 0)
        {
            status = "플레이어 이름을 입력하세요.";
            return false;
        }

        LocalPlayerName = normalizedName.Length <= 16
            ? normalizedName
            : normalizedName.Substring(0, 16);
        PlayerPrefs.SetString("PlayerName", LocalPlayerName);
        PlayerPrefs.Save();
        return true;
    }

    private static string NormalizeRoomCode(string roomCode)
    {
        return string.IsNullOrWhiteSpace(roomCode)
            ? ""
            : roomCode.Trim().ToUpperInvariant();
    }

    private static string GenerateRoomCode()
    {

        // O/0, I/1처럼 서로 혼동하기 쉬운 문자는 제외
        //랜덤
        const string characters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] code = new char[6];
        for (int i = 0; i < code.Length; i++)
            //이런게 있는.. securerandom쓸뻔
            code[i] = characters[UnityEngine.Random.Range(0, characters.Length)];
        return new string(code);
    }

    private bool IsLobbySceneActive()
    {
        return SceneManager.GetActiveScene().buildIndex == lobbySceneBuildIndex;
    }

    private static int CountPlayers(NetworkRunner networkRunner)
    {
        int count = 0;
        foreach (PlayerRef ignored in networkRunner.ActivePlayers)
            count++;
        return count;
    }

    //플레이어생성
    public void OnPlayerJoined(NetworkRunner networkRunner, PlayerRef player)
    {
        //Host만 수행
        //Client까지 Spawn을 요청하면 동일 플레이어가 중복 생성될수잇음
        if (!networkRunner.IsServer || playerPrefab == null)
            return;

        //씬 로드 콜백 등에서 같은 플레이어를 다시 확인할 수 있으므로
        //이미 PlayerObject가 연결돼 있다면 새로 생성하지 x
        if (networkRunner.TryGetPlayerObject(player, out NetworkObject existing) && existing != null)
            return;

        Vector3 position = PrototypeSpawnPoints.Get(player.PlayerId);
        NetworkObject spawned = networkRunner.Spawn(playerPrefab, position, Quaternion.identity, player);

        // PlayerRef와 NetworkObject를 연결
        // 이후 OnInput과 다른 시스템이 PlayerRef로 플레이어 오브젝트를 찾을 수 있대요.
        networkRunner.SetPlayerObject(player, spawned);
    }
    
    //위랑 비슷비슷
    public void OnPlayerLeft(NetworkRunner networkRunner, PlayerRef player)
    {
        if (networkRunner.IsServer &&
            networkRunner.TryGetPlayerObject(player, out NetworkObject playerObject) &&
            playerObject != null)
            networkRunner.Despawn(playerObject);

        PrototypeRoundManager roundManager = FindFirstObjectByType<PrototypeRoundManager>();
        if (roundManager != null)
            roundManager.ReevaluateAfterRosterChange();
    }

    //네트워크 입력인데 이 밑으로는 제가 할수있는 영역이 아니에요..
    public void OnInput(NetworkRunner networkRunner, NetworkInput input)
    {
        if (!networkRunner.TryGetPlayerObject(networkRunner.LocalPlayer, out NetworkObject playerObject) ||
            playerObject == null)
            return;

        PlayerMove move = playerObject.GetComponent<PlayerMove>();
        PlayerGameState state = playerObject.GetComponent<PlayerGameState>();
        if (move == null)
            return;

        input.Set(state != null && !state.IsInPlayground ? default : move.GetNetworkInput());
    }

    public void OnConnectedToServer(NetworkRunner r) { }
    public void OnConnectFailed(NetworkRunner r, NetAddress a, NetConnectFailedReason reason)
    {
        isConnecting = false;
        status = $"접속 실패: {reason}";
    }
    public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
    public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason)
    {
        status = $"연결 종료: {reason}";
    }
    public void OnHostMigration(NetworkRunner r, HostMigrationToken token) { }
    public void OnInputMissing(NetworkRunner r, PlayerRef p, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef p) { }
    public void OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef p) { }
    public void OnReliableDataProgress(NetworkRunner r, PlayerRef p, ReliableKey key, float progress) { }
    public void OnReliableDataReceived(NetworkRunner r, PlayerRef p, ReliableKey key, ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner r)
    {
        if (!r.IsServer)
            return;

        foreach (PlayerRef player in r.ActivePlayers)
            OnPlayerJoined(r, player);
    }
    public void OnSceneLoadStart(NetworkRunner r) { }
    public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> sessions) { }
    public void OnShutdown(NetworkRunner r, ShutdownReason reason)
    {
        isConnecting = false;
        status = $"연결 종료: {reason}";
    }
    public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr message) { }
}

public static class PrototypeSpawnPoints
{
    private static readonly Vector3[] Points =
    {
        new(-9f, 1.2f, -9f), new(9f, 1.2f, -9f),
        new(-9f, 1.2f, 9f), new(9f, 1.2f, 9f)
    };

    public static Vector3 Get(int playerId) => Points[Mathf.Abs(playerId) % Points.Length];
}
