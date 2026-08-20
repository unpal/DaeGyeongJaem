using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PlayerStaminaUI : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private PlayerCondition condition;
    [SerializeField] private PlayerGameState gameState;

    [Header("UI")]
    [SerializeField] private Slider currentSlider;
    [SerializeField] private Slider maxSlider;
    [FormerlySerializedAs("ParentsSliderGameObject")]
    [SerializeField] private GameObject parentsSliderGameObject;

    private IEnumerator Start()
    {
        NetworkRunner runner = null;
        NetworkObject localPlayer = null;

        while (localPlayer == null)
        {
            if (runner == null)
                runner = FindFirstObjectByType<NetworkRunner>();

            if (runner != null && runner.IsRunning)
                runner.TryGetPlayerObject(runner.LocalPlayer, out localPlayer);

            yield return null;
        }

        condition = localPlayer.GetComponent<PlayerCondition>();
        gameState = localPlayer.GetComponent<PlayerGameState>();
        Initialize();
    }

    private void Initialize()
    {
        if (condition == null)
        {
            Debug.LogError("Local player is missing PlayerCondition.");
            enabled = false;
            return;
        }

        if (currentSlider == null || maxSlider == null || parentsSliderGameObject == null)
        {
            Debug.LogError("PlayerStaminaUI references are not assigned.");
            enabled = false;
            return;
        }

        float baseMaximum = condition.BaseMaxStamina;
        currentSlider.maxValue = baseMaximum;
        maxSlider.maxValue = baseMaximum;
        parentsSliderGameObject.SetActive(gameState == null || gameState.IsInPlayground);
    }

    private void Update()
    {
        if (condition == null)
            return;

        bool shouldBeVisible = gameState == null || gameState.IsInPlayground;
        if (parentsSliderGameObject.activeSelf != shouldBeVisible)
            parentsSliderGameObject.SetActive(shouldBeVisible);

        if (!shouldBeVisible)
            return;

        float baseMaximum = condition.BaseMaxStamina;
        currentSlider.maxValue = baseMaximum;
        maxSlider.maxValue = baseMaximum;
        currentSlider.value = condition.CurrentStamina;
        maxSlider.value = condition.CurrentMaxStamina;
    }
}
