using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    private PlayerCondition condition; //변경

    private float currentStamina;

    public float CurrentStamina => currentStamina;

    //함수제공형태로 바꾸는게 쓰기 편할듯싶어서..

    private void Start()
    {
        //currentStamina 를 이렇게 변경.
        condition = GetComponent<PlayerCondition>();

        currentStamina = condition.CurrentMaxStamina;
    }

    //추가, 최대치가 깎이면 현재 스테미나 도 깎이도록.
    void Update()
    {
        currentStamina = Mathf.Min(
            currentStamina,
            condition.CurrentMaxStamina);
    }

    public bool UseStamina(float amount) 
    {
        if (currentStamina < amount) //써야할 stamina의 양보다 적으면 쓸수없음.
            return false;

        currentStamina -= amount; //실행
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