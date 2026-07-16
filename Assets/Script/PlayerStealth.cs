using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerStealth : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private PlayerMove move;
    [SerializeField] private PlayerNoise noise;

    [Header("Stealth")]
    [SerializeField] private float crouchSpeedMultiplier = 0.5f;

    private float originalSpeed;

    public bool IsCrouching { get; private set; }

    private void Start()
    {
        if (move == null)
            move = GetComponent<PlayerMove>();

        if (noise == null)
            noise = GetComponent<PlayerNoise>();

        originalSpeed = move.Speed;
    }

    public void OnCrouch(InputValue value)
    {
        if (value.isPressed)
            EnterStealth();
        else
            ExitStealth();
    }

    private void EnterStealth()
    {
        if (IsCrouching)
            return;

        IsCrouching = true;

        move.Speed = originalSpeed * crouchSpeedMultiplier;

        noise.SetCrouching(true);

        Debug.Log("은신 시작");
        //transform.localScale = new Vector3(1f, 0.7f, 1f); 하거나
        //CapsuleCollider.height = 1.2f; 로 변경하면 낮은공간도 지나갈수있.. 물론 capsulecolider 가 아니겠지만..
    }

    private void ExitStealth()
    {
        if (!IsCrouching)
            return;

        IsCrouching = false;

        move.Speed = originalSpeed;

        noise.SetCrouching(false);

        Debug.Log("은신 종료");
    }
}