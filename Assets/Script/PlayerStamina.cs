using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    private PlayerCondition condition; //변경
    private PlayerGameState gameState;

    private float currentStamina;

    public float CurrentStamina => gameState != null
        ? gameState.CurrentStamina
        : currentStamina;

    //함수제공형태로 바꾸는게 쓰기 편할듯싶어서..

    private void Start()
    {
        //currentStamina 를 이렇게 변경.
        condition = GetComponent<PlayerCondition>();
        gameState = GetComponent<PlayerGameState>();

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
        if (gameState != null)
            return gameState.TryUseStamina(amount);

        if (currentStamina < amount) //써야할 stamina의 양보다 적으면 쓸수없음.
            return false;

        currentStamina -= amount; //실행
        return true;
    }

    public void RecoverStamina(float amount)
    {
        if (gameState != null)
        {
            gameState.RecoverStamina(amount);
            return;
        }

        currentStamina += amount;
        currentStamina = Mathf.Clamp(currentStamina, 0, condition.CurrentMaxStamina); //회복 상한선. 2번째 수정.
    }

    public bool HasStamina(float amount)
    {
        return CurrentStamina >= amount; //bool
    }

    public void ResetForNextRound()
    {
        if (condition == null)
            condition = GetComponent<PlayerCondition>();

        if (gameState == null)
            gameState = GetComponent<PlayerGameState>();

        if (gameState != null)
        {
            gameState.ResetStamina();
            return;
        }

        currentStamina = condition != null
            ? condition.CurrentMaxStamina
            : 0f;
    }
}
