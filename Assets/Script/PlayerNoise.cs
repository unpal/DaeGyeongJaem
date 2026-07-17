using Fusion;
using System.Collections;
using UnityEngine;

public class PlayerNoise : NetworkBehaviour
{
    [Header("Periodic Noise")]
    [SerializeField] private float periodicInterval = 30f;
    [SerializeField] private float periodicRadius = 20f;

    [Header("Whistle")]
    [SerializeField] private float whistleRadius = 25f;

    [Header("Footstep")]
    [SerializeField] private float walkRadius = 2f;
    [SerializeField] private float runRadius = 6f;

    [Header("Action")]
    [SerializeField] private float jumpRadius = 5f;
    [SerializeField] private float landRadius = 8f;
    [SerializeField] private float climbRadius = 4f;

    [Header("Stealth")]
    [SerializeField] private float crouchMultiplier = 0.3f;

    private bool isCrouching;

    public static System.Action<Vector3, float, NoiseType> OnNoiseGenerated; //나중에 ai 테스트 하고 지우기


    public void SetCrouching(bool value)
    {
        isCrouching = value;
    }

    public override void Spawned()
    {
        StartCoroutine(PeriodicNoiseRoutine());
    }


    public void MakeNoise(NoiseType type)
    {
        if (!Runner.IsForward)
            return;

        float radius = GetNoiseRadius(type);

        if (isCrouching)
        {
            switch (type)
            {
                case NoiseType.Walk:
                case NoiseType.Climb:
                    radius = 0f;
                    break;

                default:
                    radius *= crouchMultiplier;
                    break;
            }
        }

        if (radius <= 0)
            return;

        Debug.Log($"[{type}] Noise ({radius}) 발생");

        //사운드가 여러번 호출되는 문제가 있어 Host만 이벤트를 처리하도록 설정
        if (Object.HasStateAuthority)
        {
            // AI 연동 예정
            // HunterAI.Instance.HearNoise(transform.position, radius, type);
            OnNoiseGenerated?.Invoke(transform.position, radius, type); //호출 인터페이스 ai 시험용
        }
    }
    /*
    private void OnEnable()
{
    PlayerNoise.OnNoiseGenerated += HearNoise;
}

private void OnDisable()
{
    PlayerNoise.OnNoiseGenerated -= HearNoise;
} 

    이렇게 쓰기

     */



    private float GetNoiseRadius(NoiseType type)
    {
        switch (type)
        {
            case NoiseType.Walk:
                return walkRadius;

            case NoiseType.Run:
                return runRadius;

            case NoiseType.Jump:
                return jumpRadius;

            case NoiseType.Land:
                return landRadius;

            case NoiseType.Climb:
                return climbRadius;

            case NoiseType.Whistle:
                return whistleRadius;

            case NoiseType.Periodic:
                return periodicRadius;
        }

        return 0f;
    }

    IEnumerator PeriodicNoiseRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(periodicInterval);

            SoundEventManager.TriggerSound(transform.position, 20.0f);
            MakeNoise(NoiseType.Periodic);
        }
    }

    public void Whistle()
    {
        if (Object.HasStateAuthority)
        {
            SoundEventManager.TriggerSound(transform.position, 20.0f);
        }
        MakeNoise(NoiseType.Whistle);
    }

}