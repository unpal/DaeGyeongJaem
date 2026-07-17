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
    [SerializeField] private GameObject ParentsSliderGameObject;
    private void Start()
    {
        //원래는 플레이어가 씬에 생성되있던 상태였지만 플레이어가 프리펩으로 스폰되기에 처음부터 플레이어 생성 이벤트를 리슨하도록 변경
        ParentsSliderGameObject.SetActive(false);
        PlayerStamina.OnLocalPlayerSpawned += SetStamina;
        PlayerCondition.OnLocalPlayerSpawned += SetCondition;
    }
    private void OnEnable()
    {
    }

    private void OnDisable()
    {

    }

    private void SetStamina(PlayerStamina stamina)
    {
        this.stamina = stamina;
        TryInitialize();
    }
    private void SetCondition(PlayerCondition condition)
    {
        this.condition = condition;
        TryInitialize();
    }
    private void TryInitialize()
    {
        if (stamina == null || condition == null)
            return;
        float baseMax = condition.BaseMaxStamina;

        currentSlider.maxValue = baseMax;
        maxSlider.maxValue = baseMax;

        ParentsSliderGameObject.SetActive(true);

        // 더 이상 이벤트가 필요 없으므로 해제
        PlayerStamina.OnLocalPlayerSpawned -= SetStamina;
        PlayerCondition.OnLocalPlayerSpawned -= SetCondition;
    }
    private void Update()
    {
        if (condition != null && stamina != null)
        {
            float baseMax = condition.BaseMaxStamina;

            currentSlider.maxValue = baseMax;
            maxSlider.maxValue = baseMax;

            currentSlider.value = stamina.CurrentStamina;
            maxSlider.value = condition.CurrentMaxStamina;
        }
    }
}