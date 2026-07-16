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

    private void OnTriggerEnter(Collider other)
    {
        PlayerCondition condition =
            other.GetComponent<PlayerCondition>();

        if (condition != null)
        {
            StartCoroutine(Burn(condition));
        }
    }

    IEnumerator Burn(PlayerCondition condition)
    {
        float timer = 0;

        while (timer < burnDuration)
        {
            condition.ApplyTemporaryDamage(burnDamage);

            yield return new WaitForSeconds(tickInterval);

            timer += tickInterval;
        }
    }
}