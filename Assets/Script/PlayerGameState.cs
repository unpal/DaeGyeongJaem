using UnityEngine;
using Fusion;
using Script.dotori;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;


//이번 빌드 변경점 > 메인씬, 로비 씬, 게임 결과 화면 추가,
// 그리고 플레이어 개인별로 이름 설정 가능해짐 > 바꿔야 하는것들 > 그냥 state나 네트워크 관련 변수들 뒤져서 정보 담고있는 변수 끌어와서 덧대기.

public class PlayerGameState : NetworkBehaviour
{
    [Networked] public NetworkBool IsSeeker { get; private set; }
    [Header("Seeker Catch")]
    [SerializeField, Min(0.1f)] private float catchRange = 2f;
    [SerializeField, Range(1f, 89f)] private float catchHalfAngle = 50f;
    [Tooltip("Delay before the server starts checking for a target. Match this to the future animation's impact time.")]
    [SerializeField, Min(0f)] private float catchWindup = 0.18f;
    [SerializeField, Min(0.01f)] private float catchActiveDuration = 0.12f;
    [FormerlySerializedAs("shotInterval")]
    [SerializeField, Min(0.1f)] private float catchInterval = 1f;
    [Header("Catch Presentation (optional; leave empty until art is ready)")]
    [Tooltip("Invoked on every client when the server accepts a catch. Presentation only; do not apply damage here.")]
    [SerializeField] private UnityEvent onCatchSwingStarted = new UnityEvent();
    [Tooltip("Invoked on every client when a catch ends, including a round/role cancellation.")]
    [SerializeField] private UnityEvent onCatchSwingEnded = new UnityEvent();
    [SerializeField] private UnityEvent onCatchSucceeded = new UnityEvent();
    [SerializeField] private UnityEvent onCatchMissed = new UnityEvent();
    [SerializeField] private UnityEvent onCatchCancelled = new UnityEvent();

    private enum CatchResult : byte { Succeeded, Missed, Cancelled }

    [Networked] private TickTimer CatchCooldown { get; set; }
    [Networked] private TickTimer CatchWindupTimer { get; set; }
    [Networked] private TickTimer CatchEndTimer { get; set; }
    [Networked] private NetworkBool CatchWasHeld { get; set; }
    [Networked] private Vector3 CatchDirection { get; set; }
    public bool IsCatchSwinging => CatchWindupTimer.IsRunning;
    private readonly SeekerCatchQuery catchQuery = new SeekerCatchQuery();
    private GameManager roundGame;
    private PrototypeRoundManager roundManager;
    private Un movement;
    private float catchFeedbackUntil;
    private string catchFeedback;

    public bool CanMove
    {
        get
        {
            if (!IsInPlayground || IsDowned) return false;
            if (roundManager == null) roundManager = FindFirstObjectByType<PrototypeRoundManager>();
            if (roundGame == null) roundGame = FindFirstObjectByType<GameManager>();
            if (roundManager == null) return true; // Lobby movement.
            if (roundManager.Object == null || !roundManager.Object.IsValid) return false;
            if (roundManager.RoundNumber == 0 || roundManager.IsRoundEnding || roundManager.IsMatchEnding)
                return false;
            return !IsSeeker || (roundGame != null && roundGame.Object != null &&
                roundGame.Object.IsValid && roundGame.Phase == RoundPhase.Playing);
        }
    }

    public void SetSeeker(bool value)
    {
        if (!Object.HasStateAuthority) return;
        IsSeeker = value;
        ResetCatch();
        GetComponent<PlayerCondition>()?.ResetForNextRound();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        bool hasInput = GetInput(out NetworkInputData input);
        bool pressed = hasInput && input.Buttons.IsSet(PlayerButtons.Catch) && !CatchWasHeld;
        // Missing input must not turn a held button into another press.
        if (hasInput) CatchWasHeld = input.Buttons.IsSet(PlayerButtons.Catch);

        if (!IsSeeker || !CanMove || roundGame == null || roundGame.Object == null ||
            !roundGame.Object.IsValid || !roundGame.Object.HasStateAuthority || roundGame.Runner != Runner ||
            roundGame.Phase != RoundPhase.Playing)
        {
            EndCatchSwing(CatchResult.Cancelled);
            return;
        }

        if (IsCatchSwinging)
        {
            if (CatchWindupTimer.Expired(Runner)) ResolveCatch();
            return;
        }

        if (!pressed || !CatchCooldown.ExpiredOrNotRunning(Runner)) return;
        Vector3 direction = input.AimDirection;
        float magnitude = direction.sqrMagnitude;
        if (float.IsNaN(magnitude) || float.IsInfinity(magnitude) || magnitude < 0.01f)
            return;

        CatchDirection = direction.normalized;
        float windup = Mathf.Max(0f, catchWindup);
        // Preserve at least one simulation tick of impact even with a very short configured window.
        float duration = windup + Mathf.Max(Runner.DeltaTime, catchActiveDuration);
        CatchWindupTimer = TickTimer.CreateFromSeconds(Runner, windup);
        CatchEndTimer = TickTimer.CreateFromSeconds(Runner, duration);
        CatchCooldown = TickTimer.CreateFromSeconds(Runner, Mathf.Max(catchInterval, duration));
        RpcCatchSwingStarted(duration);
        if (CatchWindupTimer.Expired(Runner)) ResolveCatch();
    }

