using System.Collections;
using Fusion;
using Script.dotori;
using Script.UI;
using UnityEngine;

public enum RoundPhase
{
    Starting,       // 게임 시작 전 카운트다운/준비/프롤로그
    Playing,        // 라운드 플레이 중 (보스 등장 후 본격 시작)
    RoundFinished,  // 라운드 종료
    GameFinished    // 전체 매치 종료
}

/// <summary>
/// 게임 내적 시스템: 이번 라운드의 실시간 플레이 판정(탈출/사망) 및 인게임 아나운서/시각 연출을 총괄하는 매니저
/// </summary>
public class GameManager : NetworkBehaviour
{
    [Header("Managers")]
    public PrototypeRoundManager protoRoundManager;
    public SequentialPlayer sequentialPlayer;

    private PlayerGameState pendingRoundWinner; // 이번 라운드 1등 탈출자

    [Networked]
    public RoundPhase Phase { get; private set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Phase = RoundPhase.Starting;

            if (protoRoundManager == null)
                protoRoundManager = FindFirstObjectByType<PrototypeRoundManager>();

            if (sequentialPlayer == null)
                sequentialPlayer = FindFirstObjectByType<SequentialPlayer>();

            PrototypeRoundManager.OnRoundStart += OnRoundStartedFromManager;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        PrototypeRoundManager.OnRoundStart -= OnRoundStartedFromManager;
    }

    private void OnRoundStartedFromManager(PrototypeRoundManager manager)
    {
        bool isFirstRound = (manager != null && manager.RoundNumber <= 1);
        StartRound(isFirstRound);
    }

    /// <summary>
    /// 라운드가 시작될 때 호출 (프롤로그 및 카운트다운 동안은 Starting 유지)
    /// </summary>
    public void StartRound(bool isFirstRound = false)
    {
        if (!Object.HasStateAuthority)
            return;

        Phase = RoundPhase.Starting;
        pendingRoundWinner = null;

        if (sequentialPlayer != null)
        {
            sequentialPlayer.StartSequence(isFirstRound);
        }
    }

    /// <summary>
    /// 프롤로그와 10초 카운트다운이 끝나고 보스가 등장하며 본격 플레이가 시작될 때 호출
    /// </summary>
    public void OnGameplayStarted()
    {
        if (!Object.HasStateAuthority)
            return;

        Phase = RoundPhase.Playing;
    }

    /// <summary>
    /// 플레이어가 포탈에 도달하여 탈출했을 때 호출
    /// </summary>
    public void ReportPlayerEscaped(PlayerGameState player)
    {
        if (!Object.HasStateAuthority || Phase != RoundPhase.Playing || player == null || !player.IsInPlayground)
            return;

        player.MarkEscaped();

        // 실시간 탈출 안내 자막 출력 (Phrases.csv의 PlayerEscaped 키 사용)
        if (DialogueController.Instance != null)
        {
            DialogueController.Instance.RpcShow("PlayerEscaped", player.DisplayName.ToString());
        }

        // 가장 먼저 탈출한 플레이어를 라운드 승자로 기록
        if (pendingRoundWinner == null)
        {
            pendingRoundWinner = player;
        }

        CheckRoundFinished();
    }

    /// <summary>
    /// 플레이어가 사망했을 때 호출
    /// </summary>
    public void ReportPlayerDied(PlayerGameState player)
    {
        if (!Object.HasStateAuthority || Phase != RoundPhase.Playing || player == null || !player.IsInPlayground)
            return;

        player.MarkDead();

        // 실시간 탈락 안내 자막 출력 (Phrases.csv의 PlayerDied 키 사용)
        if (DialogueController.Instance != null)
        {
            DialogueController.Instance.RpcShow("PlayerDied", player.DisplayName.ToString());
        }

        CheckRoundFinished();
    }

    /// <summary>
    /// 모든 플레이어의 상태를 검사하여 라운드 종료 여부 판정
    /// </summary>
    public void CheckRoundFinished()
    {
        if (!Object.HasStateAuthority || Phase != RoundPhase.Playing)
            return;

        PlayerGameState[] players = FindObjectsByType<PlayerGameState>(FindObjectsSortMode.None);
        if (players.Length == 0)
            return;

        // 아직 플레이그라운드에서 활동 중인 생존자가 한 명이라도 있으면 진행
        foreach (PlayerGameState player in players)
        {
            if (player.IsInPlayground)
                return;
        }

        // 전원 탈출 또는 사망 시 라운드 종료 처리
        FinishRound();
    }

    private void FinishRound()
    {
        Phase = RoundPhase.RoundFinished;

        // 1. 진행 중이던 시퀀스(적, 포탈, BGM) 정리
        if (sequentialPlayer != null)
        {
            sequentialPlayer.StopSequence();
        }

        // 2. PrototypeRoundManager가 연결되어 있다면 라운드 결과 데이터 보고
        if (protoRoundManager != null)
        {
            PlayerRef winnerRef = pendingRoundWinner != null ? pendingRoundWinner.Object.InputAuthority : PlayerRef.None;
            protoRoundManager.EndRound(winnerRef);
        }
        else
        {
            // PrototypeRoundManager 없이 독립 실행되는 경우의 폴백
            StartCoroutine(StandaloneRoundFinishRoutine());
        }
    }

    // ================= 안내 및 시각 연출 (Announcer) =================

    /// <summary>
    /// 라운드 종료 시 승자/생존 결과 메시지 출력
    /// </summary>
    public void ShowRoundResult(PlayerGameState roundWinner)
    {
        if (DialogueController.Instance == null)
            return;

        if (roundWinner != null)
        {
            DialogueController.Instance.RpcShow("RoundWin", roundWinner.DisplayName.ToString());
        }
        else
        {
            DialogueController.Instance.RpcShow("NoSurvivors");
        }
    }

    /// <summary>
    /// 전체 매치 최종 우승 메시지 출력
    /// </summary>
    public void ShowMatchWinner(PlayerGameState finalWinner)
    {
        if (DialogueController.Instance == null || finalWinner == null)
            return;

        Phase = RoundPhase.GameFinished;
        DialogueController.Instance.RpcShow("MatchWin", finalWinner.DisplayName.ToString());
    }

    /// <summary>
    /// PrototypeRoundManager가 없을 때 단독 테스트용 라운드 루틴
    /// </summary>
    private IEnumerator StandaloneRoundFinishRoutine()
    {
        ShowRoundResult(pendingRoundWinner);

        yield return new WaitForSeconds(3f);

        // 플레이어 리셋
        PlayerGameState[] players = FindObjectsByType<PlayerGameState>(FindObjectsSortMode.None);
        foreach (PlayerGameState player in players)
        {
            player.ResetForNextRound();
        }

        StartRound(false);
    }
}
