using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PrototypeLobbyBootstrap : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private int gameplaySceneBuildIndex = 3;
    [SerializeField] private string sessionName = "123TestRoundPrototype4P";

    private NetworkRunner runner;
    private string status = "H: Host / C: Client";
    private int lobbySceneBuildIndex;

    private void Awake()
    {
        lobbySceneBuildIndex = SceneManager.GetActiveScene().buildIndex;

        foreach (NetworkRunner existing in NetworkRunner.Instances)
        {
            if (existing != null && existing.IsRunning)
            {
                runner = existing;
                enabled = false;
                return;
            }
        }

        runner = gameObject.AddComponent<NetworkRunner>();
        gameObject.AddComponent<NetworkSceneManagerDefault>();
        gameObject.AddComponent<NetworkObjectProviderDefault>();
        runner.AddCallbacks(this);
    }

    private void Update()
    {
        if (!IsLobbySceneActive())
            return;

        if (runner == null || runner.IsRunning)
        {
            if (runner != null && runner.IsServer && Input.GetKeyDown(KeyCode.Return))
                runner.LoadScene(SceneRef.FromIndex(gameplaySceneBuildIndex));
            return;
        }

        if (Input.GetKeyDown(KeyCode.H))
            StartSession(GameMode.Host);
        else if (Input.GetKeyDown(KeyCode.C))
            StartSession(GameMode.Client);
    }

    private async void StartSession(GameMode mode)
    {
        status = mode == GameMode.Host ? "Host 접속 중..." : "Client 접속 중...";
        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = sessionName,
            PlayerCount = 4,
            Scene = SceneRef.FromIndex(gameObject.scene.buildIndex)
        });

        status = result.Ok
            ? (mode == GameMode.Host ? "Host 준비 완료 - Enter로 시작" : "Client 준비 완료")
            : $"접속 실패: {result.ShutdownReason}";
    }

    private void OnGUI()
    {
        if (!IsLobbySceneActive())
            return;

        GUI.Box(new Rect(20, 20, 430, 145), "4인 Round Prototype");
        GUI.Label(new Rect(40, 55, 390, 25), status);
        GUI.Label(new Rect(40, 82, 390, 25), "H: Host   C: Client   Enter: 게임 시작(Host)");
        int count = runner != null && runner.IsRunning ? CountPlayers(runner) : 0;
        GUI.Label(new Rect(40, 109, 390, 25), $"접속 인원: {count}/4");
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

    public void OnPlayerJoined(NetworkRunner networkRunner, PlayerRef player)
    {
        if (!networkRunner.IsServer || playerPrefab == null)
            return;

        if (networkRunner.TryGetPlayerObject(player, out NetworkObject existing) && existing != null)
            return;

        Vector3 position = PrototypeSpawnPoints.Get(player.PlayerId);
        NetworkObject spawned = networkRunner.Spawn(playerPrefab, position, Quaternion.identity, player);
        networkRunner.SetPlayerObject(player, spawned);
    }

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
        if (!networkRunner.TryGetPlayerObject(networkRunner.LocalPlayer, out NetworkObject playerObject) ||
            playerObject == null)
            return;

        PlayerMove move = playerObject.GetComponent<PlayerMove>();
        PlayerGameState state = playerObject.GetComponent<PlayerGameState>();
        input.Set(state != null && !state.IsInPlayground
            ? default
            : move.GetNetworkInput());
    }

    public void OnConnectedToServer(NetworkRunner r) { }
    public void OnConnectFailed(NetworkRunner r, NetAddress a, NetConnectFailedReason reason) { }
    public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
    public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason) { }
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
    public void OnShutdown(NetworkRunner r, ShutdownReason reason) { }
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