    private void ResolveCatch()
    {
        // Check expiry before querying: a target entering after the active window must not be caught.
        if (CatchEndTimer.Expired(Runner))
        {
            EndCatchSwing(CatchResult.Missed);
            return;
        }

        if (movement == null) movement = GetComponent<Un>();
        Vector3 origin = movement != null ? movement.GetCameraPivotPosition() : transform.position + Vector3.up * 1.5f;
        Transform target = catchQuery.FindTarget(gameObject.scene.GetPhysicsScene(), origin,
            CatchDirection, catchRange, catchHalfAngle, transform, ResolveCatchTarget);
        if (target != null)
        {
            PlayerGameState state = target.GetComponent<PlayerGameState>();
            roundGame.ReportPlayerDowned(state);
            if (state.IsDowned)
            {
                state.GetComponent<PlayerCondition>()?.CancelRevive();
                EndCatchSwing(CatchResult.Succeeded);
                return;
            }
        }
    }

    private Transform ResolveCatchTarget(Collider collider)
    {
        PlayerGameState target = collider.GetComponentInParent<PlayerGameState>();
        return target != null && target != this && target.Object != null && target.Object.IsValid &&
            target.Runner == Runner && target.Object.HasStateAuthority &&
            !target.IsSeeker && target.IsInPlayground && !target.IsDowned
            ? target.transform : null;
    }

    private void EndCatchSwing(CatchResult result)
    {
        if (!IsCatchSwinging) return;
        CatchWindupTimer = TickTimer.None;
        CatchEndTimer = TickTimer.None;
        RpcCatchEnded(result);
    }

