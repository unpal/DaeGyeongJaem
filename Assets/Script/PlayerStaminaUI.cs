using System.Collections;
using System.Collections.Generic;
//추가했어요
using Fusion;
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

    /*
    private void Start()
    {
        //원래는 플레이어가 씬에 생성되있던 상태였지만 플레이어가 프리펩으로 스폰되기에 처음부터 플레이어 생성 이벤트를 리슨하도록 변경
        ParentsSliderGameObject.SetActive(false);
    }
    */

    //수정.. 원래 onenable 이랑 밑에 두개 플레이스 홀더 onlocalplayerspanwed onoff 코드였던거같은데 없어서
    //ruuner.localplayer의 playerobject가 준비될때까지 대기 > why?
    // spawned 호출 > spawn 호출 되는 역순 방지를 위해서..?

    private IEnumerator Start()
    {
        NetworkRunner runner = null;
        NetworkObject localPlayer = null;

        while (localPlayer == null)
        {
            if (runner = null)
                runner = FindFirstObjectByType<NetworkRunner>();

            if (runner != null && runner.IsRunning)
                runner.TryGetPlayerObject(
                    runner.LocalPlayer,
                    out localPlayer);

            yield return null;
        }

        stamina = localPlayer.GetComponent<PlayerStamina>();
        condition = localPlayer.GetComponent<PlayerCondition>();

        TryInitialize();
    }

    //혹시 모르니 남겨놓을게요.
    /*
    private void OnEnable()
    {
    }

    private void OnDisable()
    {

    }
    */

    /*
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
    */

    private void TryInitialize()
    {
        if (stamina == null || condition == null)
        {
            Debug.LogError(
                  "로컬 플레이어에 PlayerStamina 또는 PlayerCondition이 없습니다.");
            return;
        }
    

        float baseMax = condition.BaseMaxStamina;

        currentSlider.maxValue = baseMax;
        maxSlider.maxValue = baseMax;

        ParentsSliderGameObject.SetActive(true);

        // 더 이상 이벤트가 필요 없으므로 해제
        /*
        PlayerStamina.OnLocalPlayerSpawned -= SetStamina;
        PlayerCondition.OnLocalPlayerSpawned -= SetCondition;
        */
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