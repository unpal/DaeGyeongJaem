using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cinemachine;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum LobbySessionState { Idle, Connecting, Connected, StartingGame, Leaving, Error }

// One persistent owner handles callbacks. A newly loaded lobby delegates to that owner.
public class PrototypeLobbyBootstrap : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private int gameplaySceneBuildIndex = 2;
    [SerializeField] private string sessionName = "";
    [SerializeField, Min(5f)] private float connectionTimeoutSeconds = 30f;

    private static PrototypeLobbyBootstrap activeSession;
    private PrototypeLobbyBootstrap owner;
    private NetworkRunner runner;
    private int lobbySceneBuildIndex;
    private LobbySessionState state;
    private string status = "이름을 입력하고 방을 만들거나 참가해 주세요.";
    private string lastError;
    private int errorVersion;
    private CancellationTokenSource connectionCancellation;
    private bool applicationQuitting;

    private PrototypeLobbyBootstrap Session => owner != null ? owner : this;
    public static string LocalPlayerName { get; private set; } = string.Empty;
    public LobbySessionState State => Session.state;
    public string Status => Session.status;
    public string RoomCode => Session.sessionName;
    public string LastError => Session.lastError;
    public int ErrorVersion => Session.errorVersion;
    public bool IsConnecting => State == LobbySessionState.Connecting;
    public bool IsBusy => State == LobbySessionState.Connecting ||
                          State == LobbySessionState.StartingGame ||
                          State == LobbySessionState.Leaving;
    public bool IsConnected => Session.runner != null && Session.runner.IsRunning &&
        (State == LobbySessionState.Connected || State == LobbySessionState.StartingGame);
    public bool IsHost => IsConnected && Session.runner.IsServer;
    public int PlayerCount => IsConnected ? CountPlayers(Session.runner) : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeSession = null;
        LocalPlayerName = string.Empty;
    }

    private void Awake()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.GetComponent<CinemachineBrain>() == null)
            mainCamera.gameObject.AddComponent<CinemachineBrain>();

        if (activeSession != null && activeSession != this)
        {
            owner = activeSession;
            return;
        }
        activeSession = this;
        lobbySceneBuildIndex = gameObject.scene.buildIndex;
        // Detach only this service; UI and environment stay owned by the scene.
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    public void StartHost(string playerName)
    {
        if (Session != this) { Session.StartHost(playerName); return; }
        if (IsBusy || IsConnected || !TrySetPlayerName(playerName)) return;
        sessionName = GenerateRoomCode();
        StartSession(GameMode.Host);
    }

    public void JoinClient(string playerName, string roomCode)
    {
        if (Session != this) { Session.JoinClient(playerName, roomCode); return; }
        if (IsBusy || IsConnected || !TrySetPlayerName(playerName)) return;
        string code = (roomCode ?? "").Trim().ToUpperInvariant();
        if (!IsValidRoomCode(code))
        {
            ReportError("영문과 숫자로 된 6자리 방 코드를 입력해 주세요.");
            return;
        }
        sessionName = code;
        StartSession(GameMode.Client);
    }

    private async void StartSession(GameMode mode)
    {
        // Acquire before the first await; callbacks never release this operation's lock.
        state = LobbySessionState.Connecting;
        status = mode == GameMode.Host ? "방을 만들고 있어요..." : "방에 연결하고 있어요...";
        string failure = null;
        using (var cancellation = new CancellationTokenSource())
        {
            connectionCancellation = cancellation;
            cancellation.CancelAfter(TimeSpan.FromSeconds(Mathf.Max(5f, connectionTimeoutSeconds)));
            try
            {
                // Allow the loading overlay to be presented before connection work starts.
                await Task.Yield();
                if (this == null || applicationQuitting) return;
                await DisposeRunner();
                cancellation.Token.ThrowIfCancellationRequested();
                var runnerObject = new GameObject("LobbyNetworkRunner");
                DontDestroyOnLoad(runnerObject);
                runner = runnerObject.AddComponent<NetworkRunner>();
                var sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();
                var objectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>();
                runner.AddCallbacks(this);

                StartGameResult result = await runner.StartGame(new StartGameArgs
                {
                    GameMode = mode,
                    SessionName = sessionName,
                    PlayerCount = 5,
                    Scene = SceneRef.FromIndex(lobbySceneBuildIndex),
                    SceneManager = sceneManager,
                    ObjectProvider = objectProvider,
                    EnableClientSessionCreation = false,
                    StartGameCancellationToken = cancellation.Token
                });
                if (this == null || applicationQuitting) return;
                if (cancellation.IsCancellationRequested)
                    failure = "연결 시간이 초과됐어요. 인터넷 연결을 확인하고 다시 시도해 주세요.";
                else if (!result.Ok || runner == null || !runner.IsRunning)
                    failure = FriendlyFailure(result.ShutdownReason);
                else
                    SetConnected();
            }
            catch (OperationCanceledException)
            {
                failure = "연결 시간이 초과됐어요. 인터넷 연결을 확인하고 다시 시도해 주세요.";
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                failure = "방에 연결하지 못했어요. 잠시 후 다시 시도해 주세요.";
            }
            finally
            {
                if (connectionCancellation == cancellation)
                    connectionCancellation = null;
            }
        }
        if (failure != null && this != null && !applicationQuitting)
        {
            // A failed Fusion runner is never reused. Keep input locked through cleanup.
            status = "연결을 정리하고 있어요...";
            try { await DisposeRunner(); }
            catch (Exception exception) { Debug.LogException(exception); }
            if (this != null) ReportError(failure);
        }
    }

    public async void StartGame()
    {
        if (Session != this) { Session.StartGame(); return; }
        if (IsBusy || !IsHost || !IsLobbySceneActive()) return;
        state = LobbySessionState.StartingGame;
        status = "게임을 불러오고 있어요...";
        try
        {
            await Task.Yield();
            if (this == null || applicationQuitting) return;
            if (runner == null || !runner.IsRunning)
                throw new InvalidOperationException("The session ended before loading the game.");
            NetworkSceneAsyncOp operation = runner.LoadScene(SceneRef.FromIndex(gameplaySceneBuildIndex));
            while (!operation.IsDone)
            {
                await Task.Yield();
                if (this == null || applicationQuitting) return;
                if (runner == null || !runner.IsRunning)
                    throw new InvalidOperationException("The session ended while loading the game.");
            }
            if (operation.Error != null) throw operation.Error;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (this != null)
                ReportError("게임을 불러오지 못했어요. 다시 시도하거나 방에서 나가 주세요.");
        }
    }

    public async void LeaveToMainMenu()
    {
        if (Session != this) { Session.LeaveToMainMenu(); return; }
        if (IsBusy) return;
        state = LobbySessionState.Leaving;
        status = "방에서 나가고 있어요...";
        try
        {
            await Task.Yield();
            await DisposeRunner();
            if (this == null || applicationQuitting) return;
            AsyncOperation operation = SceneManager.LoadSceneAsync("MainMenuScene");
            while (operation != null && !operation.isDone) await Task.Yield();
            activeSession = null;
            Destroy(gameObject);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (this != null) ReportError("나가기를 완료하지 못했어요. 다시 시도해 주세요.");
        }
    }

    private async Task DisposeRunner()
    {
        NetworkRunner previous = runner;
        if (previous == null) { runner = null; return; }
        previous.RemoveCallbacks(this);
        try { await previous.Shutdown(destroyGameObject: false); }
        finally
        {
            if (previous != null) Destroy(previous.gameObject);
            if (runner == previous) runner = null;
        }
    }

    private void SetConnected()
    {
        state = LobbySessionState.Connected;
        status = runner != null && runner.IsServer
            ? "준비되면 게임 시작 버튼을 눌러 주세요."
            : "방장이 게임을 시작하기를 기다리고 있어요.";
    }

    private void ReportError(string message)
    {
        state = runner != null && runner.IsRunning ? LobbySessionState.Connected : LobbySessionState.Error;
        status = message;
        lastError = message;
        errorVersion++;
    }

    private async void HandleDisconnect(NetworkRunner source, string message)
    {
        if (source != runner || IsBusy || applicationQuitting) return;
        state = LobbySessionState.Leaving;
        status = "끊어진 연결을 정리하고 있어요...";
        try
        {
            // Do not invoke Shutdown recursively inside a shutdown callback.
            await Task.Yield();
            if (this == null || applicationQuitting) return;
            await DisposeRunner();
        }
        catch (Exception exception) { Debug.LogException(exception); }
        if (this != null && !applicationQuitting) ReportError(message);
    }

    private static string FriendlyFailure(ShutdownReason reason)
    {
        // Keep engine details in logs, while explaining useful next steps to players.
        Debug.LogWarning($"[Lobby] Connection failed: {reason}");
        switch (reason.ToString())
        {
            case "GameNotFound": return "방을 찾지 못했어요. 방 코드를 확인해 주세요.";
            case "GameIsFull": return "방이 가득 찼어요. 다른 방에 참가해 주세요.";
            case "GameClosed": return "참가할 수 없는 방이에요. 방장에게 확인해 주세요.";
            case "GameIdAlreadyExists": return "방 코드가 겹쳤어요. 방 만들기를 다시 눌러 주세요.";
            case "PhotonCloudTimeout":
            case "ConnectionTimeout": return "서버 응답이 늦어지고 있어요. 인터넷 연결을 확인하고 다시 시도해 주세요.";
            default: return "방에 연결하지 못했어요. 방 코드와 인터넷 연결을 확인하고 다시 시도해 주세요.";
        }
    }

    private bool TrySetPlayerName(string playerName)
    {
        string name = (playerName ?? "").Trim();
        if (name.Length == 0) { ReportError("플레이어 이름을 입력해 주세요."); return false; }
        LocalPlayerName = name.Length <= 16 ? name : name.Substring(0, 16);
        PlayerPrefs.SetString("PlayerName", LocalPlayerName);
        PlayerPrefs.Save();
        return true;
    }

    public static bool IsValidRoomCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != 6) return false;
        foreach (char c in code)
            if (!(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9')) return false;
        return true;
    }

    private static string GenerateRoomCode()
    {
        const string characters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] code = new char[6];
        for (int i = 0; i < code.Length; i++)
            code[i] = characters[UnityEngine.Random.Range(0, characters.Length)];
        return new string(code);
    }

    private bool IsLobbySceneActive() => SceneManager.GetActiveScene().buildIndex == lobbySceneBuildIndex;
    private static int CountPlayers(NetworkRunner networkRunner)
    {
        int count = 0;
        foreach (PlayerRef ignored in networkRunner.ActivePlayers) count++;
        return count;
    }

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


    public void OnInput(NetworkRunner networkRunner, NetworkInput input)
    {
        if (IsBusy || (IsLobbySceneActive() && Cursor.lockState != CursorLockMode.Locked))
        {
            input.Set(default(NetworkInputData));
            return;
        }
        if (!networkRunner.TryGetPlayerObject(networkRunner.LocalPlayer, out NetworkObject playerObject) ||
            playerObject == null) return;
        PlayerMove move = playerObject.GetComponent<PlayerMove>();
        PlayerGameState playerState = playerObject.GetComponent<PlayerGameState>();
        if (move == null) return;
        input.Set(playerState != null && !playerState.IsInPlayground ? default : move.GetNetworkInput());
    }

    public void OnConnectedToServer(NetworkRunner r) { }
    public void OnConnectFailed(NetworkRunner r, NetAddress a, NetConnectFailedReason reason)
    {
        // StartSession owns cleanup and will report its final result.
        Debug.LogWarning($"[Lobby] Connect failed: {reason}");
    }
    public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason) =>
        HandleDisconnect(r, "방과의 연결이 끊어졌어요. 다시 참가해 주세요.");
    public void OnShutdown(NetworkRunner r, ShutdownReason reason) =>
        HandleDisconnect(r, "방이 종료됐어요. 다른 방을 만들거나 참가해 주세요.");

    public void OnSceneLoadStart(NetworkRunner r)
    {
        if (r != runner || state != LobbySessionState.Connected) return;
        state = LobbySessionState.StartingGame;
        status = "게임 화면을 불러오고 있어요...";
    }
    public void OnSceneLoadDone(NetworkRunner r)
    {
        if (r != runner) return;
        if (r.IsServer)
            foreach (PlayerRef player in r.ActivePlayers) OnPlayerJoined(r, player);
        if (state == LobbySessionState.StartingGame) SetConnected();
    }

    private void OnApplicationQuit() => applicationQuitting = true;
    private void OnDestroy()
    {
        if (activeSession != this) return;
        activeSession = null;
        connectionCancellation?.Cancel();
        if (runner != null)
        {
            runner.RemoveCallbacks(this);
            if (!applicationQuitting) _ = runner.Shutdown();
        }
    }

    public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner r, HostMigrationToken token) { }
    public void OnInputMissing(NetworkRunner r, PlayerRef p, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef p) { }
    public void OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef p) { }
    public void OnReliableDataProgress(NetworkRunner r, PlayerRef p, ReliableKey key, float progress) { }
    public void OnReliableDataReceived(NetworkRunner r, PlayerRef p, ReliableKey key, ArraySegment<byte> data) { }
    public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> sessions) { }
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
