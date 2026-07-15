using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    public float maxStamina = 100f;
    public float currentStamina;

    //함수제공형태로 바꾸는게 쓰기 편할듯싶어서..

    private void Start()
    {
        currentStamina = maxStamina; //초기화
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
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina); //회복 상한선.
    }

    public bool HasStamina(float amount)
    {
        return currentStamina >= amount; //bool
    }
}