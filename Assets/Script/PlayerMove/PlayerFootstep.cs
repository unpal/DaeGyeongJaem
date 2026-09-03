using UnityEngine;

public class PlayerFootstep
{
    private readonly PlayerAnimation animation;
    private readonly float soundRange;

    private bool firstStep;
    private bool secondStep;
    private bool FirstForce;
    private bool SecondForce;

    public PlayerFootstep(
        PlayerAnimation animation,
        float soundRange)
    {
        this.animation = animation;
        this.soundRange = soundRange;
    }

    public int Update(
        Vector3 position,
        bool climbing,
        bool sprinting)
    {
        AnimatorStateInfo state =
            animation.GetState();

        float time =
        state.normalizedTime % 1f;
        if (climbing ||
            state.IsName("Climb"))
        {
            ResetStep();
            if(time >= 0.4f && time < 0.8f && !FirstForce)
            {
                FirstForce = true;
                return 2;
            }
            if (time >= 0.8f && !SecondForce)
            {
                SecondForce = true;
                return 2;
            }
            if(time < 0.4f)
            {
                ResetForce();
            }
        }

        if (!state.IsName("Run"))
            return 0;

        ResetForce();

        if (time >= 0.15f &&
            time < 0.65f &&
            !firstStep &&
            sprinting)
        {
            firstStep = true;

            return 1;
        }

        if (time >= 0.65f &&
            !secondStep &&
            sprinting)
        {
            secondStep = true;

            return 1;
        }

        if (time < 0.15f)
            ResetStep();

        return 0;
    }

    public float SoundRange =>
        soundRange;

    public void ResetStep()
    {
        firstStep = false;
        secondStep = false;
    }
    public void ResetForce()
    {
        FirstForce = false;
        SecondForce = false;
    }
}