    private void ResetCatch()
    {
        CatchCooldown = TickTimer.None;
        EndCatchSwing(CatchResult.Cancelled);
        CatchDirection = Vector3.zero;
        // Require a release after role/round changes, including a held button during loading.
        CatchWasHeld = true;
        catchFeedbackUntil = 0f;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcCatchSwingStarted(float duration)
    {
        catchFeedback = "손 휘두르기";
        catchFeedbackUntil = Time.time + duration;
        onCatchSwingStarted?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcCatchEnded(CatchResult result)
    {
        onCatchSwingEnded?.Invoke();
        switch (result)
        {
            case CatchResult.Succeeded:
                catchFeedback = "잡기 성공";
                catchFeedbackUntil = Time.time + 0.35f;
                onCatchSucceeded?.Invoke();
                break;
            case CatchResult.Missed:
                catchFeedback = "빗나감";
                catchFeedbackUntil = Time.time + 0.35f;
                onCatchMissed?.Invoke();
                break;
            default:
                catchFeedback = "";
                catchFeedbackUntil = 0f;
                onCatchCancelled?.Invoke();
                break;
        }
    }

    private void OnGUI()
    {
        if (Object == null || !Object.IsValid || !Object.HasInputAuthority) return;
        if (roundManager == null) roundManager = FindFirstObjectByType<PrototypeRoundManager>();
        if (roundManager == null || roundManager.Object == null ||
            !roundManager.Object.IsValid || roundManager.RoundNumber == 0) return;
        GUI.Box(new Rect(20, Screen.height - 70, 360, 45),
            IsSeeker ? (CanMove ? "술래 | 우클릭: 손 휘둘러 잡기 | 좌클릭: 등반" : "술래 | 준비 중") : "도망자 | 포탈이 열리면 탈출하세요");
        if (IsSeeker && CanMove)
            GUI.Label(new Rect(Screen.width / 2f - 60, Screen.height / 2f + 20, 160, 30),
                Time.time < catchFeedbackUntil ? catchFeedback : "");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcResetMovement(Vector3 position, Quaternion rotation)
    {
        GetComponent<Un>()?.ResetForNextRound(position, rotation);
    }
    public GameObject globalVolume;
    public GameObject wbVolume;

    //음
    //player가 저장할 정보 : 죽었는지 탈출했는지> 몇번째로 탈출했는가?도 저장해야하나, 왕관, 게임안에 있는지(서버전달용) 
    [Networked]
    public int Crowns { get; private set; } 

    //추가, 이름 저장용도
    [Networked]
    public NetworkString<_32> DisplayName { get; private set; }
    public int SortNum;

    [Networked]
    public NetworkBool IsDead { get; private set; }

    [Networked]
    public NetworkBool IsDowned { get; private set; }

    [Networked]
    public NetworkBool HasEscaped { get; private set; }

    public bool IsInPlayground => !IsDead && !HasEscaped;


    public override void Spawned()
    {
        //if (!Object.HasStateAuthority)
        //    return;

        //헷갈려
        if (Object.HasStateAuthority)
        {
            DisplayName = $"Player {Object.InputAuthority.PlayerId}";
        }
        
        
        SortNum = Object.InputAuthority.PlayerId - 1;
        if (Object.HasInputAuthority && CameraManager.Instance)
        {
            CameraManager.Instance.state = this;
            VolumeManager.instance.player = this;
        }
        //추가했습니다 rpc < chatgpt..
        if (Object.HasInputAuthority && !string.IsNullOrWhiteSpace(PrototypeLobbyBootstrap.LocalPlayerName))
            RpcSetDisplayName(PrototypeLobbyBootstrap.LocalPlayerName);
    }

    //추가 
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcSetDisplayName(string playerName)
    {
        //playerName 참고.
        string trimmedName = string.IsNullOrWhiteSpace(playerName)
            ? $"Player {Object.InputAuthority.PlayerId}"
            : playerName.Trim();

        //이름 길이가 16이하이도록 함.
        DisplayName = trimmedName.Length <= 16 ? trimmedName : trimmedName.Substring(0, 16);
    }

    private bool _wasVisible = true;

    public override void Render()
    {
        bool shouldBeVisible = IsInPlayground;
        
        if (shouldBeVisible != _wasVisible)
        {
            SetVisibility(shouldBeVisible);
            _wasVisible = shouldBeVisible;
        }
    }

    private void SetVisibility(bool isVisible)
    {
        bool isLocal = Object.HasInputAuthority;

        // 모델 등 모든 렌더러 활성/비활성화
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            // 로컬 플레이어(자신)는 1인칭이므로 모델을 켜지 않음
            if (isLocal) renderer.enabled = false;
            else renderer.enabled = isVisible;
        }
        
        // 닉네임 태그 등 UI 활성/비활성화
        foreach (var canvas in GetComponentsInChildren<Canvas>(true))
        {
            // 자신의 닉네임 태그도 1인칭 화면에서는 보이지 않도록 함
            if (isLocal) canvas.enabled = false;
            else canvas.enabled = isVisible;
        }
        
        // 투명 상태일 때 충돌 방지를 위해 CharacterController도 비활성화
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = isVisible;
    }

    public void MarkDowned()
    {
        if (!Object.HasStateAuthority || IsSeeker || !IsInPlayground || IsDowned)
            return;

        IsDowned = true;
        GetComponent<Un>()?.ResetForNextRound();
    }

    public void Revive()
    {
        if (!Object.HasStateAuthority || !IsDowned || !IsInPlayground)
            return;

        // 스태미나를 먼저 복구해야 바로 다시 쓰러지지 않는다.
        if (GetComponent<PlayerCondition>().CurrentMaxStamina > 0f)
            IsDowned = false;
    }

    public void MarkDead() //죽은거 마킹
    {
        if (!Object.HasStateAuthority || IsSeeker || HasEscaped)
            return;

        IsDead = true;
        IsDowned = false;
        RenderSettings.fog = false;
    }

    public void MarkEscaped() // 탈출한거 마킹
    {
        if (!Object.HasStateAuthority || IsSeeker || IsDead || IsDowned)
            return;

        HasEscaped = true;
        RenderSettings.fog = false;
    }

    public void AddCrown() //왕관추가
    {
        if (!Object.HasStateAuthority)
            return;

        Crowns++;
    }

    public void ResetForNewMatch()
    {
        if (!Object.HasStateAuthority)
            return;

        Crowns = 0;
        IsSeeker = false;
        ResetCatch();
        IsDead = false;
        IsDowned = false;
        HasEscaped = false;
    }
    public void ResetRoundResult()
    {
        if (!Object.HasStateAuthority)
            return;

        IsDead = false;
        IsDowned = false;
        HasEscaped = false;
        ResetCatch();
    }

}
