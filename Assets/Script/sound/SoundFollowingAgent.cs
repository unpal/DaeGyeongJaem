using System;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

/// <summary>
/// SoundEventManager에서 발생하는 소리 이벤트를 감지하고
/// 소리가 난 지점으로 NavMeshAgent를 이동시키는 스크립트입니다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class SoundFollowingAgent : MonoBehaviour
{
    private NavMeshAgent agent;
    public float correctness = 10.0f;
    public GameObject target;

    private void Awake()
    {
        // GameObject에 연결된 NavMeshAgent 컴포넌트를 가져옵니다.
        agent = GetComponent<NavMeshAgent>();
    }

    private void OnEnable()
    {
        // SoundEventManager의 OnSoundTriggered 이벤트에 구독(subscribe)합니다.
        // 이제 SoundEventManager.TriggerSound가 호출될 때마다 HandleSoundTriggered 메서드가 실행됩니다.
        SoundEventManager.OnSoundTriggered += HandleSoundTriggered;
    }

    private void OnDisable()
    {
        // 이 오브젝트가 비활성화되거나 파괴될 때, 이벤트 구독을 해제(unsubscribe)합니다.
        // 이렇게 하지 않으면 메모리 누수가 발생하거나 오류의 원인이 될 수 있습니다.
        SoundEventManager.OnSoundTriggered -= HandleSoundTriggered;
    }

    /// <summary>
    /// OnSoundTriggered 이벤트가 발생했을 때 호출될 메서드입니다.
    /// </summary>
    /// <param name="soundPosition">소리가 발생한 위치</param>
    /// <param name="soundRange">소리의 크기</param>
    private void HandleSoundTriggered(Vector3 soundPosition, float soundRange)
    {
        // 1. sqrMagnitude 대신 magnitude 사용
        var soundHeard = (transform.position - soundPosition).magnitude / soundRange;
        var randomPosition = soundPosition + new Vector3(Random.Range(-soundHeard, soundHeard), 0, Random.Range(-soundHeard, soundHeard)) / correctness;

        if (!agent || !agent.isActiveAndEnabled) return;
        // 2. randomPosition이 NavMesh 위인지 확인 (반경 2.0f 이내 검사)
        if (NavMesh.SamplePosition(randomPosition, out var hit, 2.0f, NavMesh.AllAreas))
        {
            // 유효한 위치라면 그곳으로 이동
            agent.SetDestination(hit.position);
            if (target)
                target.transform.position = hit.position;
        }
        else
        {
            // NavMesh를 크게 벗어났다면 원래 소리 위치 근처로 이동하게 예외 처리
            agent.SetDestination(soundPosition);
            if (target)
                target.transform.position = soundPosition;
        }
    }
}
