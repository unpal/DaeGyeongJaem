using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler
{
    private readonly PlayerInput playerInput;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction attackAction;
    private InputAction sprintAction;
    private InputAction whistleAction;

    public Vector2 Move { get; private set; }
    public Vector2 Look { get; private set; }

    public PlayerInputHandler(PlayerInput playerInput)
    {
        this.playerInput = playerInput;

        if (playerInput == null)
        {
            Debug.LogError("PlayerInputHandler: PlayerInput이 없습니다.");
            return;
        }

        moveAction = playerInput.actions["Move"];
        lookAction = playerInput.actions["Look"];
        jumpAction = playerInput.actions["Jump"];
        attackAction = playerInput.actions["Attack"];
        sprintAction = playerInput.actions["Sprint"];
        whistleAction = playerInput.actions["Whistle"];
    }

    public void SetEnabled(bool enabled)
    {
        if (playerInput != null)
            playerInput.enabled = enabled;
    }

    public void Update()
    {
        if (playerInput == null || !playerInput.enabled)
            return;

        if (moveAction != null)
            Move = moveAction.ReadValue<Vector2>();

        if (lookAction != null)
            Look = lookAction.ReadValue<Vector2>();
    }

    public bool JumpPressed =>
        jumpAction != null &&
        jumpAction.IsPressed();

    public bool AttackPressed =>
        attackAction != null &&
        attackAction.IsPressed();

    public bool SprintPressed =>
        sprintAction != null &&
        sprintAction.IsPressed();

    public bool WhistlePressed =>
        whistleAction != null &&
        whistleAction.WasPressedThisFrame();

    public NetworkInputData GetNetworkInput()
    {
        NetworkInputData data = new NetworkInputData();

        data.Move = Move;
        data.Look = Look;

        data.Buttons.Set(
            (int)PlayerButtons.Jump,
            JumpPressed);

        data.Buttons.Set(
            (int)PlayerButtons.Attack,
            AttackPressed);

        data.Buttons.Set(
            (int)PlayerButtons.Sprint,
            SprintPressed);

        return data;
    }

    public void Reset()
    {
        Move = Vector2.zero;
        Look = Vector2.zero;
    }
}