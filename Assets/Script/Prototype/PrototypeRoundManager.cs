using System;
using System.Collections.Generic;
using Fusion;
using Script.dotori;
using UnityEngine;

public enum PrototypeRoundPhase : byte
{
    Starting,
    Playing,
    RoundEnding,
    MatchEnding
}

/// <summary>
/// 게임 외적 시스템: 전체 매치 및 라운드 단위의 라이프사이클(스폰, 텔레포트, 점수/왕관 데이터, 씬 전환)을 총괄하는 컨트롤러 (UI 코드 없음)
/// </summary>
public class PrototypeRoundManager : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float countdownSeconds = 3f;
    [SerializeField] private float roundTransitionSeconds = 3f;
    [SerializeField] private float winnerMessageSeconds = 5f;
    [SerializeField] private int matchingSceneBuildIndex = 1;

    [Header("References")]
    public CameraManager cameraManager;
    public GameManager gameManager;

    [Networked] public PrototypeRoundPhase Phase { get; private set; }
    [Networked] public PlayerRef PendingRoundWinner { get; private set; }
    [Networked] public PlayerRef FinalWinner { get; private set; }
    [Networked] public NetworkBool IsRoundEnding { get; private set; }
    [Networked] public NetworkBool IsMatchEnding { get; private set; }
    [Networked] public TickTimer PhaseTimer { get; private set; }
    [Networked] public int RoundNumber { get; private set; }
    [Networked] public PlayerRef Seeker { get; private set; }

    private bool sceneTransitionRequested;

    public static event Action<PrototypeRoundManager> OnRoundStart;

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        PendingRoundWinner = PlayerRef.None;
        FinalWinner = PlayerRef.None;
        Seeker = PlayerRef.None;
        Phase = PrototypeRoundPhase.Starting;
        PhaseTimer = TickTimer.None;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (RoundNumber == 0)
        {
            // Scene loading and the lobby's player spawns may finish after Spawned().
            var players = new List<PlayerRef>();
            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                if (GetPlayerState(player) == null) return;
                players.Add(player);
            }
            if (players.Count < 2) return;
            Seeker = players[UnityEngine.Random.Range(0, players.Count)];
            foreach (PlayerRef player in players)
                GetPlayerState(player).SetSeeker(player == Seeker);
            BeginNextRound();
            return;
        }

        if (!PhaseTimer.Expired(Runner)) return;

        switch (Phase)
        {
            case PrototypeRoundPhase.Starting:
                Phase = PrototypeRoundPhase.Playing;
                PhaseTimer = TickTimer.None;
                OnRoundStart?.Invoke(this);

                if (gameManager != null)
                {
                    bool isFirstRound = (RoundNumber <= 1);
                    gameManager.StartRound(isFirstRound);
                }
                break;

            case PrototypeRoundPhase.RoundEnding:
                BeginNextRound();
                break;

            case PrototypeRoundPhase.MatchEnding:
                ReturnToMatchingScene();
                break;
        }
    }

    /// <summary>
    /// GameManager에서 라운드가 종료되었을 때 호출하여 데이터/점수 처리 및 다음 라운드/매치 전환 진행
    /// </summary>
    public void EndRound(PlayerRef winnerRef)
    {
        if (!Object.HasStateAuthority || IsRoundEnding || IsMatchEnding)
            return;

        IsRoundEnding = true;
        PendingRoundWinner = winnerRef;

        PlayerGameState winner = GetPlayerState(winnerRef);
        if (winner != null)
        {
            winner.AddCrown();
        }

        // 2관왕 달성 시 최종 매치 승리
        if (winner != null && winner.Crowns >= 2)
        {
            FinalWinner = winnerRef;
            IsMatchEnding = true;
            Phase = PrototypeRoundPhase.MatchEnding;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, winnerMessageSeconds);

            // GameManager에게 최종 우승 연출 요청
            if (gameManager != null)
            {
                gameManager.ShowMatchWinner(winner);
            }
            return;
        }

        Phase = PrototypeRoundPhase.RoundEnding;
        PhaseTimer = TickTimer.CreateFromSeconds(Runner, roundTransitionSeconds);

        // GameManager에게 라운드 결과 연출 요청
        if (gameManager != null)
        {
            gameManager.ShowRoundResult(winner);
        }
    }

    public void ReportPlayerEscaped(PlayerGameState player)
    {
        if (gameManager != null)
        {
            gameManager.ReportPlayerEscaped(player);
            return;
        }

        if (!CanAcceptResult(player))
            return;

        player.MarkEscaped();
        PlayerRef playerRef = player.Object.InputAuthority;
        if (PendingRoundWinner == PlayerRef.None)
            PendingRoundWinner = playerRef;

        TryFinishRound();
    }

    public void ReportPlayerEliminated(PlayerGameState player)
    {
        if (gameManager != null)
        {
            gameManager.ReportPlayerDied(player);
            return;
        }

        if (!CanAcceptResult(player))
            return;

        player.MarkDead();
        TryFinishRound();
    }

    public void ReevaluateAfterRosterChange()
    {
        if (Object.HasStateAuthority && RoundNumber > 0)
        {
            int count = 0;
            bool seekerPresent = false;
            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                if (GetPlayerState(player) == null) continue;
                count++;
                if (player == Seeker) seekerPresent = true;
            }
            if (!seekerPresent || count < 2)
            {
                ReturnToMatchingScene();
                return;
            }
        }
        if (Object.HasStateAuthority && Phase == PrototypeRoundPhase.Playing)
        {
            if (gameManager != null)
                gameManager.CheckRoundFinished();
            else
                TryFinishRound();
        }
    }

    private bool CanAcceptResult(PlayerGameState player)
    {
        return Object.HasStateAuthority &&
               Phase == PrototypeRoundPhase.Playing &&
               !IsRoundEnding &&
               player != null &&
               player.Object != null &&
               !player.IsSeeker &&
               player.IsInPlayground;
    }

    private void TryFinishRound()
    {
        if (IsRoundEnding)
            return;

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            PlayerGameState state = GetPlayerState(playerRef);
            if (state != null && !state.IsSeeker && state.IsInPlayground)
                return;
        }

        EndRound(PendingRoundWinner == PlayerRef.None ? Seeker : PendingRoundWinner);
    }

    private void BeginNextRound()
    {
        if (IsMatchEnding)
            return;

        RenderSettings.fog = true;

        Transform seekerSpawn = gameManager != null && gameManager.sequentialPlayer != null
            ? gameManager.sequentialPlayer.chaserSpawnPoint : null;
        int runnerSpawnIndex = 0;
        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            PlayerGameState state = GetPlayerState(playerRef);
            if (state == null)
                continue;

            PlayerRoundLifecycle lifecycle = state.GetComponent<PlayerRoundLifecycle>();
            if (lifecycle != null)
                lifecycle.ResetForRound(
                    state.IsSeeker
                        ? (seekerSpawn != null ? seekerSpawn.position : new Vector3(0f, 1.2f, 0f))
                        : PrototypeSpawnPoints.Get(runnerSpawnIndex++),
                    state.IsSeeker && seekerSpawn != null ? seekerSpawn.rotation : Quaternion.identity);
        }

        PendingRoundWinner = PlayerRef.None;
        IsRoundEnding = false;
        RoundNumber++;
        Phase = PrototypeRoundPhase.Starting;
        PhaseTimer = TickTimer.CreateFromSeconds(Runner, countdownSeconds);
    }

    public PlayerGameState GetPlayerState(PlayerRef playerRef)
    {
        if (playerRef == PlayerRef.None ||
            !Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) ||
            playerObject == null)
            return null;

        return playerObject.GetComponent<PlayerGameState>();
    }

    private void ReturnToMatchingScene()
    {
        if (!Object.HasStateAuthority || sceneTransitionRequested)
            return;

        sceneTransitionRequested = true;

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            PlayerGameState state = GetPlayerState(playerRef);
            if (state != null)
                state.ResetForNewMatch();
        }

        Runner.LoadScene(SceneRef.FromIndex(matchingSceneBuildIndex));
    }
}
