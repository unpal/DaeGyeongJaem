using Cinemachine;
using ExitGames.Client.Photon.StructWrapping;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;


public class PlayerMove : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public bool isJump;
    public Transform target;
    public float Speed;
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

    private PlayerStamina stamina;
    private float timer; //디버깅용

    private PlayerCondition condition; // 용암,총,낙뎀 상황들
    private PlayerGameState gameState;
    private bool jumpWasPressed;

    //taunt 기능
    public InputAction whistleAction;
    public bool whistle;
    public float RunSpeed; // 달리기 속도
    public bool isRun; // 달리고 있는지 체크
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
    private Vector3 wallNormal;
    public PlayerStaminaUI playerStaminaUI;
    [Networked]
    private NetworkButtons PreviousButtons { get; set; }
    private bool whistlePressed;

    //추가한점,
    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= 1f)
        {
            //Debug.Log($"현재 스태미나 : {stamina.CurrentStamina}");
            timer = 0f;
        }
        //그랩 키 눌렀는지 확인
        attack = attackAction.IsPressed();
        //달리기 키 눌렀는지 확인
        sprint = SprintAction.IsPressed();
        //점프 키 눌렀는지 확인
        jump = JumpAction.IsPressed();
        //휫바람소리 눌렀는지 확인
        whistle = whistleAction.WasPressedThisFrame();
    }
    public override void Spawned()
    {
        SprintAction = playerInput.actions["Sprint"];
        JumpAction = playerInput.actions["Jump"];
        Debug.Log($"{Runner.LocalPlayer} / {Object.InputAuthority} / {Object.HasInputAuthority}");
        bool isMine = Object.HasInputAuthority;
        controller = GetComponent<NetworkCharacterController>();
        //추가
        stamina = GetComponent<PlayerStamina>();
        //추가
        condition = GetComponent<PlayerCondition>();
        gameState = GetComponent<PlayerGameState>();
        //추가2
        noise = GetComponent<PlayerNoise>();
        //추가3
        whistleAction = playerInput.actions["Whistle"];

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
    private void OnWhistle(InputValue value)
    {
        if (value.isPressed)
            whistlePressed = true;
    }
    //Client가 보낸 입력값을 Host가 받는 함수
    public NetworkInputData GetNetworkInput()
    {
        NetworkInputData data = new();

        data.Move = inputVec;
        data.Look = lookVec;
        data.Buttons.Set((int)PlayerButtons.Jump, jump);
        data.Buttons.Set((int)PlayerButtons.Attack, attack);
        data.Buttons.Set((int)PlayerButtons.Sprint, sprint);
        data.Buttons.Set((int)PlayerButtons.Whistle, whistlePressed);
        whistlePressed = false;
        return data;
    }
    //달리기 체크(스프린트)
    private void SprintCheck(NetworkInputData data)
    {
        if (!data.Buttons.IsSet((int)PlayerButtons.Sprint) //달리기 버튼 눌렀는지 확인
            || !stamina.HasStamina(sprintDrain*Runner.DeltaTime))//스테미나 있는지 확인
        {
            controller.maxSpeed = Speed;
            isRun = false;
        }
        else
        {
            controller.maxSpeed = RunSpeed;
            isRun = true;
        }
    }
    //카메라 무빙
    private void CameraMoving(NetworkInputData data)
    {
        float mouseY = data.Look.y * sensitivity;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        CameraObj.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
    //휫바람 버튼 푸시 체크
    private void WhistleCheck(NetworkInputData data)
    {
        NetworkButtons pressed = data.Buttons.GetPressed(PreviousButtons);

        if (pressed.IsSet((int)PlayerButtons.Whistle))
        {
            SoundEventManager.TriggerSound(transform.position, 20.0f);
            noise.Whistle();
        }

        PreviousButtons = data.Buttons;
    }
    //wasd를 이용한 앞 뒤 이동
    private void MovingCheck(NetworkInputData data)
    {
        Vector3 move =
        transform.forward * data.Move.y +
        transform.right * data.Move.x;

        controller.Move(move * Runner.DeltaTime);
        if (isRun && move.sqrMagnitude > 0.01f)
        {
            stamina.UseStamina(sprintDrain * Runner.DeltaTime);
            SoundEventManager.TriggerSound(transform.position, 5.0f);
        }
    }
    //wasd를 이용한 등반 이동
    private void ClimbCheck(NetworkInputData data)
    {
        Vector3 move =
        transform.up * data.Move.y +
        transform.right * data.Move.x;

        Vector3 stick = -wallNormal * 5f;
        if (!stamina.HasStamina(climbDrain * Runner.DeltaTime))
            move.y = -1;
        controller.Move((move/* + stick*/) * Runner.DeltaTime);
        stamina.UseStamina(climbDrain * Runner.DeltaTime);
    }
    //네트워크 오브젝트 이기에 Update에서 FixedUpdateNetwork()로 변경
    public override void FixedUpdateNetwork()
    {
        //호스트에게 입력값 받아오기
        if (!GetInput(out NetworkInputData data))
        {
            return;
        }
        // 마우스 이동을 통한 플레이어 회전
        transform.Rotate(Vector3.up * data.Look.x * sensitivity);
        //달리기 체크
        SprintCheck(data);
        WhistleCheck(data);
        CameraMoving(data);
        if (!controller.IsClimbing)
        {
            MovingCheck(data);
        }
        else
        {
            ClimbCheck(data);
        }
        //피크가 스테미나가 요구 수치만큼 없어도 점프가 되는 것을 보고 스테미나가 0이 아닐 시 점프가 되도록 설정
        if (data.Buttons.IsSet((int)PlayerButtons.Jump) && stamina.HasStamina(0.1f))
        {
            bool isJump = controller.Jump();
            if (isJump)
            {
                SoundEventManager.TriggerSound(transform.position, 10.0f);
                noise.MakeNoise(NoiseType.Jump);
                stamina.UseStamina(10f);
            }
        }
        if (controller.Grounded && !controller.IsClimbing && !isRun)
        {
            stamina.RecoverStamina(10f * Time.fixedDeltaTime);
        }

        Climbing(data);

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

    //내 팔이 벽을 잡을 수 있는지 체크
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
    //
    private void Climbing(NetworkInputData data)
    {
        bool attackPressed = data.Buttons.IsSet((int)PlayerButtons.Attack);
        if (IsWall() && attackPressed && !controller.IsClimbing && !controller.IsDash)
        {
            controller.Velocity = Vector3.zero;
            controller.IsClimbing = true;
        }
        else if ((!IsWall() || !attackPressed) && controller.IsClimbing)
        {
            controller.IsClimbing = false;
            if(attackPressed && isWall)
                controller.IsDash = true;
        }

        return canClimb;
    }

    private void OnCollisionEnter(Collision other)
    {

        if (other.collider.tag == "Ground")
        {
            isJump = true;

            //소음발생, 착지. 근데 높이별로 소음제공량이 달라야할것같습니다. << 음. 낙하데미지 코드 조금 참고해서 수정해볼게요
            //저거 코드 참고해서 변수 가져온다음에 간단한 조건문 쓰기
            noise.MakeNoise(NoiseType.Land);
        }
    }
}
