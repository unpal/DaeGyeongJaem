using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Script.dotori;

public class PlayerGameState : NetworkBehaviour
{
    private Vector3 roundSpawnPosition;
    private Quaternion roundSpawnRotation;

    //음
    //player가 저장할 정보 : 죽었는지 탈출했는지> 몇번째로 탈출했는가?도 저장해야하나, 왕관, 게임안에 있는지(서버전달용) 
    [Networked]
    public int Crowns { get; private set; } 

    [Networked]
    public NetworkString<_32> DisplayName { get; private set; }

    [Networked] public float CurrentStamina { get; private set; }
    [Networked] public float MaxStamina { get; private set; }

    [Networked]
    public NetworkBool IsDead { get; private set; }

    [Networked]
    public NetworkBool HasEscaped { get; private set; }

    public bool IsInPlayground => !IsDead && !HasEscaped;

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        roundSpawnPosition = transform.position;
        roundSpawnRotation = transform.rotation;
        DisplayName = $"Player {Object.InputAuthority.PlayerId}";
        MaxStamina = 100f;
        CurrentStamina = MaxStamina;
        
        if (Object.HasInputAuthority && CameraManager.Instance)
        {
            CameraManager.Instance.state = this;
        }
    }

    public void MarkDead() //죽은거 마킹
    {
        if (!Object.HasStateAuthority || HasEscaped)
            return;

        IsDead = true;
    }

    public void MarkEscaped() // 탈출한거 마킹
    {
        if (!Object.HasStateAuthority || IsDead)
            return;

        HasEscaped = true;
    }

    public void AddCrown() //왕관추가
    {
        if (!Object.HasStateAuthority)
            return;

        Crowns++;
    }

    public void ResetForNewMatch()
    {
        if (!Object.HasStateAuthority)
            return;

        Crowns = 0;
        IsDead = false;
        HasEscaped = false;
    }

    public bool TryUseStamina(float amount)
    {
        if (!Object.HasStateAuthority || amount <= 0f /*|| CurrentStamina < amount*/|| CurrentStamina <= 0)
            return false;

        CurrentStamina -= amount;

        if (CurrentStamina < 0)
            CurrentStamina = 0;
        return true;
    }

    public void RecoverStamina(float amount)
    {
        if (!Object.HasStateAuthority || amount <= 0f)
            return;

        CurrentStamina = Mathf.Min(CurrentStamina + amount, MaxStamina);
    }

    public void SetMaxStamina(float amount)
    {
        if (!Object.HasStateAuthority)
            return;

        MaxStamina = Mathf.Max(0f, amount);
        CurrentStamina = Mathf.Min(CurrentStamina, MaxStamina);
    }

    public void ResetStamina()
    {
        if (!Object.HasStateAuthority)
            return;

        CurrentStamina = MaxStamina;
    }

    public void ResetForNextRound() //초기화
    {
        if (!Object.HasStateAuthority)
            return;

        IsDead = false;
        HasEscaped = false;

        PlayerCondition condition = GetComponent<PlayerCondition>();

        if (condition != null)
        {
            condition.ResetForNextRound();
            SetMaxStamina(condition.CurrentMaxStamina);
        }

        ResetStamina();

        PlayerMove move = GetComponent<PlayerMove>();
        if (move != null)
            move.ResetForNextRound();

        FallDamage fallDamage = GetComponent<FallDamage>();
        if (fallDamage != null)
            fallDamage.ResetForNextRound();

        NetworkCharacterController controller =
            GetComponent<NetworkCharacterController>();

        if (controller != null)
            controller.Teleport(roundSpawnPosition, roundSpawnRotation);
        else
            transform.SetPositionAndRotation(
                roundSpawnPosition,
                roundSpawnRotation);
    }

}
