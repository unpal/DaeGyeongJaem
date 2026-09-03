using Fusion;
using UnityEngine;

public class PlayerClimbing
{
    private readonly NetworkCharacterController controller;
    private readonly PlayerCondition condition;
    private readonly PlayerGameState gameState;
    private readonly FallDamage fallDamage;

    private readonly LayerMask wallLayer;

    private readonly Transform wallCaster;

    private readonly float climbDrain;

    private Vector3 wallNormal;
    private float wallDistance;

    private bool IsForce;
    private float ForceTime;
    private float CurrentForceTime;
    public PlayerClimbing(
        NetworkCharacterController controller,
        PlayerCondition condition,
        PlayerGameState gameState,
        FallDamage fallDamage,
        Transform wallCaster,
        LayerMask wallLayer,
        float climbDrain,
        float ForceTime)
    {
        this.controller = controller;
        this.condition = condition;
        this.gameState = gameState;
        this.fallDamage = fallDamage;
        this.wallCaster = wallCaster;
        this.wallLayer = wallLayer;
        this.climbDrain = climbDrain;
        this.ForceTime = ForceTime;
    }

    public bool IsClimbing =>
        controller != null &&
        controller.IsClimbing;

    public bool UpdateState(
        NetworkInputData data)
    {
        bool attackPressed =
            data.Buttons.IsSet(
                (int)PlayerButtons.Attack);

        bool canClimb =
            IsWall() &&
            attackPressed &&
            !controller.IsDash &&
            gameState != null &&
            gameState.IsInPlayground;

        if (canClimb &&
            !controller.IsClimbing)
        {
            StartClimbing();
        }
        else if (!canClimb &&
                 controller.IsClimbing)
        {
            Debug.Log("클라이밍 종료 사유 \n"+
                $"IsWall(): {IsWall()}\n" +
                $"attackPressed: {attackPressed}\n" +
                $"controller.IsDash: {!controller.IsDash}\n" +
                $"gameState: {gameState != null}" + 
                $"gameState.IsInPlayground: {gameState.IsInPlayground}\n"
            );


            StopClimbing(attackPressed);
        }

        return controller.IsClimbing;
    }

    public void UpdateForce(float deltaTime)
    {
        if (IsForce)
        {
            CurrentForceTime -= deltaTime;
            //Debug.Log(CurrentForceTime);
            if (CurrentForceTime < 0)
            {
                CurrentForceTime = 0;
                IsForce = false;
            }
        }
    }

    private void StartClimbing()
    {
        controller.Velocity =
            Vector3.zero;

        controller.IsClimbing = true;

        if (fallDamage != null)
        {
            fallDamage
                .ResetFallTrackingFromCurrentHeight();
        }
    }

    private void StopClimbing(
        bool attackPressed)
    {
        controller.IsClimbing = false;

        if (attackPressed)
            controller.IsDash = true;
    }

    public Vector3 Move(
        NetworkInputData data,
        float deltaTime)
    {
        Vector3 wallUp =
            Vector3.up;

        Vector3 wallRight =
            Vector3.Cross(
                wallUp,
                wallNormal)
            .normalized;

        Vector3 move =
            wallUp * data.Move.y +
            wallRight * -data.Move.x;

        float stamina =
            climbDrain * deltaTime;

        if (condition != null &&
            condition.CanUseStamina(stamina))
        {
            condition.TryUseStamina(stamina);
            float staminarat = condition.StaminaRat();
           // Debug.Log(staminarat);
            controller.Move(
                move * deltaTime * (staminarat < 0.3f ? staminarat : 1),IsForce);
        }
        else
        {
            move.y -= 2f;

            controller.Move(
                move * deltaTime);
        }

        return move;
    }

    private bool IsWall()
    {
        if (wallCaster == null)
            return false;

        RaycastHit hit;

        Vector3 origin =
            wallCaster.position;

        Vector3 forward =
            wallCaster.forward;

        Vector3 right =
            wallCaster.right;

        Vector3 up =
            wallCaster.up;

        float horizontalOffset = 0.45f;
        float verticalOffset = 1.2f;

        wallDistance = float.MaxValue;

        bool found = false;

        for (int y = 1; y >= -1; y--)
        {
            for (int x = -1; x <= 1; x++)
            {
                Vector3 start =
                    origin +
                    right * (x * horizontalOffset) +
                    up * (y * verticalOffset);

                if (Physics.Raycast(
                    start,
                    forward,
                    out hit,
                    1.5f,
                    wallLayer))
                {
                    if (hit.distance < wallDistance)
                    {
                        wallDistance =
                            hit.distance;

                        wallNormal =
                            hit.normal;
                    }

                    found = true;
                }
            }
        }

        return found;
    }

    public void Reset()
    {
        controller.IsClimbing = false;
    }
   
    public void SetIsForce(bool Force)
    {
        IsForce = Force;
        CurrentForceTime = ForceTime;
    }
}