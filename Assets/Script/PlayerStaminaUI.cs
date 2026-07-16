using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStaminaUI : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private PlayerStamina stamina;
    [SerializeField] private PlayerCondition condition;

    [Header("UI")]
    [SerializeField] private Slider currentSlider;
    [SerializeField] private Slider maxSlider;

    private void Start()
    {
        float baseMax = condition.BaseMaxStamina;

        currentSlider.maxValue = baseMax;
        maxSlider.maxValue = baseMax;
    }

    private void Update()
    {
        float baseMax = condition.BaseMaxStamina;

        currentSlider.maxValue = baseMax;
        maxSlider.maxValue = baseMax;

        currentSlider.value = stamina.CurrentStamina;
        maxSlider.value = condition.CurrentMaxStamina;
    }
}