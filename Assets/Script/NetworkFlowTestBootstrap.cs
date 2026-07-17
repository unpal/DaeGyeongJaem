using System.Collections;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public class NetworkFlowTestBootstrap : MonoBehaviour
{
    [SerializeField] private int dotoriSceneBuildIndex = 1;

    private IEnumerator Start()
    {
        foreach (NetworkRunner existingRunner in NetworkRunner.Instances)
        {
            if (existingRunner != null && existingRunner.IsRunning)
            {
                Debug.Log("[Flow Test] 실행 중인 Runner가 있어 테스트 Host 시작을 건너뜁니다.");
                yield break;
            }
        }

        NetworkManager networkManager = GetComponent<NetworkManager>();
        networkManager.StartHost();

        yield return new WaitUntil(() =>
            networkManager.Runner != null && networkManager.Runner.IsRunning);

        if (!networkManager.Runner.IsServer)
            yield break;

        Debug.Log($"[Flow Test] DotoriScene 이동 요청: Build Index {dotoriSceneBuildIndex}");
        networkManager.Runner.LoadScene(SceneRef.FromIndex(dotoriSceneBuildIndex));
    }
}
