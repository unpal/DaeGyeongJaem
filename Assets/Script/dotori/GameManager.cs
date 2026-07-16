using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Script.sound;

public class GameManager : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text centerText;
    public TMP_Text bottomText;

    [Header("References")]
    public GameObject chaserPrefab;
    public Transform chaserSpawnPoint;
    public PortalManager portalManager;

    [Header("Settings")]
    public float ENDGAME_TIMER = 60f; // Inspector에서 조절 가능하도록 하되 요구사항의 const 느낌을 반영
    
    private const float SUBTITLE_MAINTAIN_TIME = 3f; // 자막 유지 시간 (n초)
    private const float SUBTITLE_FADE_TIME = 2f;     // 자막 페이드아웃 시간 (n초)

    private float timer = 0f;
    private SoundFollowingAgent spawnedChaser;
    private Coroutine subtitleCoroutine;

    void Start()
    {
        // 바텀 텍스트(자막) 초기화
        if (bottomText != null)
        {
            Color c = bottomText.color;
            c.a = 0f;
            bottomText.color = c;
            bottomText.text = "";
        }

        StartCoroutine(GameFlowRoutine());
    }

    private IEnumerator GameFlowRoutine()
    {
        // 0 ~ 10초: 화면 가운데 TMP에 카운트다운
        while (timer < 10f)
        {
            timer += Time.deltaTime;
            int countDown = Mathf.CeilToInt(10f - timer);
            if (centerText != null)
                centerText.text = countDown.ToString();
            yield return null;
        }

        // 10초: 시작 및 술래 생성
        if (centerText != null)
        {
            centerText.text = "시작";
            // '시작' 텍스트도 자막과 동일하게 유지 후 페이드아웃 되도록 추가
            StartCoroutine(FadeOutText(centerText, SUBTITLE_MAINTAIN_TIME, SUBTITLE_FADE_TIME));
        }

        ShowSubtitle("술래가 생성되었습니다.");

        if (chaserPrefab != null)
        {
            Vector3 spawnPos = chaserSpawnPoint != null ? chaserSpawnPoint.position : Vector3.zero;
            Quaternion spawnRot = chaserSpawnPoint != null ? chaserSpawnPoint.rotation : Quaternion.identity;
            
            GameObject chaserObj = Instantiate(chaserPrefab, spawnPos, spawnRot);
            spawnedChaser = chaserObj.GetComponent<SoundFollowingAgent>();
        }

        // 30초까지 대기
        while (timer < 30f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 30초: 탈출 지점 활성화
        ShowSubtitle("탈출 지점이 생겼습니다");
        if (portalManager != null)
        {
            portalManager.ActivateRandomPortals(1);
        }

        // ENDGAME_TIMER 까지 대기
        while (timer < ENDGAME_TIMER)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // ENDGAME_TIMER 초: 추적자 상태 변경
        if (spawnedChaser != null)
        {
            spawnedChaser.SetStateToKnowWhereYouAre();
        }
    }

    private void ShowSubtitle(string text)
    {
        if (bottomText == null) return;

        if (subtitleCoroutine != null)
        {
            StopCoroutine(subtitleCoroutine);
        }

        bottomText.text = text;
        Color c = bottomText.color;
        c.a = 1f;
        bottomText.color = c;

        subtitleCoroutine = StartCoroutine(FadeOutText(bottomText, SUBTITLE_MAINTAIN_TIME, SUBTITLE_FADE_TIME));
    }

    private IEnumerator FadeOutText(TMP_Text textComponent, float maintainTime, float fadeTime)
    {
        yield return new WaitForSeconds(maintainTime);

        Color c = textComponent.color;
        float fadeTimer = 0f;
        while (fadeTimer < fadeTime)
        {
            fadeTimer += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, fadeTimer / fadeTime);
            textComponent.color = c;
            yield return null;
        }

        c.a = 0f;
        textComponent.color = c;
        textComponent.text = "";
    }
}
