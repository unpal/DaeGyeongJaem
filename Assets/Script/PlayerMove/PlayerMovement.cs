using Fusion;
using UnityEngine;

public class PlayerMovement
{
    private readonly NetworkCharacterController controller;
    private readonly PlayerCondition condition;

    private readonly float walkSpeed;
    private readonly float runSpeed;
    private readonly float sprintDrain;

    public PlayerMovement(
        NetworkCharacterController controller,
        PlayerCondition condition,
        float walkSpeed,
        float runSpeed,
        float sprintDrain)
    {
        this.controller = controller;
        this.condition = condition;

        this.walkSpeed = walkSpeed;
        this.runSpeed = runSpeed;
        this.sprintDrain = sprintDrain;
    }

    public Vector3 Move(
        NetworkInputData data,
        float deltaTime,
        bool isClimbing)
    {
        if (isClimbing)
            return Vector3.zero;

        bool hasMovementInput =
            data.Move.sqrMagnitude > 0.01f;

        bool sprintPressed =
            data.Buttons.IsSet(
                (int)PlayerButtons.Sprint);

        bool canSprint =
            sprintPressed &&
            hasMovementInput &&
            condition != null &&
            condition.CanSprint &&
            condition.CanUseStamina(
                sprintDrain * deltaTime);

        controller.maxSpeed =
            canSprint
                ? runSpeed
                : walkSpeed;

        if (canSprint)
        {
            condition.TryUseStamina(
                sprintDrain * deltaTime);
        }

        Vector3 move =
            controller.transform.forward *
            data.Move.y +

            controller.transform.right *
            data.Move.x;

        controller.Move(
            move * deltaTime);

        return move;
    }

    public bool CanSprint(
        NetworkInputData data)
    {
        return
            data.Buttons.IsSet(
                (int)PlayerButtons.Sprint) &&
            data.Move.sqrMagnitude > 0.01f;
    }
}