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



    //추가한점, 
    public float sprintMultiplier = 1.5f; //달리기속도
    public float sprintDrain = 15f; //달리는데 소모되는 스태미너량
    private bool isSprint; // state 체크
    private PlayerStamina stamina;
    private float timer; //디버깅용

    void Start()
    {
        //추가
        stamina = GetComponent<PlayerStamina>();

        Rigid = GetComponent<Rigidbody>();
        isJump = true;
        attackAction = playerInput.actions["Attack"];
    }

    //추가한점,
    void Update()
    {
        isSprint = Keyboard.current.leftShiftKey.isPressed;

        timer += Time.deltaTime;

        if (timer >= 1f)
        {
            Debug.Log($"현재 스태미나 : {stamina.currentStamina}");
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
                isJump = false;
                //Anim.SetBool("Jump", true);
            }
        }
    }
    void FixedUpdate()
    {
        Vector3 move;
        if (GetComponent<Rigidbody>() != null)
        {
            isGrapple = attackAction.IsPressed();
            if (Rigid.useGravity)
            {
                move =
                    transform.forward * InputVec.y +
                    transform.right * InputVec.x;

                Vector3 velocity = Rigid.velocity;




                /*
                      
                velocity.x = move.x * Speed;
                velocity.z = move.z * Speed;
                                               
                 */
                //여기부터
                float currentSpeed = Speed;
                //Debug.Log(currentSpeed); 

                if (isSprint && stamina.HasStamina(0.1f))
                {
                    currentSpeed *= sprintMultiplier;
                    stamina.UseStamina(sprintDrain * Time.fixedDeltaTime); //sprint 일경우, stamina 가 있으면 쓰고 없으면 꾸준히 회복.
                }
                else
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
    stamina.UseStamina(20f * Time.fixedDeltaTime) &&
    Rigid.useGravity) //변경사항, 사용할 스태미너가 있는지 고려하기.

        {
            Rigid.useGravity = false;
            Rigid.velocity = Vector3.zero;
        }
        else if (IsWall() || !isGrapple) //흠..
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
