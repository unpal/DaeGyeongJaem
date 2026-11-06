using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerGameState))]
public class PlayerCondition : NetworkBehaviour
{
    [Header("Base")]
    [SerializeField, Min(0f)] private float baseMaxStamina = 100f;

    [Header("Burn")]
    [SerializeField, Min(0f)] private float burnDuration = 3f;
    [SerializeField, Min(0.01f)] private float burnTick = 0.5f;
    [SerializeField, Min(0f)] private float burnDamage = 2f;

    [Header("Recovery")]
    [SerializeField, Min(0f)] private float recoverDelay = 5f;
    [SerializeField, Min(0f)] private float recoverRate = 1f;

    [Header("Eyes")]
    [SerializeField, Min(0f)] private float eyeStaminaDrain = 15f;

    [Header("Revive")]
    [SerializeField, Min(0.1f)] private float reviveDuration = 10f;
    [SerializeField, Min(0.1f)] private float reviveDistance = 2.5f;
    [SerializeField, Range(0.01f, 1f)] private float reviveStaminaRatio = 0.3f;

    [Networked] public NetworkBool EyesOpen { get; private set; }
    [Networked] public int ReviveItemCount { get; private set; }
    [Networked] public NetworkObject ReviveTarget { get; private set; }
    [Networked] public float ReviveElapsed { get; private set; }
    [Networked] private NetworkBool ReviveNeedsRelease { get; set; }
    [Networked] private NetworkBool EyesNeedRelease { get; set; }

    [Networked] public float CurrentStamina { get; private set; }
    [Networked] public float TemporaryDamage { get; private set; }
    [Networked] public float PermanentDamage { get; private set; }
    [Networked] private float BurnRemaining { get; set; }
    [Networked] private float BurnTickAccumulator { get; set; }
    [Networked] private float RecoveryDelayRemaining { get; set; }
    [Networked] private float SprintLockRemaining { get; set; }

    private PlayerGameState playerGameState;
    private GameManager gameManager;
    private PrototypeRoundManager prototypeRoundManager;
    private Un movement;

    public float BaseMaxStamina => Mathf.Max(0f, baseMaxStamina);
    public float CurrentMaxStamina => Mathf.Clamp(
        BaseMaxStamina - (IsDamageImmune ? 0f : PermanentDamage + TemporaryDamage),
        0f,
        BaseMaxStamina);
    public bool CanSprint => SprintLockRemaining <= 0f;
    private bool IsDamageImmune => playerGameState != null && playerGameState.IsSeeker;
    public float ReviveProgress => Mathf.Clamp01(ReviveElapsed / reviveDuration);

    // 기존 Shotgun 코드와 Inspector 데이터 호환을 위해 유지한다.
    public bool isShotGunHit;

    private void Awake()
    {
        playerGameState = GetComponent<PlayerGameState>();
        movement = GetComponent<Un>();
    }

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        ResetVitals();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        float deltaTime = Runner.DeltaTime;
        CheckDowned();
        UpdateBurn(deltaTime);
        if (!playerGameState.IsDowned)
            UpdateRecovery(deltaTime);
        SprintLockRemaining = Mathf.Max(0f, SprintLockRemaining - deltaTime);

        bool hasInput = GetInput(out NetworkInputData input);
        UpdateEyes(hasInput && input.Buttons.IsSet(PlayerButtons.OpenEyes), deltaTime);
        UpdateRevive(input, hasInput, deltaTime);

