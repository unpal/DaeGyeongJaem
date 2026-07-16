using System.Net.NetworkInformation;
using Unity.VisualScripting;
using UnityEditor.Timeline.Actions;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
public class PlayerMove : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    Rigidbody Rigid;
    Vector3 InputVec;
    public bool isJump;
    public Transform target;
    public float Speed;
    public bool Uping;
    public bool isGrapple;
    public PlayerInput playerInput;
    public InputAction attackAction;
    [SerializeField] private LayerMask WallLayer;
    public Transform WallLayCasterTrans;
    public Transform GroundLayCasterTrans;
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

    void Start()
    {
        //추가
        stamina = GetComponent<PlayerStamina>();
        //추가
        condition = GetComponent<PlayerCondition>();
        //추가2
        noise = GetComponent<PlayerNoise>();
        //추가3
        whistleAction = playerInput.actions["Whistle"];

        Rigid = GetComponent<Rigidbody>();
        isJump = true;
        attackAction = playerInput.actions["Attack"];
    }

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
    }

    private void OnMove(InputValue value)
    {
        InputVec = value.Get<Vector2>();
        //Debug.Log(InputVec);
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
                Rigid.AddForce(Vector3.up * 5, ForceMode.Impulse);

                //소음추가
                noise.MakeNoise(NoiseType.Jump);

                isJump = false;
                //Anim.SetBool("Jump", true);
            }
        }
    }


    private void ClimbMove()
    {
        Vector3 move =
            transform.up * InputVec.y +
            transform.right * InputVec.x;

        Vector3 velocity = Rigid.velocity;

        velocity.x = move.x * Speed;
        velocity.y = move.y * Speed;
        velocity.z = move.z * Speed;

        Rigid.velocity = velocity;
    }   

    private void HandleMovement()
    {
        Vector3 move;
        //소음
        runNoiseTimer += Time.fixedDeltaTime;

        if (isSprint && isMoving)
        {
            if (runNoiseTimer >= 0.4f)
            {
                noise.MakeNoise(NoiseType.Run);
                runNoiseTimer = 0;
            }
        }
        else if (isMoving)
        {
            if (runNoiseTimer >= 0.8f)
            {
                noise.MakeNoise(NoiseType.Walk);
                runNoiseTimer = 0;
            }
        }

        if (Rigid.useGravity)
        {
            move =
                transform.forward * InputVec.y +
                transform.right * InputVec.x;

            Vector3 velocity = Rigid.velocity;

            float currentSpeed = Speed;

            bool isMoving = InputVec.sqrMagnitude > 0.01f;

            if (isSprint &&
                isMoving &&
                stamina.HasStamina(sprintDrain * Time.fixedDeltaTime) &&
                condition.CanSprint)//총맞으면 멈추기
            {
                currentSpeed *= sprintMultiplier;

                stamina.UseStamina(
                    sprintDrain * Time.fixedDeltaTime);
            }
            else if(Rigid.useGravity)
            {//이러면 벽타기 중에 회복된다는 단점이있음 나중에 Rigid.useGravity == true 일때만 회복하도록 수정하기
                stamina.RecoverStamina( 
                    10f * Time.fixedDeltaTime);
            }

            velocity.x = move.x * currentSpeed;
            velocity.z = move.z * currentSpeed;

            Rigid.velocity = velocity;
        }
        else
        {
            ClimbMove();
        }
    }

    private void HandleClimbing()
    {
        bool canClimb =
            IsWall() &&
            isGrapple &&
            stamina.HasStamina(climbDrain * Time.fixedDeltaTime);

        if (canClimb)
        {
            climbNoiseTimer += Time.fixedDeltaTime;

            if (climbNoiseTimer >= 0.5f)
            {
                noise.MakeNoise(NoiseType.Climb);
                climbNoiseTimer = 0;
            }
        }
        else
        {
            climbNoiseTimer = 0;
        }
    }

    void FixedUpdate()
    {
        //코드 리팩터링 했습니다. 두줄로 정리

        HandleClimbing();
        HandleMovement();
        /*
        Vector3 move;
        if ( Rigid != null) // 흠
        {
            isGrapple = attackAction.IsPressed();
            if (Rigid.useGravity)
            {
                move =
                    transform.forward * InputVec.y +
                    transform.right * InputVec.x;

                Vector3 velocity = Rigid.velocity;




                
                      
                velocity.x = move.x * Speed;
                velocity.z = move.z * Speed;
                                               
                 
                //여기부터
                float currentSpeed = Speed;
                //Debug.Log(currentSpeed); 

                bool isMoving = InputVec.sqrMagnitude > 0.01f;

                if (isSprint &&
                    isMoving &&
                    stamina.HasStamina(sprintDrain * Time.fixedDeltaTime) &&
                    condition.CanSprint )
                {
                    currentSpeed *= sprintMultiplier;
                    stamina.UseStamina(sprintDrain * Time.fixedDeltaTime);
                }
                else if (isMoving)
                {
                    stamina.RecoverStamina(10f * Time.fixedDeltaTime);
                }

                velocity.x = move.x * currentSpeed;
                velocity.z = move.z * currentSpeed;
                //여기까지 변경사항


                float TempSpeed = velocity.magnitude;
                //Anim.SetFloat("Speed", TempSpeed);
                Rigid.velocity = velocity;
            }
            else
            {
                move =
                transform.up * InputVec.y +
                transform.right * InputVec.x;
                //move.Normalize();

                Vector3 velocity = Rigid.velocity;

                velocity.x = move.x * Speed;
                velocity.y = move.y * Speed;   // 추가
                velocity.z = move.z * Speed;

                float TempSpeed = velocity.magnitude;
                //Anim.SetFloat("Speed", TempSpeed);
                Rigid.velocity = velocity;
                //Rigid.AddForce(move * Speed, ForceMode.Acceleration);
            }
        }

        Climbing();
        */
    }

    bool IsWall()
    {
        isWall = Physics.Raycast(
            WallLayCasterTrans.position,
            WallLayCasterTrans.forward,
            0.6f,
            WallLayer);
        return isWall;
    }
    bool IsGround()
    {
        isGround = Physics.Raycast(
            GroundLayCasterTrans.position,
            GroundLayCasterTrans.forward,
            0.6f,
            WallLayer);
        return isGround;
    }

    private void Climbing()
    {
        if (IsWall() &&
            isGrapple &&
            stamina.HasStamina(climbDrain * Time.fixedDeltaTime))
        {
            stamina.UseStamina(climbDrain * Time.fixedDeltaTime);

            Rigid.useGravity = false;
            //여기서 Rigid.velocity = Vector3.zero; 한번 제거해봤어요.
        }
        else
        {
            Rigid.useGravity = true;
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        Debug.Log("콜라이더 발생");
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
}
