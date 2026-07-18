using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCondition : MonoBehaviour
{
    [Header("Base")]
    [SerializeField] private float baseMaxStamina = 100f;

    [Header("Damage")]
    [SerializeField] private float permanentDamage = 0f;
    [SerializeField] private float temporaryDamage = 0f;

    [Header("Sprint Lock")]
    [SerializeField] private float sprintLockTimer = 0f;

    [Header("Burn")] //화상관련
    [SerializeField] private float burnTime;
    [SerializeField] private float burnDuration = 3f;
    [SerializeField] private float burnTick = 0.5f; 
    [SerializeField] private float burnDamage = 2f;

    private float burnTickTimer;

    [Header("Recovery")]
    [SerializeField] private float recoverDelay = 5f;     // 화상 종료 후 대기
    [SerializeField] private float recoverRate = 1f;      // 초당 회복

    private float recoverTimer;

    //추가,
    private PlayerGameState playerGameState; // 게임진행상태 가져오기(플레이어별)
    private GameManager gameManager; // 게임매니저
    private PrototypeRoundManager prototypeRoundManager;
    private bool deathReported; // 사망했는지 확인하는 변수 < checkgameover 계속 실행되어도 사망보고는 한번만<중복사망방지

    [SerializeField]
    public float BaseMaxStamina => baseMaxStamina; //아 짜증나
    public float TemporaryDamage => temporaryDamage;//추가 ui용으로 값 가져오기용
    public float PermanentDamage => permanentDamage;

    public bool isShotGunHit;
    public float CurrentMaxStamina
    {
        get
        {
            return Mathf.Clamp(
                baseMaxStamina - permanentDamage - temporaryDamage,
                0,
                baseMaxStamina);
        }
    }

    public bool CanSprint => sprintLockTimer <= 0f; //총에 맞은것처럼~

    public bool IsGameOver => CurrentMaxStamina <= 0f;// 게임오버조건

    [SerializeField]
    private float burnRecovery = 1f; //화상회복틱당몇?


    //추가
    private void Awake()
    {
        playerGameState = GetComponent<PlayerGameState>(); // 컴포넌트 가져오기
    }

    void Update()
    {
        //리팩토링
        HandleBurn();

        HandleRecover();

        HandleSprintLock();

        CheckGameOver();

        if(isShotGunHit)
        {
            isShotGunHit = false;
        }

        /* if (temporaryDamage > 0)
         {
             RecoverTemporaryDamage(
                 burnRecovery * Time.deltaTime);
         }

         if (sprintLockTimer > 0)
             sprintLockTimer -= Time.deltaTime;
        */
    }

    public void ResetForNextRound()
    {
        deathReported = false;
        permanentDamage = 0f;
        temporaryDamage = 0f;
        sprintLockTimer = 0f;
        burnTime = 0f;
        burnTickTimer = 0f;
        recoverTimer = 0f;
        SyncDamageBreakdown();//추가

        PlayerStamina stamina = GetComponent<PlayerStamina>();
        if (stamina != null)
            stamina.ResetForNextRound();
    }

    //이거 추가, 게임오버조건체크 > 
    private void CheckGameOver()
    {
        //사망보고 되었는지(중복사망불가), 회복가능한 최대 스태미나가 0 이하, 컴포넌트 부재시 오류 방지,
        //fusion? networkbehavior 오브젝트 부재 오류 방지, 호스트인지 확인(fusion)
        if (!deathReported && CurrentMaxStamina <= 0f &&
            playerGameState != null &&
            playerGameState.Object != null &&
            playerGameState.Object.HasStateAuthority)
        {
            //gamemanager 있어야함.
            if (gameManager == null)
                gameManager = FindFirstObjectByType<GameManager>();

            //라운드 플레이중인지
            if (gameManager != null && gameManager.Phase == RoundPhase.Playing)
            {
                deathReported = true;
                gameManager.ReportPlayerDied(playerGameState);
            }
            else
            {
                if (prototypeRoundManager == null)
                    prototypeRoundManager = FindFirstObjectByType<PrototypeRoundManager>();

                if (prototypeRoundManager != null &&
                    prototypeRoundManager.Phase == PrototypeRoundPhase.Playing)
                {
                    deathReported = true;
                    prototypeRoundManager.ReportPlayerEliminated(playerGameState);
                }
            }
        }
    }

    private void HandleBurn()
    {
        if (burnTime <= 0)
            return;

        burnTime -= Time.deltaTime;

        burnTickTimer += Time.deltaTime;

        if (burnTickTimer >= burnTick)
        {
            burnTickTimer = 0;

            ApplyTemporaryDamage(burnDamage);
        }

        // 화상 걸린동안에는 회복 x
        recoverTimer = recoverDelay;
    }

    private void HandleRecover()
    {
        // 아직 화상 중이면 회복하지 않음
        if (burnTime > 0f)
            return;

        // 회복 대기시간
        if (recoverTimer > 0f)
        {
            recoverTimer -= Time.deltaTime;
            return;
        }

        // 천천히 회복
        if (temporaryDamage > 0f)
        {
            RecoverTemporaryDamage(recoverRate * Time.deltaTime);
        }
    }

    public void RefreshBurn()
    {
        burnTime = burnDuration;
    }

    private void HandleSprintLock()
    {
        if (sprintLockTimer > 0f)
        {
            sprintLockTimer -= Time.deltaTime;

            if (sprintLockTimer < 0f)
                sprintLockTimer = 0f;
        }
    }
    //나중에 총맞을때 condition.LockSprint(2f); 이것만 추가해주면 총맞고 못뛰게 할수이썽요.
    public void LockSprint(float seconds)
    {
        sprintLockTimer = Mathf.Max(sprintLockTimer, seconds);
    }

    public void ApplyPermanentDamage(float amount)
    {
        permanentDamage += amount;
        SyncDamageBreakdown();//+

        Debug.Log($"영구 손상 +{amount}"); //디버깅용 나중에 지우기
        Debug.Log($"현재 최대 스태미나 : {CurrentMaxStamina}");
    }

    public void ApplyTemporaryDamage(float amount)
    {
        temporaryDamage += amount;
        recoverTimer = recoverDelay;
        SyncDamageBreakdown();//+
    }

    public void RecoverTemporaryDamage(float amount)    
    {
        temporaryDamage -= amount;
        temporaryDamage = Mathf.Max(0, temporaryDamage);
        SyncDamageBreakdown();
    }
    //hmm
    private void SyncDamageBreakdown()
    {
        if (playerGameState != null)
            playerGameState.SetDamageBreakdown(temporaryDamage, permanentDamage);
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == "ShotgunHit")
        {
            ApplyTemporaryDamage(70);
        }
    }
}
