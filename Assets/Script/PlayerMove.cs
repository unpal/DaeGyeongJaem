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

    void Start()
    {
        Rigid = GetComponent<Rigidbody>();
        isJump = true;
        attackAction = playerInput.actions["Attack"];
    }

    private void OnMove(InputValue value)
    {
        InputVec = value.Get<Vector2>();
    }

    void OnJump(InputValue value)
    {
        if (value.isPressed)
        {
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

                velocity.x = move.x * Speed;
                velocity.z = move.z * Speed;
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
        if (IsWall() && isGrapple && Rigid.useGravity)
        {
            Rigid.useGravity = false;
            Rigid.velocity = Vector3.zero;
        }
        else if (IsWall() || !isGrapple)
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
