//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using Fusion;

//public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
//{
//    public NetworkObject PlayerPrefab;
//    private NetworkRunner runner;

//    private void Awake()
//    {
//        runner = GetComponent<NetworkRunner>();
//    }

//    public async void StartHost()
//    {
//        await runner.StartGame(...);
//    }

//    public async void StartClient()
//    {
//        await runner.StartGame(...);
//    }
//    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
//    {
//        if (runner.IsServer)
//        {
//            runner.Spawn(
//                PlayerPrefab,
//                new Vector3(0, 1, 0),
//                Quaternion.identity,
//                player
//            );
//        }
//    }


//    // Start is called before the first frame update
//    void Start()
//    {
        
//    }

//    // Update is called once per frame
//    void Update()
//    {
        
//    }
//}
