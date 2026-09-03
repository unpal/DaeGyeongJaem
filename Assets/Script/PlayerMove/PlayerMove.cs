using Cinemachine;
using Fusion;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    public float Speed
    {
        get => speed;
        set => speed = value;
    }
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float ClimbForceTime = 0.05f;

    [Header("Sensitivity")]
    [SerializeField] private float sensitivity = 0.1f;

    [Header("Stamina")]
    [SerializeField] private float sprintDrain = 15f;
    [SerializeField] private float jumpCost = 10f;
    [SerializeField] private float climbDrain = 20f;
    [SerializeField] private float recoverRate = 10f;

    [Header("Unity References")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private CinemachineVirtualCamera playerCamera;
    [SerializeField] private Animator animator;

    [Header("Climbing References")]
    [SerializeField] private Transform wallCaster;
    [SerializeField] private LayerMask wallLayer;

    [Header("Sound")]
    [SerializeField] private AudioClip footSound;
    [SerializeField] private float footSoundRange = 5f;

    private NetworkCharacterController controller;
    private PlayerCondition condition;
    private PlayerGameState gameState;
    private PlayerNoise noise;
    private FallDamage fallDamage;

    private PlayerInputHandler input;
    private PlayerCamera cameraController;
    private PlayerMovement movement;
    private PlayerClimbing climbing;
    private PlayerAnimation animationController;
    private PlayerFootstep footstep;

    private bool jumpWasPressed;

    [Networked]
    private NetworkBool IsRunning { get; set; }

    public override void Spawned()
    {
        controller =
            GetComponent<NetworkCharacterController>();

        condition =
            GetComponent<PlayerCondition>();

        gameState =
            GetComponent<PlayerGameState>();

        noise =
            GetComponent<PlayerNoise>();

        fallDamage =
            GetComponent<FallDamage>();

        bool isMine =
            Object.HasInputAuthority;

        // 일반 클래스 생성

        input =
            new PlayerInputHandler(
                playerInput);

        cameraController =
            new PlayerCamera(
                playerCamera,
                sensitivity);

        movement =
            new PlayerMovement(
                controller,
                condition,
                speed,
                runSpeed,
                sprintDrain);

        climbing =
            new PlayerClimbing(
                controller,
                condition,
                gameState,
                fallDamage,
                wallCaster,
                wallLayer,
                climbDrain,
                ClimbForceTime);

        animationController =
            new PlayerAnimation(
                animator);

        footstep =
            new PlayerFootstep(
                animationController,
                footSoundRange);

        // Local Player 설정

        input.SetEnabled(isMine);

        cameraController.SetActive(isMine);

        if (isMine)
            cameraController.Reset();

        Debug.Log(
            $"{Runner.LocalPlayer} / " +
            $"{Object.InputAuthority} / " +
            $"{Object.HasInputAuthority}");
    }

    private void Update()
    {
        if (Object == null ||
            !Object.HasInputAuthority)
        {
            return;
        }

        input.Update();

        cameraController.UpdateLook(
            input.Look);

        if (input.WhistlePressed)
        {
            noise.Whistle();
        }
    }

    public NetworkInputData GetNetworkInput()
    {
        return input.GetNetworkInput();
    }

    public override void FixedUpdateNetwork()
    {
        if (gameState != null &&
            !gameState.IsInPlayground)
        {
            return;
        }

        if (!GetInput(
            out NetworkInputData data))
        {
            return;
        }

        float deltaTime =
            Runner.DeltaTime;

        // 회전

        transform.Rotate(
            Vector3.up *
            data.Look.x *
            sensitivity);

        // 점프

        bool jumpPressed =
            data.Buttons.IsSet(
                (int)PlayerButtons.Jump);

        if (jumpPressed &&
            !jumpWasPressed &&
            controller.Grounded &&
            condition != null &&
            condition.CanUseStamina(jumpCost))
        {
            condition.TryUseStamina(
                jumpCost);

            controller.Jump();
        }

        jumpWasPressed =
            jumpPressed;

        // 등반

        bool isClimbing =
            climbing.UpdateState(data);

        climbing.UpdateForce(deltaTime);

        // 이동

        Vector3 move;

        if (isClimbing)
        {
            move =
                climbing.Move(
                    data,
                    deltaTime);
        }
        else
        {
            move =
                movement.Move(
                    data,
                    deltaTime,
                    false);
        }

        // 달리기

        bool canSprint =
            data.Buttons.IsSet(
                (int)PlayerButtons.Sprint) &&
            data.Move.sqrMagnitude > 0.01f &&
            !isClimbing &&
            condition != null &&
            condition.CanSprint;

        // 애니메이션

        IsRunning =
            move.sqrMagnitude > 0.01f &&
            !isClimbing;

        animationController.Update(
            move,
            isClimbing,
            canSprint);

        // 발소리
        int FootStepNumber = footstep.Update(transform.position,isClimbing,canSprint);
        if (FootStepNumber == 1)
        {
            Rpc_PlayFootstep(
                transform.position);

            SoundEventManager.TriggerSound(
                transform.position,
                footstep.SoundRange);
        }
        else if(FootStepNumber == 2)
        {
            climbing.SetIsForce(true);
        }
        else
        {

        }

            // 스태미나 회복

            bool tryingToSprint =
                data.Buttons.IsSet(
                    (int)PlayerButtons.Sprint) &&
                data.Move.sqrMagnitude > 0.01f;

        if (!canSprint &&
            !tryingToSprint &&
            !isClimbing &&
            condition != null)
        {
            condition.RecoverStamina(
                recoverRate * deltaTime);
        }
    }

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All)]
    private void Rpc_PlayFootstep(
        Vector3 position)
    {
        if (footSound == null)
            return;

        AudioSource.PlayClipAtPoint(
            footSound,
            position);
    }

    public void ResetForNextRound()
    {
        input.Reset();

        jumpWasPressed = false;

        if (controller != null)
        {
            controller.Velocity =
                Vector3.zero;

            controller.gravity =
                -20f;

            controller.IsClimbing =
                false;

            controller.IsDash =
                false;
        }

        climbing.Reset();
        animationController.Reset();
        footstep.ResetStep();
        footstep.ResetForce();
        cameraController.Reset();
    }

    public bool isPlayerInput()
    {
        if (playerInput == null)
            return false;
        else
            return true;
    }
    public void PlayerInputSetting(bool isPlayerInput)
    {
        playerInput.enabled = isPlayerInput;
    }
}