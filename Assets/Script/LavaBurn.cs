using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class LavaBurn : MonoBehaviour
{
    //용암 오브젝트에 붙이면 됩니다 이파일.
    public float burnDuration = 3f;
    public float tickInterval = 0.5f;
    public float burnDamage = 2f;

    private void OnTriggerStay(Collider other) //계속 머무르면 데미지 갱신
    {
        PlayerCondition condition =
            other.GetComponent<PlayerCondition>();

        if (condition != null)
        {
            condition.RefreshBurn();
        }
    }
    //Photon Fusion 2에서 TriggerEnter 사용할 시 TriggerEnter가 여러번 찍히는 문제가 발생해 주석처리함.
    //private void OnTriggerEnter(Collider other)
    //{
    //    PlayerCondition condition =
    //        other.GetComponent<PlayerCondition>();
    //    if (!condition.Object.HasStateAuthority)
    //        return;

    //    if (condition != null)
    //    {
    //        StartCoroutine(Burn(condition));
    //    }
    //}

    //IEnumerator Burn(PlayerCondition condition)
    //{
    //    float timer = 0;

    //    while (timer < burnDuration)
    //    {
    //        condition.ApplyTemporaryDamage(burnDamage);

    //        yield return new WaitForSeconds(tickInterval);

    //        timer += tickInterval;
    //    }
    //}
}