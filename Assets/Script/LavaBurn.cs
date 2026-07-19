using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class LavaBurn : MonoBehaviour
{
    //용암 오브젝트에 붙이면 됩니다 이파일.
    public float tickInterval = 0.5f;
    public float burnDamage = 10f;
    [SerializeField] private float activationDelay = 10f; //추가> 게임시작후 10초 후에 화상
    //추가
    [SerializeField] private float riseHeight = 1.8f; //용암오브젝트 땅바닥에 숨겨놨다가 올릴 높이
    [SerializeField] private float riseDuration = 1f; //몇초동안 올릴지 (부드럽게)

    private readonly Dictionary<PlayerCondition, float> nextDamageTime = new();
    private GameManager gameManager;
    private PrototypeRoundManager prototypeRoundManager;
    private RoundPhase lastGamePhase;
    private PrototypeRoundPhase lastPrototypePhase;
    private float damageActivationTime;
    private bool phaseInitialized;

    //추가, 숨겨진위치, 올라올위치, 시작될 시간, 조건문용 부울
    private Vector3 hiddenLocalPosition;
    private Vector3 activeLocalPosition;
    private float riseStartedTime;
    private bool isRising;
    private bool isActive;

    private void Start()
    {
        hiddenLocalPosition = transform.localPosition;
        activeLocalPosition = hiddenLocalPosition + Vector3.up * riseHeight;
        FindRoundManager();
        RestartActivationDelay();
    }

    private void Update()
    {
        if (gameManager == null && prototypeRoundManager == null)
            FindRoundManager();

        if (gameManager != null &&
            gameManager.Object != null &&
            gameManager.Id.IsValid)
        {
            RoundPhase phase = gameManager.Phase;
            if (!phaseInitialized ||
                (phase == RoundPhase.Starting && lastGamePhase != RoundPhase.Starting))
                RestartActivationDelay();

            lastGamePhase = phase;
            phaseInitialized = true;
        }
        else if (prototypeRoundManager != null &&
                 prototypeRoundManager.Object != null &&
                 prototypeRoundManager.Id.IsValid)
        {
            PrototypeRoundPhase phase = prototypeRoundManager.Phase;
            if (!phaseInitialized ||
                (phase == PrototypeRoundPhase.Starting &&
                 lastPrototypePhase != PrototypeRoundPhase.Starting))
                RestartActivationDelay();

            lastPrototypePhase = phase;
            phaseInitialized = true;
        }
        //추가
        UpdateLavaSurface();
    }

    private void OnTriggerStay(Collider other) //계속 머무르면 데미지 갱신
    {
        if (!isActive || !CanApplyDamage()) // 용암 상승 완료와 대미지 조건 확인
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
        //추가
        isRising = false;
        isActive = false;
        transform.localPosition = hiddenLocalPosition;
    }

    //추가, 용암올리기
    private void UpdateLavaSurface()
    {
        if (isActive)
            return;

        if (!isRising)
        {
            if (!CanApplyDamage())
                return;

            isRising = true;
            riseStartedTime = Time.time;
        }

        float duration = Mathf.Max(0.01f, riseDuration);
        float progress = Mathf.Clamp01((Time.time - riseStartedTime) / duration); //상한선이 흠.. 1초말고 더 시간 길게 해도됨. 비율 이용.

        progress = Mathf.SmoothStep(0f, 1f, progress); //부드럽게 진행.
        transform.localPosition = Vector3.Lerp(
            hiddenLocalPosition,
            activeLocalPosition,
            progress);

        if (progress >= 1f)
            isActive = true;
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
