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

public class PrototypeRoundManager : NetworkBehaviour
{
    [SerializeField] private float countdownSeconds = 3f;
    [SerializeField] private float roundTransitionSeconds = 3f;
    [SerializeField] private float winnerMessageSeconds = 5f;
    [SerializeField] private int matchingSceneBuildIndex = 2;

    [Networked] public PrototypeRoundPhase Phase { get; private set; }
    [Networked] public PlayerRef PendingRoundWinner { get; private set; }
    [Networked] public PlayerRef FinalWinner { get; private set; }
    [Networked] public NetworkBool IsRoundEnding { get; private set; }
    [Networked] public NetworkBool IsMatchEnding { get; private set; }
    [Networked] public TickTimer PhaseTimer { get; private set; }
    [Networked] public int RoundNumber { get; private set; }
    public CameraManager cameraManager;

    private bool sceneTransitionRequested;

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        PendingRoundWinner = PlayerRef.None;
        FinalWinner = PlayerRef.None;
        BeginNextRound();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || !PhaseTimer.Expired(Runner))
            return;

        switch (Phase)
        {
            case PrototypeRoundPhase.Starting:
                Phase = PrototypeRoundPhase.Playing;
                PhaseTimer = TickTimer.None;
                break;
            case PrototypeRoundPhase.RoundEnding:
                BeginNextRound();
                break;
            case PrototypeRoundPhase.MatchEnding:
                ReturnToMatchingScene();
                break;
        }
    }

    public void ReportPlayerEscaped(PlayerGameState player)
    {
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
        if (!CanAcceptResult(player))
            return;

        player.MarkDead();
        TryFinishRound();
    }

    public void ReevaluateAfterRosterChange()
    {
        if (Object.HasStateAuthority && Phase == PrototypeRoundPhase.Playing)
            TryFinishRound();
    }

    private bool CanAcceptResult(PlayerGameState player)
    {
        return Object.HasStateAuthority &&
               Phase == PrototypeRoundPhase.Playing &&
               !IsRoundEnding &&
               player != null &&
               player.Object != null &&
               player.IsInPlayground;
    }

    private void TryFinishRound()
    {
        if (IsRoundEnding)
            return;

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            PlayerGameState state = GetPlayerState(playerRef);
            if (state != null && state.IsInPlayground)
                return;
        }

        FinishRound();
    }

    private void FinishRound()
    {
        if (IsRoundEnding)
            return;

        IsRoundEnding = true;

        PlayerGameState winner = GetPlayerState(PendingRoundWinner);
        if (winner != null)
            winner.AddCrown();

        if (winner != null && winner.Crowns >= 2)
        {
            FinalWinner = PendingRoundWinner;
            IsMatchEnding = true;
            Phase = PrototypeRoundPhase.MatchEnding;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, winnerMessageSeconds);
            return;
        }

        Phase = PrototypeRoundPhase.RoundEnding;
        PhaseTimer = TickTimer.CreateFromSeconds(Runner, roundTransitionSeconds);
    }

    private void BeginNextRound()
    {
        if (IsMatchEnding)
            return;

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            PlayerGameState state = GetPlayerState(playerRef);
            if (state == null)
                continue;

            state.ResetForNextRound();
            NetworkCharacterController controller = state.GetComponent<NetworkCharacterController>();
            if (controller != null)
                controller.Teleport(PrototypeSpawnPoints.Get(playerRef.PlayerId), Quaternion.identity);

            FallDamage fallDamage = state.GetComponent<FallDamage>();
            if (fallDamage != null)
                fallDamage.ResetForNextRound();
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
