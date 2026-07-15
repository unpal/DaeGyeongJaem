using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerInput 컴포넌트로부터 WASD 입력을 받아 플레이어를 움직이는 간단한 스크립트입니다.
/// 이 스크립트가 제대로 동작하려면 GameObject에 Rigidbody와 PlayerInput 컴포넌트가 반드시 필요합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerSimpleMove : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField]
    private float speed = 5f;

    private Rigidbody rb;
    private Vector2 moveInput;

    private void Awake()
    {
        // GameObject에 연결된 Rigidbody 컴포넌트를 가져옵니다.
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// PlayerInput 컴포넌트가 "Move" 액션을 감지했을 때 호출되는 메서드입니다.
    /// (PlayerInput Actions 설정에서 "Move" 액션이 정의되어 있어야 합니다.)
    /// </summary>
    private void OnMove(InputValue value)
    {
        // 입력값 (Vector2)을 moveInput 변수에 저장합니다.
        moveInput = value.Get<Vector2>();
    }

    private void FixedUpdate()
    {
        // 물리 업데이트 주기에 맞춰 플레이어를 움직입니다.

        // 2D 입력(좌/우, 앞/뒤)을 3D 월드 공간의 움직임으로 변환합니다.
        // x축은 좌/우(moveInput.x), z축은 앞/뒤(moveInput.y)를 담당합니다.
        Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);

        // Rigidbody의 속도를 변경하여 플레이어를 움직입니다.
        // 기존의 y축 속도(중력 등)는 유지하면서 x와 z축 속도만 새로 계산하여 적용합니다.
        rb.velocity = new Vector3(movement.x * speed, rb.velocity.y, movement.z * speed);
    }
}
