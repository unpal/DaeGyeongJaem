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
    [Header("UI")] public TMP_Text centerText; //화면 중앙 텍스트 > 카운트다운, 시작

    public TMP_Text bottomText; //화면 하단 텍스트 > 추격자,포탈생성

    [Header("References")] public GameObject chaserPrefab; // 추격자 프리팹
    public Transform chaserSpawnPoint; // 추격자 생성 위치,방향
    public PortalManager portalManager; // 포탈관리자(활성화)
    public PrototypeRoundManager protoRoundManager; //게임 라운드 매니저

    [Header("Settings")] public float ENDGAME_TIMER = 60f; // 게임시작후 추격자가 플레이어 위치를 알게되는 시간?

    [SerializeField] private int matchingSceneBuildIndex = 0; // 추가, scene에 index 붙여서 종료화면, 게임화면, 로비화면 가리키도록 하는거.
    //지금은 samplescene = 0, dotoriscene = 1 이라 0으로 해놨는데 나중에 순서 바뀌거나 scene 추가되면 손 봐줄것.

    private const float SUBTITLE_MAINTAIN_TIME = 3f; // 하단 텍스트 완전히 떠있는 시간
    private const float SUBTITLE_FADE_TIME = 2f; // 서서히 사라지는 시간
    private const float ROUND_RESULT_TIME = 3f; // 결과 보여주는 시간? 이거 시간으로 안하고 버튼 누르는 식으로 해도 될거같은데. 나중에 변경

    private float timer; //경과시간, 타이머

    private SoundFollowingAgent spawnedChaser; // 추격자 ai 컴포넌트래요

    //추가
    private Coroutine subtitleCoroutine; // 자막 페이드 코루틴?
    private Coroutine centerFadeCoroutine;
    private Coroutine gameFlowCoroutine; // 게임 진행 코루틴

    private Coroutine roundFinishCoroutine; // 엔딩화면 코루틴

    //거의 다 추가한거라..
    private PlayerGameState pendingRoundWinner; // 크라운위너 저장용도 (1등)


    [Networked] // < 네트워크. State Authority가 값을 변경하고, 다른 클라이언트에 동기화하는식.
    public RoundPhase Phase { get; private set; }

    //fusion 네트워크 오브젝트가 생성되면 호출
    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Phase = RoundPhase.Starting;
            //gameFlowCoroutine = StartCoroutine(GameFlowRoutine());
            PrototypeRoundManager.OnRoundStart += GameFlowStart;
        }
    }

    private void GameFlowStart(PrototypeRoundManager manager)
    {
        gameFlowCoroutine = StartCoroutine(GameFlowRoutine());
        PrototypeRoundManager.OnRoundStart -= GameFlowStart;
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
    }

    //추가
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcSetCenterText(string text)
    {
        centerText.text = text;
    }

    //훔
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcPrepareCenterText()
    {
        if (centerText == null)
            return;

        if (centerFadeCoroutine != null)
        {
            StopCoroutine(centerFadeCoroutine);
            centerFadeCoroutine = null;
        }

        Color color = centerText.color;
        color.a = 1f;
        centerText.color = color;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcCenterFadeOut()
    {
        if (centerText == null)
            return;

        if (centerFadeCoroutine != null)
            StopCoroutine(centerFadeCoroutine);

        centerFadeCoroutine = StartCoroutine(FadeOutText(
            centerText,
            SUBTITLE_MAINTAIN_TIME,
            SUBTITLE_FADE_TIME));
    }

    //게임 흐름 처리 카운트다운 > 추격자 생성 > 포탈생성 > endgame(위치노출) 까지
    private IEnumerator GameFlowRoutine()
    {
        if (!Object.HasStateAuthority)
            yield break;

        float countdownTime = 10f;
        RpcPrepareCenterText();

        while (timer < countdownTime)
        {
            timer += Time.deltaTime;
            int countdown = Mathf.CeilToInt(countdownTime - timer);

            if (centerText != null)
            {
                RpcSetCenterText(countdown.ToString());
            }

            yield return null;
        }

        Phase = RoundPhase.Playing;
        RpcPlayBGM(false); // 게임 시작 시 기본 BGM 재생

        if (chaserPrefab != null)
        {
            Vector3 spawnPosition = chaserSpawnPoint != null
                ? chaserSpawnPoint.position
                : Vector3.zero;
            Quaternion spawnRotation = chaserSpawnPoint != null
                ? chaserSpawnPoint.rotation
                : Quaternion.identity;
            /*기존코드 ;
             *
             *  gameObject chaserObject = Instantiate(
             *                                  chaserPrefab,
             *                                  spawnPosition,
             *                                  spawnRotation);
             * 이건데
             *
             * spawn(), despanw 이었나 그걸로 바꿨었던거같음
             *
             */
            NetworkObject chaserNetworkPrefab =
                chaserPrefab.GetComponent<NetworkObject>();

            if (chaserNetworkPrefab == null)
            {
                Debug.LogError("[Flow Test] Chaser Prefab에 NetworkObject가 없습니다.");
                yield break;
            }

            NetworkObject chaserObject = Runner.Spawn(
                chaserPrefab.GetComponent<NetworkObject>(),
                spawnPosition,
                spawnRotation);
            spawnedChaser = chaserObject.GetComponent<SoundFollowingAgent>();
            
            RpcPrepareCenterText();
            RpcSetCenterText("도망쳐!");
            RpcCenterFadeOut();
            RpcShowSubtitle("추격자 스폰됨");
            RpcPlayChaserSpawnSound(); // 추격자 등장 사운드 재생

            Debug.Log($"[Flow Test] 추격자 생성 완료: {spawnPosition}");
        }
        else
        {
            Debug.LogError("[Flow Test] GameManager의 Chaser Prefab이 비어 있습니다.");
        }

        while (timer < 30f)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        
        RpcPrepareCenterText();
        RpcSetCenterText("탈출구를 찾으세요");
        RpcCenterFadeOut();

        if (portalManager != null)
        {
            portalManager.ActivateRandomPortals(1);
            RpcPlayPortalSpawnSound(); // 포탈 생성 사운드 재생
        }


        while (timer < ENDGAME_TIMER)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (spawnedChaser != null)
        {
            RpcPrepareCenterText();
            RpcSetCenterText("술래가 당신을 볼 수 있습니다.");
            RpcCenterFadeOut();
            spawnedChaser.SetStateToKnowWhereYouAre();
            RpcPlayBGM(true); // 엔드게임 BGM으로 변경
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
        if (!Object.HasStateAuthority ||
            Phase != RoundPhase.Playing ||
            roundFinishCoroutine != null)
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

    //게임 이긴사람, roundfinishroutine() 에 쓰일부분.
    private IEnumerator GameWinnerRoutine(PlayerGameState winner)
    {
        if (!Object.HasStateAuthority)
            yield break;

        Phase = RoundPhase.GameFinished;

        string winnerName = winner.DisplayName.ToString();

        if (string.IsNullOrWhiteSpace(winnerName))
            winnerName = winner.Object.InputAuthority.ToString();

        if (centerText != null)
            centerText.text = $"{winnerName} is WINNER!";

        yield return new WaitForSeconds(5f); //시간조정하기 or 버튼, 코루틴으로 바꾸기?

        // TODO: authority 뭐시기 해서 host 가 scene transition 하기 결과 > 로비
        //추가완료
        if (Object.HasStateAuthority) //방장만
        {
            Runner.LoadScene( //씬 불러오기
                SceneRef.FromIndex(matchingSceneBuildIndex));
        }
    }

    //추가. 라운드 종료 루틴 다 탈출했는가? > 크라운 2개 있는사람 있는가 > 라운드 다시 진행 OR 게임 승리판정
    private IEnumerator RoundFinishRoutine()
    {
        if (!Object.HasStateAuthority)
            yield break;

        Phase = RoundPhase.RoundFinished;

        if (spawnedChaser != null)
        {
            NetworkObject chaserObject =
                spawnedChaser.GetComponent<NetworkObject>();

            if (chaserObject != null && chaserObject.IsValid)
                Runner.Despawn(chaserObject);

            spawnedChaser = null;
        }

        if (portalManager != null)
            portalManager.DeactivateAllPortals();

        //이전에 게임을 한 상태가 아니면,
        if (gameFlowCoroutine != null)
        {
            StopCoroutine(gameFlowCoroutine);
            gameFlowCoroutine = null;
        }

        //자막 띄울공간에 뭐가 있으면
        if (subtitleCoroutine != null)
        {
            StopCoroutine(subtitleCoroutine);
            subtitleCoroutine = null;
        }

        //우승자가 있으면 add crown ㅇㅇ
        if (pendingRoundWinner != null)
        {
            pendingRoundWinner.AddCrown();

            if (centerText != null)
                centerText.text = "라운드 종료 - 왕관 증정";
        }
        else if (centerText != null)
        {
            //없으면.
            centerText.text = "라운드 종료 - 생존자 없음";
        }

        //코루틴용 리턴문
        yield return new WaitForSeconds(ROUND_RESULT_TIME);

        bool hasGameWinner =
            pendingRoundWinner != null &&
            pendingRoundWinner.Crowns >= 2;

        if (hasGameWinner)
        {
            // Phase = RoundPhase.GameFinished; < winner루틴으로 옮겨짐
            //이러고 여기에 승자이름 is WINNER! 이런 문구 나오게 해줘야해요.
            //게임 이겼으니 흠.. 라운드 진행말고
            yield return StartCoroutine(GameWinnerRoutine(pendingRoundWinner));
            //추가완료.
            yield break;
        }

        //플레이어들 상태 가져와요.
        PlayerGameState[] players =
            FindObjectsByType<PlayerGameState>(FindObjectsSortMode.None);

        //모든 플레이어 상태 초기화
        foreach (PlayerGameState player in players)
        {
            player.ResetForNextRound();
        }

        //초기화
        pendingRoundWinner = null;
        timer = 0f;
        Phase = RoundPhase.Starting;
        //다시 시작~
        gameFlowCoroutine = StartCoroutine(GameFlowRoutine());

        if (centerText != null)
            centerText.text = "";

        roundFinishCoroutine = null;
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    //하단에 안내문구 보이게하고 사라지게하는 함수.
    private void RpcShowSubtitle(string text)
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcPlayBGM(bool isEndgame)
    {
        if (PublicSpeaker.Instance != null)
            PublicSpeaker.Instance.PlayBGM(isEndgame);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcPlayChaserSpawnSound()
    {
        if (PublicSpeaker.Instance != null)
            PublicSpeaker.Instance.PlayChaserSpawn();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcPlayPortalSpawnSound()
    {
        if (PublicSpeaker.Instance != null)
            PublicSpeaker.Instance.PlayPortalSpawn();
    }
}