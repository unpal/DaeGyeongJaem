using Cinemachine;
using Fusion;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerMove : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public bool isJump;
    public Transform target;
    public float Speed;
    public bool Uping;
    public bool isGrapple;
    public PlayerInput playerInput;
    public InputAction attackAction;
    [SerializeField] private LayerMask WallLayer;
    public bool isWall;
    public bool isGround;
    public Animator Anim;

    //함수
    //bool useStamina(float amount),
    //recoverStamina(float amount),
    //bool HasStamina(float amount)

    //소음기능
    private PlayerNoise noise;

    //추가한점,
    [Header("Sprint")]
    public float sprintMultiplier = 1.5f; //달리기속도
    public float sprintDrain = 15f; //달리는데 소모되는 스태미너량

    [Header("Jump")]
    public float jumpCost = 10f;

    [Header("Climb")]
    [SerializeField]
    public float climbDrain = 20f;

    [Header("Recover")]
    public float recoverRate = 10f;

    //나중에 밸런스 패치 편하도록 위처럼 magicnum 모아두는것도 괜찮을듯 싶지만.. 아래에 수정 안해놨음.

    //소음 호출용 타이머,매 틱마다 호출x 일정 시간간격으로.
    private float runNoiseTimer;
    private float climbNoiseTimer;

    private bool isSprint; // state 체크
    private PlayerCondition condition; // 용암,총,낙뎀 상황들
    private PlayerGameState gameState;
    private bool jumpWasPressed;

    //taunt 기능
    public InputAction whistleAction;
    public float RunSpeed;
    public bool isRun;
    private InputAction SprintAction;
    private InputAction JumpAction;
    [SerializeField] private CinemachineVirtualCamera playerCamera;
    private Vector2 inputVec;
    private NetworkCharacterController controller;
    [SerializeField] private bool jump;
    [SerializeField] private bool attack;
    [SerializeField] private bool sprint;
    [SerializeField] private float sensitivity = 0.1f;
    private Vector2 lookVec;
    private float xRotation = 0f;
    [SerializeField] private GameObject CameraObj;
    private CharacterController cc;
    [SerializeField] private float gravity = -0.0001f;
    [SerializeField] private float verticalVelocity;
    [SerializeField] private float edgePushForce = 3f;
    [SerializeField] private bool wasGrapped;
    [SerializeField] private float edgePushTime;
    [SerializeField] private float edgePushTimer;
    [SerializeField] private float HeadUpMove;
    //애니메이션 체크용
    [SerializeField] private Animator animator;
    [SerializeField] private bool isRunSound;
    //추가한점,

