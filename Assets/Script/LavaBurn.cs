using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class LavaBurn : MonoBehaviour
{
    //용암 오브젝트에 붙이면 됩니다 이파일.
    public float tickInterval = 0.5f;
    public float burnDamage = 10f;
    [SerializeField] private float activationDelay = 10f; //추가> 게임시작후 10초 후에 화상

    private readonly Dictionary<PlayerCondition, float> nextDamageTime = new();
    private GameManager gameManager;
    private PrototypeRoundManager prototypeRoundManager;
    private RoundPhase lastGamePhase;
    private PrototypeRoundPhase lastPrototypePhase;
    private float damageActivationTime;
    private bool phaseInitialized;

    private void Start()
    {
        FindRoundManager();
        RestartActivationDelay();
    }

    private void Update()
    {
        if (gameManager == null && prototypeRoundManager == null)
            FindRoundManager();

        if (gameManager != null)
        {
            RoundPhase phase = gameManager.Phase;
            if (!phaseInitialized ||
                (phase == RoundPhase.Starting && lastGamePhase != RoundPhase.Starting))
                RestartActivationDelay();

            lastGamePhase = phase;
            phaseInitialized = true;
            return;
        }

        if (prototypeRoundManager != null)
        {
            PrototypeRoundPhase phase = prototypeRoundManager.Phase;
            if (!phaseInitialized ||
                (phase == PrototypeRoundPhase.Starting &&
                 lastPrototypePhase != PrototypeRoundPhase.Starting))
                RestartActivationDelay();

            lastPrototypePhase = phase;
            phaseInitialized = true;
        }
    }

    private void OnTriggerStay(Collider other) //계속 머무르면 데미지 갱신
    {
        if (!CanApplyDamage()) // 대미지 줄수있는지 먼저 확인하기
            return;

        PlayerCondition condition = other.GetComponentInParent<PlayerCondition>();
        PlayerGameState state = other.GetComponentInParent<PlayerGameState>();

        if (condition == null || state == null || state.Object == null ||
            !state.Object.HasStateAuthority || !state.IsInPlayground)
            return;

        if (nextDamageTime.TryGetValue(condition, out float nextTime) &&
            Time.time < nextTime)
            return;

        nextDamageTime[condition] = Time.time + tickInterval;
        condition.ApplyTemporaryDamage(burnDamage);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerCondition condition = other.GetComponentInParent<PlayerCondition>();
        if (condition != null)
            nextDamageTime.Remove(condition);
    }

    private void FindRoundManager()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager == null)
            prototypeRoundManager = FindFirstObjectByType<PrototypeRoundManager>();
    }

    private void RestartActivationDelay() // 라운드가 starting으로 바뀔 때 마다 활성화 시간 초기화.
    {
        damageActivationTime = Time.time + activationDelay;
        nextDamageTime.Clear();
    }

    private bool CanApplyDamage() // 피해 조건 검사를 걸어버리면서 타이머 시간이 지나야 데미지를 줄수있도록.
    {
        if (Time.time < damageActivationTime)
            return false;

        if (gameManager != null)
            return gameManager.Phase == RoundPhase.Playing;

        return prototypeRoundManager == null ||
               prototypeRoundManager.Phase == PrototypeRoundPhase.Playing;
    }
}
