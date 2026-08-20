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
    private bool deathReported;

    public float BaseMaxStamina => Mathf.Max(0f, baseMaxStamina);
    public float CurrentMaxStamina => Mathf.Clamp(
        BaseMaxStamina - PermanentDamage - TemporaryDamage,
        0f,
        BaseMaxStamina);
    public bool CanSprint => SprintLockRemaining <= 0f;
    public bool IsGameOver => CurrentMaxStamina <= 0f;

    // 기존 Shotgun 코드와 Inspector 데이터 호환을 위해 유지한다.
    public bool isShotGunHit;

    private void Awake()
    {
        playerGameState = GetComponent<PlayerGameState>();
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
        UpdateBurn(deltaTime);
        UpdateRecovery(deltaTime);
        SprintLockRemaining = Mathf.Max(0f, SprintLockRemaining - deltaTime);
        CheckGameOver();

        if (isShotGunHit)
            isShotGunHit = false;
    }

    public bool CanUseStamina(float amount)
    {
        // 남은 스태미나가 비용보다 적어도 마지막 행동은 허용한다.
        return IsPositiveFinite(amount) && CurrentStamina > 0f;
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
        if (!Object.HasStateAuthority || !IsPositiveFinite(amount))
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
        if (!Object.HasStateAuthority || !IsPositiveFinite(amount))
            return;

        PermanentDamage = Mathf.Clamp(
            PermanentDamage + amount,
            0f,
            BaseMaxStamina);
        TemporaryDamage = Mathf.Clamp(
            TemporaryDamage,
            0f,
            BaseMaxStamina - PermanentDamage);
        ClampCurrentStamina();
    }

    public void ApplyTemporaryDamage(float amount)
    {
        if (!Object.HasStateAuthority || !IsPositiveFinite(amount))
            return;

        TemporaryDamage = Mathf.Clamp(
            TemporaryDamage + amount,
            0f,
            BaseMaxStamina - PermanentDamage);
        RecoveryDelayRemaining = recoverDelay;
        ClampCurrentStamina();
    }

    public void RecoverTemporaryDamage(float amount)
    {
        if (!Object.HasStateAuthority || !IsPositiveFinite(amount))
            return;

        TemporaryDamage = Mathf.Max(0f, TemporaryDamage - amount);
    }

    public void RefreshBurn()
    {
        if (!Object.HasStateAuthority)
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

    private void CheckGameOver()
    {
        if (deathReported || !IsGameOver || playerGameState == null)
            return;

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager != null && gameManager.Phase == RoundPhase.Playing)
        {
            gameManager.ReportPlayerDied(playerGameState);
            deathReported = playerGameState.IsDead;
            return;
        }

        if (prototypeRoundManager == null)
            prototypeRoundManager = FindFirstObjectByType<PrototypeRoundManager>();

        if (prototypeRoundManager != null &&
            prototypeRoundManager.Phase == PrototypeRoundPhase.Playing)
        {
            prototypeRoundManager.ReportPlayerEliminated(playerGameState);
            deathReported = playerGameState.IsDead;
        }
    }

    private void ResetVitals()
    {
        deathReported = false;
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
