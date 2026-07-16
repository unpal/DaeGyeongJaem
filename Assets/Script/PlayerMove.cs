using Cinemachine;
using Fusion;
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
    public Transform WallLayCasterTrans;
    public bool isWall;
    public bool isGround;
    //public Animator Anim;

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
    private PlayerStamina stamina;
    private float timer; //디버깅용

    private PlayerCondition condition; // 용암,총,낙뎀 상황들

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
    private Vector3 wallNormal;
    [SerializeField] private float gravity = -0.0001f;
    [SerializeField] private float verticalVelocity;
    [SerializeField] private float edgePushForce = 3f;
    [SerializeField] private bool wasGrapped;
    [SerializeField] private float edgePushTime;
    [SerializeField] private float edgePushTimer;

    //추가한점,
    void Update()
    {
        isSprint = Keyboard.current.leftShiftKey.isPressed;
        isGrapple = attackAction.IsPressed();

        if (whistleAction.WasPressedThisFrame())
        {
            noise.Whistle();
        }

        timer += Time.deltaTime;

        if (timer >= 1f)
        {
            Debug.Log($"현재 스태미나 : {stamina.CurrentStamina}");
            timer = 0f;
        }
        attack = attackAction.IsPressed();
        sprint = SprintAction.IsPressed();
        jump = JumpAction.IsPressed();
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
        //추가
        stamina = GetComponent<PlayerStamina>();
        //추가
        condition = GetComponent<PlayerCondition>();
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
        if (!GetInput(out NetworkInputData data))
        {
            //Debug.Log("입력 없음");
            return;
        }
        bool sprintPressed = data.Buttons.IsSet((int)PlayerButtons.Sprint);
        transform.Rotate(
            Vector3.up * data.Look.x * sensitivity
        );
        if(!sprintPressed)
        {
            controller.maxSpeed = Speed;
        }
        else
        {
            controller.maxSpeed = RunSpeed;
        }
            float mouseY = data.Look.y * sensitivity;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        CameraObj.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        if (controller.gravity < 0)
        {
            Vector3 move =
                transform.forward * data.Move.y +
                transform.right * data.Move.x;

            controller.Move(move * Runner.DeltaTime);
        }
        else
        {

            Vector3 move =
            transform.up * data.Move.y +
            transform.right * data.Move.x;

            Vector3 stick = -wallNormal * 5f;

            controller.Move((move/* + stick*/)* Runner.DeltaTime);
        }
        if (data.Buttons.IsSet((int)PlayerButtons.Jump))
        {
            controller.Jump();
            jump = false;
        }
      //  Debug.Log($"Move:{data.Move} Look:{data.Look}");
        Climbing(data);
    }

    bool IsWall()
    {
        RaycastHit hit;
        if (Physics.Raycast(
            WallLayCasterTrans.position,
            WallLayCasterTrans.forward,
            out hit,
            0.8f,
            WallLayer))
        {
            wallNormal = hit.normal;
            isWall = true;
            return isWall;
        }
        isWall = false;
        return isWall;
    }

    private void Climbing(NetworkInputData data)
    {
        bool attackPressed = data.Buttons.IsSet((int)PlayerButtons.Attack);
        if (IsWall() && attackPressed && controller.gravity != 0 && !controller.IsDash)
        {
            controller.gravity = 0;
            controller.Velocity = Vector3.zero;
            wasGrapped = true;
            controller.IsClimbing = true;
        }
        else if ((!IsWall() || !attackPressed) && controller.gravity != -20)
        {
            controller.gravity = -20;
            controller.IsClimbing = false;
            if(attackPressed)
                controller.IsDash = true;
        }
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

            //소음발생, 착지. 근데 높이별로 소음제공량이 달라야할것같습니다.
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
