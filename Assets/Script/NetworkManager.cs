using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkRunner runner;
    [SerializeField] private NetworkObject playerPrefab;

    public NetworkRunner Runner => runner;
    private void Awake()
    {
        if (runner == null)
            runner = GetComponent<NetworkRunner>();

        if (runner == null)
        {
            foreach (NetworkRunner existingRunner in NetworkRunner.Instances)
            {
                if (existingRunner != null && existingRunner.IsRunning)
                {
                    enabled = false;
                    return;
                }
            }

            Debug.LogError("NetworkManager와 같은 오브젝트에 NetworkRunner가 없습니다.");
            enabled = false;
            return;
        }

        runner.AddCallbacks(this);
    }

    public async void StartHost()
    {
        Debug.Log("[Flow Test] Host 시작 요청");

        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = "Room1",
            Scene = SceneRef.FromIndex(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex)
        });

        if (!result.Ok)
        {
            Debug.LogError($"[Flow Test] Host 시작 실패: {result.ShutdownReason}");
            return;
        }

        Debug.Log("[Flow Test] Host 시작 성공");

    }

    public async void StartClient()
    {
        await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = "Room1",
            Scene = SceneRef.FromIndex(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex)
        });
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player Joined : {player}");

        if (runner.IsServer)
            SpawnPlayerIfNeeded(runner, player);
    }

    private void SpawnPlayerIfNeeded(NetworkRunner runner, PlayerRef player)
    {
        if (runner.TryGetPlayerObject(player, out NetworkObject existingPlayer) &&
            existingPlayer != null)
            return;

        NetworkObject spawnedPlayer = runner.Spawn(
            playerPrefab,
            new Vector3(0, 5, 0),
            Quaternion.identity,
            player);

        runner.SetPlayerObject(player, spawnedPlayer);
        Debug.Log($"[Flow Test] 플레이어 생성 완료: {player}");
    }
    public void OnConnectedToServer(NetworkRunner runner)
    {
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerMove[] players = FindObjectsByType<PlayerMove>(
            FindObjectsSortMode.None);

        foreach (var player in players)
        {
            if (player.Object != null && player.Object.HasInputAuthority)
            {
                input.Set(player.GetNetworkInput());
                return;
            }
        }
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
    }

    private void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
