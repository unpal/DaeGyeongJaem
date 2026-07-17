using System.Collections;
using Fusion;
using Script.sound;
using TMPro;
using UnityEngine;

public enum RoundPhase
{
    Starting, //게임시작전 카운트다운
    Playing, //게임중
    RoundFinished, // 라운드 종료
    GameFinished //전체 게임 종료
}

public class GameManager : NetworkBehaviour
{
    [Header("UI")]
    public TMP_Text centerText; //화면 중앙 텍스트 > 카운트다운, 시작
        
    public TMP_Text bottomText; //화면 하단 텍스트 > 추격자,포탈생성

    [Header("References")]  
    public GameObject chaserPrefab; // 추격자 프리팹
    public Transform chaserSpawnPoint; // 추격자 생성 위치,방향
    public PortalManager portalManager; // 포탈관리자(활성화)

    [Header("Settings")]
    public float ENDGAME_TIMER = 60f; // 게임시작후 추격자가 플레이어 위치를 알게되는 시간?

    private const float SUBTITLE_MAINTAIN_TIME = 3f; // 하단 텍스트 완전히 떠있는 시간
    private const float SUBTITLE_FADE_TIME = 2f; // 서서히 사라지는 시간
    private const float ROUND_RESULT_TIME = 3f;

    private float timer; //경과시간, 타이머
    private SoundFollowingAgent spawnedChaser; // 추격자 ai 컴포넌트래요
    private Coroutine subtitleCoroutine; // 자막 페이드 코루틴?
    private Coroutine gameFlowCoroutine;
    private Coroutine roundFinishCoroutine;
    private PlayerGameState pendingRoundWinner; // 크라운위너 저장용도 (1등)

    [Networked] // < 네트워크. State Authority가 값을 변경하고, 다른 클라이언트에 동기화하는식.
    public RoundPhase Phase { get; private set; }

