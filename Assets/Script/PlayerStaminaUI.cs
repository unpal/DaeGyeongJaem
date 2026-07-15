using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStaminaUI : MonoBehaviour
{
    public PlayerStamina playerStamina;
    public Slider staminaSlider;

    void Start()
    {
        staminaSlider.maxValue = playerStamina.maxStamina;
        staminaSlider.value = playerStamina.currentStamina;
    }

    void Update()
    {
        staminaSlider.value = playerStamina.currentStamina;
    }
}