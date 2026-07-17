using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class FallDamage : MonoBehaviour
{
    [Header("Reference")]
    private PlayerCondition condition;
    private PlayerGameState gameState;
    private NetworkCharacterController controller;

    [Header("Fall Damage")]
    public float safeHeight = 3f;      // 일단 맵 scaling 확인이 안되어서 이정도로 설정했습니다.
    public float damagePerMeter = 5f;  // 1m당 데미지

    //추가, 라운드시작 > 텔레포트시 낙하피해 적용x용 변수들
    private float highestPoint;
    private bool wasGrounded = true;
    private bool waitForInitialGrounding; // 라운드시작시 땅에 닿을때까지 기다리는 상태 추가

    void Start()
    {
        condition = GetComponent<PlayerCondition>();
        gameState = GetComponent<PlayerGameState>();
        controller = GetComponent<NetworkCharacterController>();
        highestPoint = transform.position.y;
    }

    void Update()
    {
        if (gameState == null || gameState.Object == null ||
            !gameState.Object.HasStateAuthority || !gameState.IsInPlayground)
            return;

        bool grounded = IsGrounded();

        //따라서 라운드 텔레포트를 높은 곳에서 떨어진것으로 착각하지 않는다!!!!!!!!!
        if (waitForInitialGrounding)
        {
            highestPoint = transform.position.y;
            wasGrounded = grounded;

            if (grounded)
                waitForInitialGrounding = false;

            return;
        }

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
            }

            highestPoint = transform.position.y;
        }

        wasGrounded = grounded;
    }

    bool IsGrounded()
    {
        if (controller != null)
            return controller.Grounded;

        return Physics.Raycast( //일단 raycast 하나 더 쓰긴했는데 playermove의 착지확인부분을 빌려서 써도 될것같습니다. 추후 수정 문의
            transform.position,
            Vector3.down,
            1.2f);
    }

    //플레이어가 처음 땅에 닿기 전까지는 현재 높이만 갱신.
    public void ResetForNextRound()
    {
        highestPoint = transform.position.y;
        wasGrounded = true;
        waitForInitialGrounding = true;
    }
}
