using UnityEngine;

public class PlayerAnimation
{
    private readonly Animator animator;

    public PlayerAnimation(
        Animator animator)
    {
        this.animator = animator;
    }

    public void Update(
        Vector3 move,
        bool climbing,
        bool sprinting)
    {
        if (animator == null)
            return;

        bool running =
            move.sqrMagnitude > 0.01f &&
            !climbing;

        animator.SetBool(
            "Climbing",
            climbing);

        animator.SetBool(
            "Running",
            running);

        animator.speed =
            sprinting ? 1.5f : 1f;
    }

    public AnimatorStateInfo GetState()
    {
        if (animator == null)
            return default;

        return animator.GetCurrentAnimatorStateInfo(0);
    }

    public void Reset()
    {
        if (animator == null)
            return;

        animator.SetBool(
            "Climbing",
            false);

        animator.SetBool(
            "Running",
            false);

        animator.speed = 1f;
    }
}