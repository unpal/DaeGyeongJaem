using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStamina : NetworkBehaviour
{

    public static event Action<PlayerStamina> OnLocalPlayerSpawned;

    private PlayerCondition condition; //변경
    [Networked]
    private float currentStamina { get; set; }

    public float CurrentStamina => currentStamina;

    //함수제공형태로 바꾸는게 쓰기 편할듯싶어서..

    //네트워크 오브젝트이기에 Start에서 Spawned로 변경
    public override void Spawned()
    {
        //currentStamina 를 이렇게 변경.
        condition = GetComponent<PlayerCondition>();

        currentStamina = condition.CurrentMaxStamina;

        //나 자신의 플레이어만 이벤트를 보내도록
        if (Object.HasInputAuthority)
        {
            OnLocalPlayerSpawned?.Invoke(this);
        }
    }

    //네트워크 오브젝트 이기에 Update에서 FixedUpdatenetwork()로 변경
    //추가, 최대치가 깎이면 현재 스테미나 도 깎이도록.
    public override void FixedUpdateNetwork()
    {
        currentStamina = Mathf.Min(
            currentStamina,
            condition.CurrentMaxStamina);
    }

    public bool UseStamina(float amount) 
    {
        //피크가 스테미나가 부족해도 0이 아니면 스테미나가 계속 사용되는 점을 보고 해당 부분 주석처리함
        //if (currentStamina < amount) //써야할 stamina의 양보다 적으면 쓸수없음.
        //    return false;

        currentStamina -= amount; //실행
        if (currentStamina < 0)
            currentStamina = 0;
        return true;
    }

    public void RecoverStamina(float amount)
    {
        currentStamina += amount;
        currentStamina = Mathf.Clamp(currentStamina, 0, condition.CurrentMaxStamina); //회복 상한선. 2번째 수정.
    }

    public bool HasStamina(float amount)
    {
        return currentStamina >= amount; //bool
    }
}