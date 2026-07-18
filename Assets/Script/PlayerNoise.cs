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
    private AudioSource _audioSource;
    
    private PlayerGameState _gameState;

    public static System.Action<Vector3, float, NoiseType> OnNoiseGenerated; //나중에 ai 테스트 하고 지우기

    private void Awake()
    {
        if (TryGetComponent(out _audioSource))
        {
            //로비에 입장하자마자 휘파람 소리 들리는거 false
            _audioSource.playOnAwake = false;
            _audioSource.Stop();
        }
    }


    public void SetCrouching(bool value)
    {
        isCrouching = value;
    }

    public override void Spawned()
    {
        TryGetComponent(out _audioSource);
        //죽었을때 들리면 안되니까? << 이거 다른코드에 붙어있어서 중복되는지 확인하고 지울게요
        TryGetComponent(out _gameState);

        //이거 조건문 붙히고 내렸어용, 본인소리는 (periodic whistle은) 본인만 들리구로
        if (Object.HasStateAuthority)
            StartCoroutine(PeriodicNoiseRoutine());
    }


    public void MakeNoise(NoiseType type)
    {
        // Update, Coroutine, RPC 등 FixedUpdateNetwork 외부에서 호출될 경우 
        // Runner.IsForward가 false이기 때문에 여기서 막히게 됩니다. 
        // 따라서 IsForward 체크를 제거합니다.
        // if (!Runner.IsForward)
        //     return;

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

    //추가
    private IEnumerator PeriodicNoiseRoutine()
    {
        while (true)
        {
                

            yield return new WaitForSeconds(periodicInterval);
            //아래에 있던거 양식 살짝 빌려와서 작성. 수정, 추가.
            if (_gameState == null && !TryGetComponent(out _gameState))
                continue;
            //조건문 복잡하길래 분리해서 가시적으로.
            if (!_gameState.IsInPlayground)
                continue;

            Rpc_PlayPeriodicWhistle();
            SoundEventManager.TriggerSound(transform.position, 20.0f);
            MakeNoise(NoiseType.Periodic);

        }
    }

    public void Whistle()
    {
        if (Object.HasInputAuthority)
        {
            Rpc_Whistle();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    public void Rpc_Whistle()
    {
        if (_gameState == null) TryGetComponent(out _gameState);
        // 2. 플레이어가 죽었거나 탈출했다면 소리를 내지 않고 무시합니다. << 오호
        if (_gameState != null && !_gameState.IsInPlayground)
        {
            return;
        }
        
        PlayWhistleAudio();
        SoundEventManager.TriggerSound(transform.position, 20.0f);
        MakeNoise(NoiseType.Whistle);
    }

    //교체,추가 RpcSources << 원격 함수 실행
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)] //호스트만 사용가능, 모든 플레이어를 대상으로,
    private void Rpc_PlayPeriodicWhistle()
    {
        PlayWhistleAudio();
    }
    //교체,추가, 위에서 씀.
    private void PlayWhistleAudio()
    {
        if (_audioSource != null)
        {
            _audioSource.Play();
        }
        else
        {
            Debug.LogWarning("_audioSource 컴포넌트가 없음");
        }
    }

}
