using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMove : NetworkBehaviour
{
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Transform player;

    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private Transform headBone;
    private InputAction lookAction;
    private float xRotation = 0f;
    private Vector2 lookInput;

    private void Awake()
    {
        lookAction = playerInput.actions["Look"];
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    //private void Update()
    //{
    //    if (!Object.HasInputAuthority)
    //        return;

    //    float mouseY = lookInput.y * sensitivity;
    //    xRotation -= mouseY;
    //    xRotation = Mathf.Clamp(xRotation, -90f, 90f);

    //    transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

    //}
    // private void LateUpdate()
    // {
    //     Vector3 tempVec = headBone.position;
    //     transform.position = tempVec;
    // }
}
