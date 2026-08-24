using UnityEngine;

public class PlayerFootstep
{
    private readonly PlayerAnimation animation;
    private readonly float soundRange;

    private bool firstStep;
    private bool secondStep;

    public PlayerFootstep(
        PlayerAnimation animation,
        float soundRange)
    {
        this.animation = animation;
        this.soundRange = soundRange;
    }

    public bool Update(
        Vector3 position,
        bool climbing,
        bool sprinting)
    {
        AnimatorStateInfo state =
            animation.GetState();

        if (climbing ||
            state.IsName("Climb"))
        {
            Reset();
            return false;
        }

        if (!state.IsName("Run"))
            return false;

        float time =
            state.normalizedTime % 1f;

        if (time >= 0.15f &&
            time < 0.65f &&
            !firstStep &&
            sprinting)
        {
            firstStep = true;

            return true;
        }

        if (time >= 0.65f &&
            !secondStep &&
            sprinting)
        {
            secondStep = true;

            return true;
        }

        if (time < 0.15f)
            Reset();

        return false;
    }

    public float SoundRange =>
        soundRange;

    public void Reset()
    {
        firstStep = false;
        secondStep = false;
    }
}