    //fusion 네트워크 오브젝트가 생성되면 호출
    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Phase = RoundPhase.Starting;
        }
    }

    // Unity 오브젝트가 활성화될 때 한 번 호출,
    // UI 초기화와 게임 진행 코루틴을 시작한다.
    private void Start()
    {
        if (bottomText != null)
        {
            Color color = bottomText.color;
            color.a = 0f;
            bottomText.color = color;
            bottomText.text = "";
        }

        gameFlowCoroutine = StartCoroutine(GameFlowRoutine());
    }


    //게임 흐름 처리 카운트다운 > 추격자 생성 > 포탈생성 > endgame(위치노출) 까지
    private IEnumerator GameFlowRoutine()
    {
        while (timer < 10f)
        {
            timer += Time.deltaTime;
            int countdown = Mathf.CeilToInt(10f - timer);

            if (centerText != null)
            {
                centerText.text = countdown.ToString();
            }

            yield return null;
        }

        if (centerText != null)
        {
            centerText.text = "시작";
            StartCoroutine(FadeOutText(
                centerText,
                SUBTITLE_MAINTAIN_TIME,
                SUBTITLE_FADE_TIME));
        }

        if (Object.HasStateAuthority)
        {
            Phase = RoundPhase.Playing;
        }

        ShowSubtitle("추격자 스폰됨");

        if (chaserPrefab != null)
        {
            Vector3 spawnPosition = chaserSpawnPoint != null
                ? chaserSpawnPoint.position
                : Vector3.zero;
            Quaternion spawnRotation = chaserSpawnPoint != null
                ? chaserSpawnPoint.rotation
                : Quaternion.identity;

            GameObject chaserObject = Instantiate(
                chaserPrefab,
                spawnPosition,
                spawnRotation);
            spawnedChaser = chaserObject.GetComponent<SoundFollowingAgent>();
        }

        while (timer < 30f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        ShowSubtitle("탈출 포탈 생성됨");

        if (portalManager != null)
        {
            portalManager.ActivateRandomPortals(1);
        }

        while (timer < ENDGAME_TIMER)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (spawnedChaser != null)
        {
            spawnedChaser.SetStateToKnowWhereYouAre();
        }
    }

    //플레이어가 포탈에 닿음 호출.
    public void ReportPlayerEscaped(PlayerGameState player)
    {
        //호스트만 탈출을 검사함, 게임중이어야하고, 플레이어 아닌건 무시.
        if (!Object.HasStateAuthority || Phase != RoundPhase.Playing || player == null)
            return;

        if (!player.IsInPlayground) //흠
            return;

        player.MarkEscaped();

        if (pendingRoundWinner == null)
        {
            pendingRoundWinner = player;
        }

        CheckRoundFinished();
    }

    //플레이어가 죽으면 호출
    public void ReportPlayerDied(PlayerGameState player)
    {
        if (!Object.HasStateAuthority ||
            Phase != RoundPhase.Playing ||
            player == null ||
            !player.IsInPlayground)
            return;

        player.MarkDead();
        CheckRoundFinished();
    }

    //음
    private void CheckRoundFinished()
    {
        //TODO 인데 플레이어 사망보고 처리, 전체라운드 종료 로직을 여기 추가해야함. < 햇음
        if (!Object.HasStateAuthority)
            return;
        //정신나갈거같아정신나갈거같아정신나갈거같아정신나갈거같아정신나갈거같아
        PlayerGameState[] players =
            FindObjectsByType<PlayerGameState>(FindObjectsSortMode.None);

        if (players.Length == 0)
            return;
        //외계어니
        foreach (PlayerGameState player in players)
        {
            if (player.IsInPlayground)
                return;
        }

        roundFinishCoroutine = StartCoroutine(RoundFinishRoutine());
    }

    private IEnumerator RoundFinishRoutine()
    {
        Phase = RoundPhase.RoundFinished;

        if (gameFlowCoroutine != null)
        {
            StopCoroutine(gameFlowCoroutine);
            gameFlowCoroutine = null;
        }

        if (subtitleCoroutine != null)
        {
            StopCoroutine(subtitleCoroutine);
            subtitleCoroutine = null;
        }

        if (pendingRoundWinner != null)
        {
            pendingRoundWinner.AddCrown();

            if (centerText != null)
                centerText.text = "Round Finished - Crown Awarded!";
        }
        else if (centerText != null)
        {
            centerText.text = "Round Finished - No Winner";
        }

        yield return new WaitForSeconds(ROUND_RESULT_TIME);

        if (centerText != null)
            centerText.text = "";

        roundFinishCoroutine = null;
    }


    //하단에 안내문구 보이게하고 사라지게하는 함수.
    private void ShowSubtitle(string text)
    {
        //nullreference 방지
        if (bottomText == null)
            return;
        //기존 코루틴이 실행중이면, 자막 겹치면 안되니 취소시키기?
        if (subtitleCoroutine != null)
        {
            StopCoroutine(subtitleCoroutine);
        }

        //새 내용
        bottomText.text = text;

        Color color = bottomText.color;
        color.a = 1f;
        bottomText.color = color;
        //유지 > 페이드 아웃 코루틴
        subtitleCoroutine = StartCoroutine(FadeOutText(
            bottomText,
            SUBTITLE_MAINTAIN_TIME,
            SUBTITLE_FADE_TIME));
    }


    //일정시간 유지후, 투명하게.
    private IEnumerator FadeOutText(
        TMP_Text textComponent,
        float maintainTime,
        float fadeTime)
    {
        yield return new WaitForSeconds(maintainTime);

        Color color = textComponent.color;
        float fadeTimer = 0f;

        while (fadeTimer < fadeTime)
        {
            fadeTimer += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, fadeTimer / fadeTime);
            textComponent.color = color;
            yield return null;
        }

        color.a = 0f;
        textComponent.color = color;
        textComponent.text = "";
    }
}