void Update()
    {
        if (Object == null || !Object.HasInputAuthority)
            return;

        isSprint = Keyboard.current.leftShiftKey.isPressed;
        isGrapple = attackAction.IsPressed();

        if (whistleAction.WasPressedThisFrame())
        {
            noise.Whistle();
        }

        attack = attackAction.IsPressed();
        sprint = SprintAction.IsPressed();
        jump = JumpAction.IsPressed();

        float mouseY = lookVec.y * sensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90, 90);

        CameraObj.transform.localRotation =
            Quaternion.Euler(xRotation, 0, 0);
    }

    public override void Spawned()
    {
        SprintAction = playerInput.actions["Sprint"];
        JumpAction = playerInput.actions["Jump"];
        Debug.Log($"{Runner.LocalPlayer} / {Object.InputAuthority} / {Object.HasInputAuthority}");
        bool isMine = Object.HasInputAuthority;
        controller = GetComponent<NetworkCharacterController>();
        isJump = true;
        cc = GetComponent<CharacterController>();
        condition = GetComponent<PlayerCondition>();
        gameState = GetComponent<PlayerGameState>();
        //추가2
        noise = GetComponent<PlayerNoise>();
        //추가3
        whistleAction = playerInput.actions["Whistle"];

        isJump = true;
        attackAction = playerInput.actions["Attack"];
        playerCamera.gameObject.SetActive(isMine);

        playerInput.enabled = isMine;
    }
    private void OnMove(InputValue value)
    {
        inputVec = value.Get<Vector2>();
    }
    private void OnLook(InputValue value)
    {
        lookVec = value.Get<Vector2>();
    }
    public NetworkInputData GetNetworkInput()
    {
        NetworkInputData data = new();

        data.Move = inputVec;
        data.Look = lookVec;
        data.Buttons.Set((int)PlayerButtons.Jump, jump);
        data.Buttons.Set((int)PlayerButtons.Attack, attack);
        data.Buttons.Set((int)PlayerButtons.Sprint, sprint);

        return data;
    }

    public override void FixedUpdateNetwork()
    {
        if (gameState != null && !gameState.IsInPlayground)
        {
            return;
        }

        if (!GetInput(out NetworkInputData data))
        {
            //Debug.Log("입력 없음");
            return;
        }
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        if (state.IsName("Run"))
        {
            float time = state.normalizedTime;

            if (time >= 0.1f && !isRunSound)
            {
                isRunSound = true;
                SoundEventManager.TriggerSound(transform.position, 5.0f);
                Debug.Log("소음 발생");
            }
            else if (time < 0.1f)
            {
                isRunSound = false;
            }
        }
        else
        {
            isRunSound = false;
        }
        if(state.IsName("Climb"))
        {

        }
        bool sprintPressed = data.Buttons.IsSet((int)PlayerButtons.Sprint);
        bool hasMovementInput = data.Move.sqrMagnitude > 0.01f;

        if (gameState != null && condition != null)
            gameState.SetMaxStamina(condition.CurrentMaxStamina);

        bool canSprint = sprintPressed &&
                         hasMovementInput &&
                         condition != null &&
                         condition.CanSprint &&
                         gameState != null &&
                         gameState.TryUseStamina(sprintDrain * Runner.DeltaTime);
        transform.Rotate(Vector3.up * data.Look.x * sensitivity);
        if(!canSprint)
        {
            controller.maxSpeed = Speed;
        }
        else
        {
            controller.maxSpeed = RunSpeed;
        }


        if (!controller.IsClimbing)
        {
            Vector3 move =
                transform.forward * data.Move.y +
                transform.right * data.Move.x;

            controller.Move(move * Runner.DeltaTime);
        }
        else
        {
            Vector3 wallUp = Vector3.up;
            Vector3 wallRight = Vector3.Cross(wallUp, controller.wallNormal).normalized;

            Vector3 move =
                wallUp * data.Move.y +
                wallRight * -data.Move.x;


            if (gameState.TryUseStamina(climbDrain * Runner.DeltaTime))
            {
                //Debug.Log(move);
                //if(wallDistance > 0.15f)
                //{
                //    controller.Move((-wallNormal * 0.2f) * Runner.DeltaTime);
                //}
                controller.Move(move * Runner.DeltaTime);
            }
            else
            {
                move.y -= 2;
                //if (wallDistance > 0.15f)
                //{
                //    controller.Move((-wallNormal * 0.2f) * Runner.DeltaTime);
                //}
                controller.Move(move * Runner.DeltaTime);
            }
            
        }
        bool jumpPressed = data.Buttons.IsSet((int)PlayerButtons.Jump);
        if (jumpPressed && !jumpWasPressed && controller.Grounded &&
            gameState != null && gameState.TryUseStamina(jumpCost))
        {
            controller.Jump();
            jump = false;
        }
        jumpWasPressed = jumpPressed;
      //  Debug.Log($"Move:{data.Move} Look:{data.Look}");
        bool isClimbingNow = Climbing(data);

        if (Anim != null)
        {
            Anim.SetBool("Climbing", isClimbingNow);
            Anim.SetBool("Runing", canSprint);
        }

        if (!canSprint && !isClimbingNow && gameState != null)
            gameState.RecoverStamina(recoverRate * Runner.DeltaTime);


    }
    public void ResetForNextRound()
    {
        inputVec = Vector2.zero;
        lookVec = Vector2.zero;
        jump = false;
        attack = false;
        sprint = false;
        isSprint = false;
        isGrapple = false;
        isWall = false;
        Uping = false;
        wasGrapped = false;
        edgePushTimer = 0f;
        verticalVelocity = 0f;
        xRotation = 0f;
        jumpWasPressed = false;

        if (controller != null)
        {
            controller.Velocity = Vector3.zero;
            controller.gravity = -20f;
            controller.IsClimbing = false;
            controller.IsDash = false;
        }

        if (CameraObj != null)
            CameraObj.transform.localRotation = Quaternion.identity;
    }


 
    private bool Climbing(NetworkInputData data)
    {
        bool attackPressed = data.Buttons.IsSet((int)PlayerButtons.Attack);
        bool canClimb = controller.IsWall() &&
                        attackPressed &&
                        !controller.IsDash &&
                        gameState != null;

        if (canClimb && !controller.IsClimbing)
        {
            controller.Velocity = Vector3.zero;
            wasGrapped = true;
            controller.IsClimbing = true;
        }
        else if (!canClimb && controller.IsClimbing)
        {
            controller.IsClimbing = false;
            if(attackPressed && !isWall)
                controller.IsDash = true;
        }

        return canClimb;
    }

    private void OnCollisionEnter(Collision other)
    {
        //Debug.Log("콜라이더 발생");
        if (other.collider.tag == "Wall")
        {
            Uping = true;
        }
        if (other.collider.tag == "Ground")
        {
            isJump = true;

            //소음발생, 착지. 근데 높이별로 소음제공량이 달라야할것같습니다. << 음. 낙하데미지 코드 조금 참고해서 수정해볼게요
            //저거 코드 참고해서 변수 가져온다음에 간단한 조건문 쓰기
            noise.MakeNoise(NoiseType.Land);

            //Anim.SetBool("Jump", false);
        }
    }
    private void OnCollisionExit(Collision other)
    {
        if (other.collider.tag == "Wall")
        {
            Uping = false;
        }
    }
    void OnJump(InputValue value)
    {
        if (value.isPressed)
        {

            //여기 추후에 변경하면 좋을듯한 방식
            /*
             
                if (isJump && stamina.UseStamina(10f)) <<<<<<<<<<<<<조건문만 변경.
                    {
                        Rigid.AddForce(Vector3.up * 5, ForceMode.Impulse);
                        isJump = false;
                    }   
             
             */


            if (isJump)
            {
                //Rigid.AddForce(Vector3.up * 5, ForceMode.Impulse);

                //소음추가
                noise.MakeNoise(NoiseType.Jump);

                isJump = false;
                //Anim.SetBool("Jump", true);
            }
        }
    }
}
