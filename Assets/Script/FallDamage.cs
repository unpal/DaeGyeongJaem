using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallDamage : NetworkBehaviour
{
    [Header("Reference")]
    private PlayerCondition condition;
    private NetworkCharacterController characterController;

    [Header("Fall Damage")]
    public float safeHeight = 3f;      // 일단 맵 scaling 확인이 안되어서 이정도로 설정했습니다.
    public float damagePerMeter = 5f;  // 1m당 데미지

    private float highestPoint;
    private bool wasGrounded = true;

    public override void Spawned()
    {
        condition = GetComponent<PlayerCondition>();
        characterController = GetComponent<NetworkCharacterController>();   
        highestPoint = transform.position.y;
    }
    public override void FixedUpdateNetwork()
    {
        bool grounded = IsGrounded();

        // 공중일 때 가장 높은 위치 저장
        if (!grounded)
        {
            highestPoint = Mathf.Max(highestPoint, transform.position.y);
        }

        // 착지 순간
        if (!wasGrounded && grounded)
        {
            float fallDistance = highestPoint - transform.position.y;

            if (fallDistance > safeHeight)
            {
                float damage =
                    (fallDistance - safeHeight) * damagePerMeter;

                Debug.Log($"낙하 거리 : {fallDistance:F1}m");
                Debug.Log($"낙하 데미지 : {damage:F1}");

                condition.ApplyPermanentDamage(damage);
                SoundEventManager.TriggerSound(transform.position, 10.0f);
            }

            highestPoint = transform.position.y;
        }

        wasGrounded = grounded;
    }

    bool IsGrounded()
    {
        return Physics.Raycast( //일단 raycast 하나 더 쓰긴했는데 playermove의 착지확인부분을 빌려서 써도 될것같습니다. 추후 수정 문의
            transform.position,
            Vector3.down,
            1.2f);
    }
}