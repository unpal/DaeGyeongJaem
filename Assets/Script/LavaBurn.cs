using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class LavaBurn : MonoBehaviour
{
    //용암 오브젝트에 붙이면 됩니다 이파일.
    public float tickInterval = 0.5f;
    public float burnDamage = 10f;

    private readonly Dictionary<PlayerCondition, float> nextDamageTime = new();

    private void OnTriggerStay(Collider other) //계속 머무르면 데미지 갱신
    {
        PlayerCondition condition = other.GetComponentInParent<PlayerCondition>();
        PlayerGameState state = other.GetComponentInParent<PlayerGameState>();

        if (condition == null || state == null || state.Object == null ||
            !state.Object.HasStateAuthority || !state.IsInPlayground)
            return;

        if (nextDamageTime.TryGetValue(condition, out float nextTime) &&
            Time.time < nextTime)
            return;

        nextDamageTime[condition] = Time.time + tickInterval;
        condition.ApplyTemporaryDamage(burnDamage);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerCondition condition = other.GetComponentInParent<PlayerCondition>();
        if (condition != null)
            nextDamageTime.Remove(condition);
    }
}