        if (isShotGunHit)
            isShotGunHit = false;
    }

    public bool CanUseStamina(float amount)
    {
        // 남은 스태미나가 비용보다 적어도 마지막 행동은 허용한다.
        return IsPositiveFinite(amount) && CurrentStamina > 0f &&
            playerGameState.IsInPlayground && !playerGameState.IsDowned;
    }

    public float StaminaRat()
    {
        //남은 스테미나 비율 반환
        return CurrentMaxStamina > 0f ? CurrentStamina / CurrentMaxStamina : 0f;
    }

    public bool TryUseStamina(float amount)
    {
        if (!Object.HasStateAuthority || !CanUseStamina(amount))
            return false;

        CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
        return true;
    }

    public void RecoverStamina(float amount)
    {
        if (!Object.HasStateAuthority || playerGameState.IsDowned || !IsPositiveFinite(amount))
            return;

        CurrentStamina = Mathf.Min(CurrentStamina + amount, CurrentMaxStamina);
    }

    public void ResetStamina()
    {
        if (!Object.HasStateAuthority)
            return;

        CurrentStamina = CurrentMaxStamina;
    }

    public void ResetForNextRound()
    {
        if (!Object.HasStateAuthority)
            return;

        ResetVitals();
    }

    public void ApplyPermanentDamage(float amount)
    {
        if (!Object.HasStateAuthority || IsDamageImmune || playerGameState.IsDowned ||
            !playerGameState.IsInPlayground || !IsPositiveFinite(amount))
            return;

        CancelRevive();
        PermanentDamage = Mathf.Clamp(
            PermanentDamage + amount,
            0f,
            BaseMaxStamina);
        TemporaryDamage = Mathf.Clamp(
            TemporaryDamage,
            0f,
            BaseMaxStamina - PermanentDamage);
        ClampCurrentStamina();
        CheckDowned();
    }

    public void ApplyTemporaryDamage(float amount)
    {
        if (!Object.HasStateAuthority || IsDamageImmune || playerGameState.IsDowned ||
            !playerGameState.IsInPlayground || !IsPositiveFinite(amount))
            return;

        CancelRevive();
        TemporaryDamage = Mathf.Clamp(
            TemporaryDamage + amount,
            0f,
            BaseMaxStamina - PermanentDamage);
        RecoveryDelayRemaining = recoverDelay;
        ClampCurrentStamina();
        CheckDowned();
    }

    public void RecoverTemporaryDamage(float amount)
    {
        if (!Object.HasStateAuthority || playerGameState.IsDowned || !IsPositiveFinite(amount))
            return;

        TemporaryDamage = Mathf.Max(0f, TemporaryDamage - amount);
    }

    public void RefreshBurn()
    {
        if (!Object.HasStateAuthority || IsDamageImmune || playerGameState.IsDowned)
            return;

        BurnRemaining = Mathf.Max(BurnRemaining, burnDuration);
        RecoveryDelayRemaining = recoverDelay;
    }

    public void LockSprint(float seconds)
    {
        if (!Object.HasStateAuthority || !IsPositiveFinite(seconds))
            return;

        SprintLockRemaining = Mathf.Max(SprintLockRemaining, seconds);
    }

    private void UpdateBurn(float deltaTime)
    {
        if (BurnRemaining <= 0f)
            return;

        BurnRemaining = Mathf.Max(0f, BurnRemaining - deltaTime);
        BurnTickAccumulator += deltaTime;
        RecoveryDelayRemaining = recoverDelay;

        float interval = Mathf.Max(0.01f, burnTick);
        while (BurnTickAccumulator >= interval)
        {
            BurnTickAccumulator -= interval;
            ApplyTemporaryDamage(burnDamage);
        }
    }

    private void UpdateRecovery(float deltaTime)
    {
        if (BurnRemaining > 0f || TemporaryDamage <= 0f)
            return;

        if (RecoveryDelayRemaining > 0f)
        {
            RecoveryDelayRemaining = Mathf.Max(0f, RecoveryDelayRemaining - deltaTime);
            return;
        }

        RecoverTemporaryDamage(recoverRate * deltaTime);
    }

    private bool IsRoundPlaying()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
            return gameManager.Object != null && gameManager.Object.IsValid &&
                gameManager.Phase == RoundPhase.Playing;

        if (prototypeRoundManager == null)
            prototypeRoundManager = FindFirstObjectByType<PrototypeRoundManager>();
        return prototypeRoundManager != null && prototypeRoundManager.Object != null &&
            prototypeRoundManager.Object.IsValid && prototypeRoundManager.RoundNumber > 0 &&
            prototypeRoundManager.Phase == PrototypeRoundPhase.Playing;
    }

    private void CheckDowned()
    {
        if (IsDamageImmune || CurrentMaxStamina > 0f || playerGameState.IsDowned ||
            !playerGameState.IsInPlayground || !IsRoundPlaying())
            return;

        CurrentStamina = 0f;
        CancelRevive();
        if (gameManager != null)
            gameManager.ReportPlayerDowned(playerGameState);
        else
            prototypeRoundManager.ReportPlayerDowned(playerGameState);
    }

    private void UpdateEyes(bool held, float deltaTime)
    {
        EyesOpen = false;
        if (!held)
        {
            EyesNeedRelease = false;
            return;
        }

        if (!playerGameState.IsSeeker || !playerGameState.CanMove || EyesNeedRelease)
            return;

        if (eyeStaminaDrain > 0f)
            TryUseStamina(eyeStaminaDrain * deltaTime);

        EyesOpen = CurrentStamina > 0f;
        if (!EyesOpen)
            EyesNeedRelease = true;
    }

    public bool AddReviveItem()
    {
        if (!Object.HasStateAuthority || playerGameState.IsSeeker ||
            !playerGameState.IsInPlayground || playerGameState.IsDowned)
            return false;

        ReviveItemCount++;
        return true;
    }

    public bool RemoveReviveItem()
    {
        if (!Object.HasStateAuthority || ReviveItemCount <= 0)
            return false;

        ReviveItemCount--;
        CancelRevive();
        return true;
    }

    // 다른 아이템 사용 코드에서도 사용 직전에 호출할 수 있다.
    public void CancelRevive()
    {
        if (!Object.HasStateAuthority)
            return;

        ReviveTarget = null;
        ReviveElapsed = 0f;
        ReviveNeedsRelease = true;
    }

    private bool CanReviveTarget(PlayerCondition target)
    {
        return target != null && target != this && target.Object != null && target.Object.IsValid &&
            target.Runner == Runner && target.playerGameState.IsDowned &&
            target.playerGameState.IsInPlayground && !target.playerGameState.IsSeeker &&
            target.BaseMaxStamina > 0f &&
            (target.transform.position - transform.position).sqrMagnitude <= reviveDistance * reviveDistance;
    }

    private void UpdateRevive(NetworkInputData input, bool hasInput, float deltaTime)
    {
        if (!hasInput)
        {
            CancelRevive();
            return;
        }

        if (!input.Buttons.IsSet(PlayerButtons.Revive))
        {
            CancelRevive();
            ReviveNeedsRelease = false;
            return;
        }

        bool otherAction = input.Move.sqrMagnitude > 0f ||
            input.Buttons.IsSet(PlayerButtons.Jump) || input.Buttons.IsSet(PlayerButtons.Attack) ||
            input.Buttons.IsSet(PlayerButtons.Fire) || input.Buttons.IsSet(PlayerButtons.UseItem) ||
            (movement != null && movement.statemachine != Un.StateMachine.Floor);

        if (ReviveNeedsRelease || otherAction || !playerGameState.CanMove ||
            playerGameState.IsSeeker || ReviveItemCount <= 0 || !IsRoundPlaying())
        {
            CancelRevive();
            return;
        }

        // 시작할 때만 가까운 동료를 찾는다. 진행 중에는 대상이 바뀌지 않는다.
        if (ReviveTarget == null && ReviveElapsed == 0f)
        {
            float closestDistance = float.MaxValue;
            foreach (PlayerCondition candidate in FindObjectsByType<PlayerCondition>(FindObjectsSortMode.None))
            {
                if (!CanReviveTarget(candidate)) continue;
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                ReviveTarget = candidate.Object;
            }
        }

        PlayerCondition target = ReviveTarget != null && ReviveTarget.IsValid
            ? ReviveTarget.GetComponent<PlayerCondition>() : null;
        if (!CanReviveTarget(target))
        {
            CancelRevive();
            return;
        }

        ReviveElapsed += deltaTime;
        if (ReviveElapsed < reviveDuration)
            return;

        if (target.RecoverFromDowned())
            ReviveItemCount--;
        CancelRevive();
    }

    private bool RecoverFromDowned()
    {
        if (!Object.HasStateAuthority || !playerGameState.IsDowned ||
            !playerGameState.IsInPlayground || BaseMaxStamina <= 0f)
            return false;

        // 최대치도 같이 복구한다. 영구 피해로 쓰러진 경우에도 소생 가능.
        float restored = BaseMaxStamina * reviveStaminaRatio;
        PermanentDamage = Mathf.Min(PermanentDamage, BaseMaxStamina - restored);
        TemporaryDamage = 0f;
        BurnRemaining = 0f;
        BurnTickAccumulator = 0f;
        RecoveryDelayRemaining = recoverDelay;
        SprintLockRemaining = 0f;
        CurrentStamina = restored;
        playerGameState.Revive();
        return !playerGameState.IsDowned;
    }

    private void ResetVitals()
    {
        EyesOpen = false;
        EyesNeedRelease = false;
        ReviveItemCount = 0;
        ReviveTarget = null;
        ReviveElapsed = 0f;
        ReviveNeedsRelease = false;
        CurrentStamina = BaseMaxStamina;
        TemporaryDamage = 0f;
        PermanentDamage = 0f;
        BurnRemaining = 0f;
        BurnTickAccumulator = 0f;
        RecoveryDelayRemaining = 0f;
        SprintLockRemaining = 0f;
        isShotGunHit = false;
    }

    private void ClampCurrentStamina()
    {
        CurrentStamina = Mathf.Min(CurrentStamina, CurrentMaxStamina);
    }

    private static bool IsPositiveFinite(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
