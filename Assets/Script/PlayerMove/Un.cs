using Cinemachine;
using Fusion;
using Script.item;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerCondition))]
public class Un : NetworkBehaviour, RawInput.IPlayerActions
{
    public enum StateMachine
    {
        Floor,
        Wall,
        Ragdoll
    }

    [Header("Config")]
    [SerializeField] private ClimbingConfig config;

    [Header("State (Read Only)")]
    [SerializeField] private StateMachine stateMachine = StateMachine.Floor;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private CinemachineVirtualCamera cam;
    [SerializeField] private Transform headTransform;
    [SerializeField] private Transform camPivot;
    [SerializeField] private Vector3 cameraEyeOffset = new Vector3(0f, 0.1f, 0f);
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private float wallYawLimit = 70f;

    [Header("Scroll / Zoom Settings")]
    [SerializeField] private float scrollSensitivity = 0.01f;
    [SerializeField] private float minDistance = -10f;
    [SerializeField] private float maxDistance = 0f;

    [Header("Items & UI")]
    public PlayerInventory _playerInventory;
    [SerializeField] private Canvas ChattingCanvas;
    [SerializeField] private float WaterGunDistance = 5.0f;

    [Networked] private StateMachine NetState { get; set; }
    [Networked] private NetworkBool JumpWasPressed { get; set; }
    [Networked] private NetworkBool UseItemWasPressed { get; set; }
    [Networked] private NetworkBool DropWasPressed { get; set; }
    [Networked] private NetworkBool ThrowWasPressed { get; set; }
    [Networked] private TickTimer ItemUseCooldown { get; set; }
    [Networked] private int RagdollSequence { get; set; }
    [Networked] private Vector3 RagdollInitialVelocity { get; set; }
    [Networked] private Vector3 NetSurfaceNormal { get; set; }

    public StateMachine statemachine => (Object != null && Object.IsValid) ? NetState : stateMachine;
    public PrototypeItemInput ItemInput => itemInput;
    public bool OpenEyesHeld => itemInput != null && itemInput.OpenEyes.IsPressed();
    public bool IsGrounded => _cc != null && _cc.enabled && _cc.isGrounded;
    public bool IsClimbing => statemachine == StateMachine.Wall;
    public bool IsRagdoll => statemachine == StateMachine.Ragdoll;
    public bool IsLunging => _lungeRemaining > 0f;

    private CharacterController _cc;
    private NetworkTransform _networkTransform;
    private PlayerCondition _condition;
    private PlayerGameState _gameState;
    private PlayerRagdoll _ragdoll;
    private FallDamage _fallDamage;
    private ClimbingConfig _runtimeFallbackConfig;

    private RawInput _rawInput;
    private RawInput.PlayerActions _playerActions;
    private InputAction _sprintAction;
    private PrototypeItemInput itemInput;

    private Vector2 _moveInput;
    private Vector2 _mouseDelta;
    private bool _jumpPressed;
    private bool _attackPressed;

    private float _pitch;
    private float _yaw;
    private float _currentCamDistance;

    private Vector3 _surfaceNormal = Vector3.up;
    private float _verticalVelocity;
    private float _regrabCooldownTimer;
    private float _recoverLockTimer;

    private bool _trackingWallReleaseFall;
    private float _wallReleaseStartY;

    private float _ragdollElapsed;
    private float _ragdollSettleTimer;
    private float _lungeRemaining;
    private float _lungeCooldownTimer;
    private bool _ragdollImpactReported;
    private Vector3 _fallbackRagdollVelocity;
    private Vector3 _latestAimDirection = Vector3.forward;

    private bool _localRagdollApplied;
    private int _appliedRagdollSequence;

    private readonly RaycastHit[] _raycastScratch = new RaycastHit[16];
    private readonly Collider[] _overlapScratch = new Collider[16];

    private FallDamage ResolveFallDamage()
    {
        if (_fallDamage == null)
            _fallDamage = GetComponent<FallDamage>();
        return _fallDamage;
    }

    private ClimbingConfig GetConfig()
    {
        if (config != null)
            return config;

        config = Resources.Load<ClimbingConfig>("ClimbingConfig");
        if (config != null)
            return config;

        if (_runtimeFallbackConfig == null)
            _runtimeFallbackConfig = ScriptableObject.CreateInstance<ClimbingConfig>();

        return _runtimeFallbackConfig;
    }

    public override void Spawned()
    {
        _cc = GetComponent<CharacterController>();
        _networkTransform = GetComponent<NetworkTransform>();
        _condition = GetComponent<PlayerCondition>();
        _gameState = GetComponent<PlayerGameState>();
        _ragdoll = GetComponent<PlayerRagdoll>();
        _fallDamage = GetComponent<FallDamage>();

        if (_playerInventory == null)
            _playerInventory = GetComponent<PlayerInventory>();

        ResolveAnimatorAndHead();

        if (Object.HasInputAuthority)
        {
            _rawInput = new RawInput();
            _playerActions = _rawInput.Player;
            _playerActions.Enable();
            _playerActions.AddCallbacks(this);

            _sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            _sprintAction.AddBinding("<Gamepad>/leftStickPress");
            _sprintAction.Enable();

            itemInput = new PrototypeItemInput();

            if (GetComponent<SeekerVisionMask>() == null)
                gameObject.AddComponent<SeekerVisionMask>();

            if (cam == null)
            {
                cam = GetComponentInChildren<CinemachineVirtualCamera>();
                if (cam == null)
                    Debug.LogError("카메라 설정 오류 발생 바로 확인 바람");
            }

            if (camPivot == null || (cam != null && camPivot == cam.transform))
            {
                GameObject pivotGo = new GameObject("CameraPivot");
                camPivot = pivotGo.transform;
            }

            _yaw = transform.eulerAngles.y;
            camPivot.position = GetCameraPivotPosition();
            camPivot.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            if (cam != null)
            {
                cam.Follow = null;
                cam.LookAt = null;
                cam.transform.SetParent(camPivot, false);
                cam.transform.localScale = Vector3.one;
                _currentCamDistance = Mathf.Clamp(cam.transform.localPosition.z, minDistance, maxDistance);
                cam.transform.localPosition = new Vector3(0f, 0f, _currentCamDistance);
                cam.transform.localRotation = Quaternion.identity;
            }
        }
        else if (cam != null)
        {
            cam.gameObject.SetActive(false);
        }

        _yaw = transform.eulerAngles.y;
        _surfaceNormal = Vector3.up;
        stateMachine = StateMachine.Floor;

        if (Object.HasStateAuthority)
        {
            NetState = StateMachine.Floor;
            NetSurfaceNormal = Vector3.up;
        }

        _ragdoll?.SetRagdollEnabled(false);
        _localRagdollApplied = false;
        _appliedRagdollSequence = RagdollSequence;
        if (_cc != null)
            _cc.enabled = _gameState == null || _gameState.IsInPlayground;
    }

    private void ResolveAnimatorAndHead()
    {
        if (animator == null || !animator.gameObject.activeSelf)
        {
            Animator[] animators = GetComponentsInChildren<Animator>(true);
            foreach (Animator candidate in animators)
            {
                if (candidate == null) continue;
                if (candidate.gameObject.activeSelf)
                {
                    animator = candidate;
                    break;
                }
            }

            if (animator == null && animators.Length > 0)
                animator = animators[0];
        }

        if (headTransform == null && _ragdoll != null && _ragdoll.HeadBone != null)
            headTransform = _ragdoll.HeadBone;

        if (headTransform == null && animator != null && animator.isHuman)
            headTransform = animator.GetBoneTransform(HumanBodyBones.Head);
    }

    private void OnDestroy()
    {
        _rawInput?.Dispose();
        _sprintAction?.Dispose();
        itemInput?.Dispose();

        if (camPivot != null && camPivot != transform && Object != null && Object.HasInputAuthority)
            Destroy(camPivot.gameObject);

        if (_runtimeFallbackConfig != null)
            Destroy(_runtimeFallbackConfig);
    }

    private void OnEnable()
    {
        if (_rawInput != null)
            _playerActions.Enable();
        _sprintAction?.Enable();
    }

    private void OnDisable()
    {
        if (_rawInput != null)
            _playerActions.Disable();
        _sprintAction?.Disable();
        itemInput?.SetRole(false, true);

        _moveInput = Vector2.zero;
        _jumpPressed = false;
        _attackPressed = false;
    }

    public NetworkInputData GetNetworkInput()
    {
        // Send orientation even while menus suppress actions; a zero yaw would snap remote players.
        NetworkInputData data = new NetworkInputData
        {
            BodyYaw = _yaw,
            AimDirection = camPivot != null ? camPivot.forward : transform.forward,
            SelectedSlot = -1
        };

        bool isSeeker = _gameState != null && _gameState.IsSeeker;
        bool blocked = !isActiveAndEnabled || Cursor.visible || PrototypeSettingsPanel.IsOpen;
        itemInput?.SetRole(isSeeker, blocked);
        if (blocked) return data;

        int selectedSlot = (_playerInventory != null && !isSeeker)
            ? _playerInventory.SelectedSlot
            : -1;

        data.Move = _moveInput;
        data.Look = _mouseDelta;
        data.BodyYaw = _yaw;
        data.AimDirection = camPivot != null ? camPivot.forward : transform.forward;
        data.SelectedSlot = selectedSlot;

        data.Buttons.Set((int)PlayerButtons.OpenEyes, OpenEyesHeld);
        data.Buttons.Set((int)PlayerButtons.Revive, itemInput != null && itemInput.Revive.IsPressed());
        data.Buttons.Set((int)PlayerButtons.Catch, itemInput != null && itemInput.Catch.IsPressed());
        data.Buttons.Set((int)PlayerButtons.DropItem, itemInput != null && itemInput.Drop.IsPressed());
        data.Buttons.Set((int)PlayerButtons.ThrowItem, itemInput != null && itemInput.Throw.IsPressed());
        data.Buttons.Set((int)PlayerButtons.Jump, _jumpPressed);
        data.Buttons.Set((int)PlayerButtons.Attack, _attackPressed);
        data.Buttons.Set((int)PlayerButtons.UseItem, !isSeeker && selectedSlot >= 0 && itemInput != null && itemInput.Use.IsPressed());
        data.Buttons.Set((int)PlayerButtons.Sprint, _sprintAction != null && _sprintAction.IsPressed());

        return data;
    }

    public void TeleportCharacter(Vector3 position, Quaternion rotation)
    {
        bool shouldEnableCc = _cc != null && !IsRagdoll && (_gameState == null || _gameState.IsInPlayground);
        if (_cc != null)
            _cc.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (_networkTransform != null && Object != null && Object.HasStateAuthority)
            _networkTransform.Teleport(position, rotation);

        Physics.SyncTransforms();

        if (_cc != null)
            _cc.enabled = shouldEnableCc;
    }

    public void ResetForNextRound(Vector3 position, Quaternion rotation)
    {
        ResetLocalMovementFields(rotation.eulerAngles.y);
        _playerInventory?.SyncClear();

        if (Object != null && Object.HasStateAuthority)
        {
            NetState = StateMachine.Floor;
            NetSurfaceNormal = Vector3.up;
            JumpWasPressed = false;
            UseItemWasPressed = false;
            DropWasPressed = ThrowWasPressed = false;
            ItemUseCooldown = TickTimer.None;
            RagdollInitialVelocity = Vector3.zero;
        }

        _ragdoll?.SetRagdollEnabled(false);
        _localRagdollApplied = false;
        _appliedRagdollSequence = Object != null && Object.IsValid ? RagdollSequence : 0;

        TeleportCharacter(position, rotation);
        ResetAnimatorBools();
    }

    public void ResetForNextRound()
    {
        if (Object != null && Object.HasStateAuthority)
            RpcResetMovement();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcResetMovement()
    {
        ResetLocalMovementFields(transform.eulerAngles.y);
        _playerInventory?.SyncClear();

        if (Object != null && Object.HasStateAuthority)
        {
            NetState = StateMachine.Floor;
            NetSurfaceNormal = Vector3.up;
            JumpWasPressed = false;
            UseItemWasPressed = false;
            DropWasPressed = ThrowWasPressed = false;
            ItemUseCooldown = TickTimer.None;
            RagdollInitialVelocity = Vector3.zero;
        }

        _ragdoll?.SetRagdollEnabled(false);
        _localRagdollApplied = false;
        _appliedRagdollSequence = Object != null && Object.IsValid ? RagdollSequence : 0;

        if (_cc != null)
            _cc.enabled = _gameState == null || _gameState.IsInPlayground;

        ResetAnimatorBools();
    }

    private void ResetLocalMovementFields(float yawDegrees)
    {
        stateMachine = StateMachine.Floor;
        _verticalVelocity = 0f;
        _regrabCooldownTimer = 0f;
        _recoverLockTimer = 0f;
        _trackingWallReleaseFall = false;
        _ragdollElapsed = 0f;
        _ragdollSettleTimer = 0f;
        _lungeRemaining = 0f;
        _lungeCooldownTimer = 0f;
        _ragdollImpactReported = false;
        _fallbackRagdollVelocity = Vector3.zero;
        _surfaceNormal = Vector3.up;
        _yaw = yawDegrees;
        _pitch = 0f;
    }

    public override void FixedUpdateNetwork()
    {
        if (Object == null || !Object.IsValid)
            return;

        if (!Object.HasStateAuthority)
            return;

        float dt = Runner.DeltaTime;
        _regrabCooldownTimer = Mathf.Max(0f, _regrabCooldownTimer - dt);
        _recoverLockTimer = Mathf.Max(0f, _recoverLockTimer - dt);
        _lungeCooldownTimer = Mathf.Max(0f, _lungeCooldownTimer - dt);

        if (_gameState == null || !_gameState.CanMove)
        {
            if (NetState == StateMachine.Wall)
            {
                ExitWallToFloor(trackFallForRagdoll: false);
            }
            else if (NetState == StateMachine.Ragdoll && (_gameState == null || _gameState.IsDowned || !_gameState.IsInPlayground))
            {
                ForceExitRagdollAtCurrentPosition();
            }

            if (_cc != null && _cc.enabled && NetState == StateMachine.Floor)
                _cc.Move(Vector3.zero);

            ResetAnimatorBools();
            return;
        }

        if (!GetInput(out NetworkInputData data))
            return;

        if (!Object.HasInputAuthority && !float.IsNaN(data.BodyYaw) && !float.IsInfinity(data.BodyYaw))
            _yaw = data.BodyYaw;

        float aimMagnitude = data.AimDirection.sqrMagnitude;
        if (aimMagnitude > 0.001f && !float.IsNaN(aimMagnitude) && !float.IsInfinity(aimMagnitude))
        {
            _latestAimDirection = data.AimDirection.normalized;
        }
        else
        {
            _latestAimDirection = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
        }

        if (NetState != StateMachine.Ragdoll && _cc != null && _cc.enabled)
            Physics.SyncTransforms();

        switch (NetState)
        {
            case StateMachine.Floor:
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

                FloorMove(data, dt);

                if (NetState == StateMachine.Floor &&
                    _recoverLockTimer <= 0f &&
                    _regrabCooldownTimer <= 0f &&
                    data.Buttons.IsSet(PlayerButtons.Attack) &&
                    _condition.CurrentStamina > 0f)
                {
                    TryGrabWall(data);
                }
                break;

            case StateMachine.Wall:
                WallClimb(data, dt);
                break;

            case StateMachine.Ragdoll:
                UpdateRagdoll(dt);
                break;

            default:
                NetState = StateMachine.Floor;
                break;
        }

        bool useItemPressed = data.Buttons.IsSet(PlayerButtons.UseItem);
        bool dropPressed = data.Buttons.IsSet(PlayerButtons.DropItem);
        bool throwPressed = data.Buttons.IsSet(PlayerButtons.ThrowItem);
        bool released = false;
        if (_playerInventory != null)
        {
            if (throwPressed && !ThrowWasPressed)
                released = _playerInventory.ReleaseItem(data.SelectedSlot, data.AimDirection, true);
            else if (dropPressed && !DropWasPressed)
                released = _playerInventory.ReleaseItem(data.SelectedSlot, data.AimDirection, false);
        }
        if (!released && (_gameState == null || !_gameState.IsSeeker) &&
            useItemPressed && !UseItemWasPressed && ItemUseCooldown.ExpiredOrNotRunning(Runner) &&
            data.SelectedSlot >= 0)
        {
            UseItem(data.SelectedSlot);
            ItemUseCooldown = TickTimer.CreateFromSeconds(Runner, 0.25f);
        }

        JumpWasPressed = data.Buttons.IsSet(PlayerButtons.Jump);
        UseItemWasPressed = useItemPressed;
        DropWasPressed = dropPressed;
        ThrowWasPressed = throwPressed;
        stateMachine = NetState;
    }

    public override void Render()
    {
        if (Object == null || !Object.IsValid)
            return;

        stateMachine = NetState;
        if (!Object.HasStateAuthority && NetSurfaceNormal.sqrMagnitude > 0.0001f)
            _surfaceNormal = NetSurfaceNormal.normalized;

        SyncLocalRagdollPresentation();
    }

    private void Update()
    {
        if (Object == null || !Object.HasInputAuthority || _playerInventory == null)
            return;

        bool blocked = Cursor.visible || PrototypeSettingsPanel.IsOpen;
        itemInput?.SetRole(_gameState != null && _gameState.IsSeeker, blocked);
        if (blocked || (_gameState != null && !_gameState.CanMove)) return;

        if (_gameState != null && _gameState.IsSeeker)
        {
            if (_playerInventory.SelectedSlot != -1)
                _playerInventory.SwitchInventory(-1);
            return;
        }

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            _playerInventory.ToggleSlot(0);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            _playerInventory.ToggleSlot(1);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            _playerInventory.ToggleSlot(2);
    }

    private void LateUpdate()
    {
        if (Object == null || !Object.HasInputAuthority || camPivot == null)
            return;

        if (statemachine == StateMachine.Wall)
        {
            float wallYaw = GetWallFacingYaw(_surfaceNormal);
            float rel = Mathf.Clamp(Mathf.DeltaAngle(wallYaw, _yaw), -wallYawLimit, wallYawLimit);
            _yaw = wallYaw + rel;
        }

        camPivot.position = GetCameraPivotPosition();
        camPivot.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    public Vector3 GetCameraPivotPosition()
    {
        if (IsRagdoll && _ragdoll != null && _ragdoll.HeadBone != null)
            return _ragdoll.HeadBone.position + Vector3.up * cameraEyeOffset.y;

        if (headTransform != null)
            return headTransform.position + transform.TransformDirection(cameraEyeOffset);

        return transform.position + Vector3.up * 1.5f;
    }

    private void FloorMove(NetworkInputData data, float dt)
    {
        ClimbingConfig cfg = GetConfig();

        if (_cc != null && !_cc.enabled && (_gameState == null || _gameState.IsInPlayground))
            _cc.enabled = true;

        bool wasGrounded = _cc != null && _cc.isGrounded;
        bool jumped = false;

        if (wasGrounded)
        {
            _trackingWallReleaseFall = false;

            if (_recoverLockTimer <= 0f &&
                data.Buttons.IsSet(PlayerButtons.Jump) &&
                !JumpWasPressed &&
                SpendStamina(cfg.jumpStaminaCost))
            {
                _verticalVelocity = cfg.jumpSpeed;
                jumped = true;
                SetAnimBool("Jump", true);
            }
            else if (_verticalVelocity < 0f)
            {
                _verticalVelocity = cfg.groundedStickVelocity;
                SetAnimBool("Jump", false);
            }
        }
        else
        {
            _verticalVelocity -= cfg.gravity * dt;
            SetAnimBool("Jump", true);
        }

        Quaternion yawRotation = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 forward = yawRotation * Vector3.forward;
        Vector3 right = yawRotation * Vector3.right;

        Vector2 rawMove = _recoverLockTimer > 0f ? Vector2.zero : data.Move;
        Vector3 moveDirection = right * rawMove.x + forward * rawMove.y;
        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        bool tryingToSprint = _recoverLockTimer <= 0f &&
            data.Buttons.IsSet(PlayerButtons.Sprint) &&
            rawMove.sqrMagnitude > 0.01f;

        bool sprinting = tryingToSprint &&
            _condition.CanSprint &&
            SpendStamina(cfg.sprintStaminaDrain * dt);

        Vector3 horizontalMove = moveDirection * (sprinting ? cfg.runSpeed : cfg.walkSpeed);

        if (wasGrounded &&
            !jumped &&
            !tryingToSprint &&
            !data.Buttons.IsSet(PlayerButtons.Attack) &&
            !(_gameState != null && _gameState.IsSeeker && data.Buttons.IsSet(PlayerButtons.OpenEyes)))
        {
            _condition.RecoverStamina(cfg.groundStaminaRecoverRate * dt);
        }

        SetAnimBool("Climbing", false);
        if (horizontalMove.sqrMagnitude > 0.001f)
        {
            SetAnimBool("Walking", true);
            SetAnimBool("Running", sprinting);
        }
        else
        {
            SetAnimBool("Walking", false);
            SetAnimBool("Running", false);
        }

        Vector3 velocity = horizontalMove;
        velocity.y = _verticalVelocity;

        if (_cc != null && _cc.enabled)
            _cc.Move(velocity * dt);

        bool groundedNow = _cc != null && _cc.isGrounded;
        if (groundedNow)
        {
            _trackingWallReleaseFall = false;
        }
        else if (_trackingWallReleaseFall &&
            (_wallReleaseStartY - transform.position.y) >= cfg.manualReleaseRagdollFallDistance)
        {
            // 방안 B: 직접 손을 놓은 뒤 착지 전 일정 높이 이상 낙하하면 공중에서 래그돌 전환
            Vector3 fallVelocity = horizontalMove;
            fallVelocity.y = _verticalVelocity;
            EnterRagdoll(fallVelocity);
        }
    }

    private void TryGrabWall(NetworkInputData data)
    {
        ClimbingConfig cfg = GetConfig();
        Vector3 aimDir = data.AimDirection.sqrMagnitude > 0.01f
            ? data.AimDirection.normalized
            : (Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward);
        Vector3 bodyForward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;

        Vector3 chestOrigin = transform.position + Vector3.up * cfg.grabRayOriginHeight;
        Vector3 waistOrigin = transform.position + Vector3.up * cfg.wallProbeHeight;

        RaycastHit hit;
        bool found = TryCastClimbRaycast(chestOrigin, aimDir, cfg.grabRayDistance, cfg.climbableLayers, out hit) &&
            cfg.IsClimbableSlope(hit.normal);

        if (!found)
        {
            found = TryCastClimbRaycast(chestOrigin, bodyForward, cfg.grabRayDistance, cfg.climbableLayers, out hit) &&
                cfg.IsClimbableSlope(hit.normal);
        }

        if (!found)
        {
            found = TryCastClimbRaycast(waistOrigin, bodyForward, cfg.grabRayDistance, cfg.climbableLayers, out hit) &&
                cfg.IsClimbableSlope(hit.normal);
        }

        if (!found)
        {
            found = TryCastClimbSphere(chestOrigin, aimDir, cfg.grabSphereRadius, cfg.grabRayDistance, cfg.climbableLayers, out hit) &&
                cfg.IsClimbableSlope(hit.normal);
        }

        if (!found)
            return;

        EnterWall(hit, cfg);
    }

    private void EnterWall(RaycastHit hit, ClimbingConfig cfg)
    {
        _surfaceNormal = hit.normal.normalized;
        NetSurfaceNormal = _surfaceNormal;
        NetState = StateMachine.Wall;
        stateMachine = StateMachine.Wall;

        _verticalVelocity = 0f;
        _lungeRemaining = 0f;
        _trackingWallReleaseFall = false;
        ResolveFallDamage()?.ResetFallTrackingFromCurrentHeight();

        if (_cc != null && _cc.enabled)
        {
            Vector3 currentProbePos = transform.position + Vector3.up * cfg.wallProbeHeight;
            float distFromPlane = Vector3.Dot(currentProbePos - hit.point, _surfaceNormal);
            Vector3 snapDelta = _surfaceNormal * (cfg.wallStandOffDistance - distFromPlane);
            _cc.Move(snapDelta);
        }

        Vector3 flatIntoWall = Vector3.ProjectOnPlane(-_surfaceNormal, Vector3.up);
        if (flatIntoWall.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(flatIntoWall.normalized, Vector3.up);

        SetAnimBool("Climbing", true);
        SetAnimBool("Walking", false);
        SetAnimBool("Running", false);
        SetAnimBool("Jump", false);
    }

    private void WallClimb(NetworkInputData data, float dt)
    {
        ClimbingConfig cfg = GetConfig();

        // 1. 좌클릭 해제 시 손 놓기 (방안 B 낙하 추적 시작)
        if (!data.Buttons.IsSet(PlayerButtons.Attack))
        {
            _lungeRemaining = 0f;
            ExitWallToFloor(trackFallForRagdoll: true);
            return;
        }

        // 2. 등반 중 Space 단발 입력으로 벽 밀착 위쪽 도약(런지) 발동 (스태미나 > 0이면 1회 허용)
        bool jumpJustPressed = data.Buttons.IsSet(PlayerButtons.Jump) && !JumpWasPressed;
        if (_lungeRemaining <= 0f &&
            _lungeCooldownTimer <= 0f &&
            jumpJustPressed &&
            _condition != null &&
            _condition.CurrentStamina > 0f &&
            SpendStamina(cfg.lungeStaminaCost))
        {
            _lungeRemaining = Mathf.Max(0.05f, cfg.lungeDuration);
            _lungeCooldownTimer = cfg.lungeCooldown;
        }

        bool isLunging = _lungeRemaining > 0f;

        // 3. 일반 등반 스태미나 소모 (도약 중에는 유예해 꼭대기 맨틀 또는 도약 종료 후 추락 보장)
        if (!isLunging)
        {
            float drain = cfg.climbStaminaDrain * dt;
            bool spent = drain <= 0f || SpendStamina(drain);
            if (!spent || (cfg.climbStaminaDrain > 0f && _condition.CurrentStamina <= 0f))
            {
                EnterRagdoll(_surfaceNormal * cfg.staminaDepletedPushOffSpeed);
                return;
            }
        }

        SetAnimBool("Climbing", true);
        SetAnimBool("Walking", false);
        SetAnimBool("Running", false);
        SetAnimBool("Jump", false);

        Vector2 rawMove = Vector2.ClampMagnitude(data.Move, 1f);
        ComputeWallBasis(_surfaceNormal, out Vector3 wallUp, out Vector3 wallRight);
        Vector3 desiredTangentDir = isLunging
            ? wallUp
            : (wallUp * rawMove.y + wallRight * rawMove.x);

        // 4. 벽 접촉 확인 및 굽은 벽/코너 법선 추적
        Vector3 probeOrigin = transform.position + Vector3.up * cfg.wallProbeHeight;
        bool hasWall = ProbeClimbSurface(probeOrigin, desiredTangentDir, cfg, out RaycastHit wallHit);

        if (!hasWall)
        {
            if ((isLunging || rawMove.y > 0.1f) && cfg.enableAutoMantle && TryMantleLedge(_surfaceNormal, cfg, out Vector3 mantlePos))
            {
                TeleportCharacter(mantlePos, transform.rotation);
                ExitWallToFloor(trackFallForRagdoll: false);
                return;
            }

            ExitWallToFloor(trackFallForRagdoll: true);
            return;
        }

        // 벽 상단 끝을 오르는 중이면 자동 맨틀 체크
        if ((isLunging || rawMove.y > 0.1f) && cfg.enableAutoMantle)
        {
            Vector3 upperProbe = transform.position + Vector3.up * (cfg.wallProbeHeight + 0.55f);
            bool hasUpperWall = TryCastClimbRaycast(upperProbe, -_surfaceNormal, cfg.wallKeepContactDistance, cfg.climbableLayers, out _);
            if (!hasUpperWall && TryMantleLedge(_surfaceNormal, cfg, out Vector3 mantlePos))
            {
                TeleportCharacter(mantlePos, transform.rotation);
                ExitWallToFloor(trackFallForRagdoll: false);
                return;
            }
        }

        if (!cfg.IsClimbableSlope(wallHit.normal))
        {
            ExitWallToFloor(trackFallForRagdoll: false);
            return;
        }

        float blend = 1f - Mathf.Exp(-cfg.wallNormalSmoothSpeed * dt);
        _surfaceNormal = Vector3.Slerp(_surfaceNormal, wallHit.normal.normalized, blend).normalized;
        NetSurfaceNormal = _surfaceNormal;

        // 5. 등반 / 벽 밀착 도약 이동 및 벽 밀착 유지
        ComputeWallBasis(_surfaceNormal, out wallUp, out wallRight);
        Vector3 tangentMove;
        if (isLunging)
        {
            float duration = Mathf.Max(0.05f, cfg.lungeDuration);
            float t = Mathf.Clamp01(_lungeRemaining / duration);
            float lungeSpeed = Mathf.Lerp(cfg.climbSpeed, Mathf.Max(cfg.climbSpeed, cfg.lungeUpwardImpulse), t);
            tangentMove = wallUp * lungeSpeed;
            _lungeRemaining = Mathf.Max(0f, _lungeRemaining - dt);
        }
        else
        {
            tangentMove = (wallUp * rawMove.y + wallRight * rawMove.x) * cfg.climbSpeed;
        }

        float currentDist = Vector3.Dot(probeOrigin - wallHit.point, wallHit.normal);
        Vector3 pullMove = wallHit.normal * ((cfg.wallStandOffDistance - currentDist) * cfg.wallPullStrength);

        if (_cc != null && _cc.enabled)
            _cc.Move((tangentMove + pullMove) * dt);

        if (!isLunging && rawMove.y < -0.1f && _cc != null && _cc.enabled && _cc.isGrounded)
        {
            ExitWallToFloor(trackFallForRagdoll: false);
            return;
        }

        // 5. 벽 정면을 바라보도록 몸체 수평 회전
        Vector3 flatIntoWall = Vector3.ProjectOnPlane(-_surfaceNormal, Vector3.up);
        if (flatIntoWall.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(flatIntoWall.normalized, Vector3.up);
            float rotBlend = 1f - Mathf.Exp(-cfg.wallBodyTurnSpeed * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotBlend);
        }
    }

    private bool ProbeClimbSurface(
        Vector3 probeOrigin,
        Vector3 desiredTangentDir,
        ClimbingConfig cfg,
        out RaycastHit wallHit)
    {
        if (TryCastClimbRaycast(probeOrigin, -_surfaceNormal, cfg.wallKeepContactDistance, cfg.climbableLayers, out wallHit))
            return true;

        if (desiredTangentDir.sqrMagnitude > 0.001f)
        {
            Vector3 tangentNorm = desiredTangentDir.normalized;
            Vector3 curvedDir = (-_surfaceNormal + tangentNorm * 0.45f).normalized;
            if (TryCastClimbRaycast(probeOrigin + tangentNorm * 0.15f, curvedDir, cfg.wallKeepContactDistance * 1.15f, cfg.climbableLayers, out wallHit))
                return true;
        }

        Vector3 lowerOrigin = transform.position + Vector3.up * (cfg.wallProbeHeight * 0.45f);
        if (TryCastClimbRaycast(lowerOrigin, -_surfaceNormal, cfg.wallKeepContactDistance, cfg.climbableLayers, out wallHit))
            return true;

        Vector3 highOrigin = transform.position + Vector3.up * cfg.grabRayOriginHeight;
        if (TryCastClimbRaycast(highOrigin, -_surfaceNormal, cfg.wallKeepContactDistance, cfg.climbableLayers, out wallHit))
            return true;

        return false;
    }

    private bool TryMantleLedge(Vector3 wallNormal, ClimbingConfig cfg, out Vector3 mantlePosition)
    {
        mantlePosition = transform.position;

        Vector3 intoWall = Vector3.ProjectOnPlane(-wallNormal, Vector3.up);
        if (intoWall.sqrMagnitude < 0.001f)
            intoWall = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

        if (intoWall.sqrMagnitude < 0.001f)
            return false;

        intoWall.Normalize();
        Vector3 downOrigin = transform.position + intoWall * cfg.mantleForwardOffset + Vector3.up * cfg.mantleRayUpOffset;

        if (!TryCastClimbRaycast(downOrigin, Vector3.down, cfg.mantleRayDownDistance, cfg.climbableLayers, out RaycastHit topHit))
            return false;

        if (Vector3.Angle(Vector3.up, topHit.normal) > cfg.mantleMaxSlopeAngle)
            return false;

        float heightDiff = topHit.point.y - transform.position.y;
        if (heightDiff < 0.1f || heightDiff > 1.85f)
            return false;

        Vector3 candidate = topHit.point + Vector3.up * 0.05f;
        if (!HasCapsuleClearance(candidate, cfg.climbableLayers))
            return false;

        mantlePosition = candidate;
        return true;
    }

    private bool HasCapsuleClearance(Vector3 footPosition, LayerMask mask)
    {
        float worldScale = Mathf.Abs(transform.lossyScale.y);
        float radius = (_cc != null ? _cc.radius : 0.5f) * Mathf.Max(Mathf.Abs(transform.lossyScale.x), 0.1f) * 0.85f;
        float height = (_cc != null ? _cc.height : 3.6f) * Mathf.Max(worldScale, 0.1f);
        float centerY = _cc != null ? _cc.center.y * worldScale : height * 0.5f;
        float halfSpan = Mathf.Max(0f, height * 0.5f - radius);

        Vector3 center = footPosition + Vector3.up * centerY;
        Vector3 bottom = center - Vector3.up * halfSpan;
        Vector3 top = center + Vector3.up * halfSpan;

        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, _overlapScratch, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapScratch[i];
            if (col == null || col.transform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }

    private void ExitWallToFloor(bool trackFallForRagdoll)
    {
        ClimbingConfig cfg = GetConfig();

        NetState = StateMachine.Floor;
        stateMachine = StateMachine.Floor;
        _surfaceNormal = Vector3.up;
        NetSurfaceNormal = Vector3.up;
        _lungeRemaining = 0f;
        _regrabCooldownTimer = cfg.climbRegrabCooldown;

        if (trackFallForRagdoll)
        {
            _trackingWallReleaseFall = true;
            _wallReleaseStartY = transform.position.y;
            _verticalVelocity = Mathf.Min(_verticalVelocity, -0.5f);
        }
        else
        {
            _trackingWallReleaseFall = false;
            _verticalVelocity = Mathf.Min(_verticalVelocity, 0f);
        }

        SetAnimBool("Climbing", false);
    }

    private void EnterRagdoll(Vector3 initialVelocity)
    {
        NetState = StateMachine.Ragdoll;
        stateMachine = StateMachine.Ragdoll;
        RagdollInitialVelocity = initialVelocity;
        RagdollSequence++;

        _surfaceNormal = Vector3.up;
        NetSurfaceNormal = Vector3.up;
        _lungeRemaining = 0f;
        _trackingWallReleaseFall = false;
        _ragdollElapsed = 0f;
        _ragdollSettleTimer = 0f;
        _ragdollImpactReported = false;
        _fallbackRagdollVelocity = initialVelocity;
        _verticalVelocity = 0f;
        ResolveFallDamage()?.OnEnterRagdoll();

        SetAnimBool("Climbing", false);
        SetAnimBool("Walking", false);
        SetAnimBool("Running", false);
        SetAnimBool("Jump", false);

        SyncLocalRagdollPresentation();
    }

    private void UpdateRagdoll(float dt)
    {
        ClimbingConfig cfg = GetConfig();
        _ragdollElapsed += dt;

        if (_ragdoll != null && _ragdoll.HasValidRig)
        {
            bool nearGround = _ragdoll.IsHipsNearGround(cfg.ragdollGroundLayers, cfg.ragdollGroundCheckDistance);
            if (nearGround && !_ragdollImpactReported)
            {
                _ragdollImpactReported = true;
                float impactY = _ragdoll.TryGetSafeStandUpPosition(
                    cfg.ragdollGroundLayers,
                    0.5f,
                    cfg.ragdollGroundCheckDistance + 0.35f,
                    out Vector3 impactPos)
                    ? impactPos.y
                    : _ragdoll.HipsWorldPosition.y;
                ResolveFallDamage()?.ReportRagdollImpact(impactY);
            }

            bool slowEnough = _ragdoll.HipsVelocity.sqrMagnitude <= cfg.ragdollSettleSpeed * cfg.ragdollSettleSpeed;

            if (_ragdollElapsed >= cfg.ragdollMinDuration && nearGround && slowEnough)
                _ragdollSettleTimer += dt;
            else
                _ragdollSettleTimer = 0f;

            if (_ragdollSettleTimer >= cfg.ragdollSettleHoldTime || _ragdollElapsed >= cfg.ragdollMaxDuration)
            {
                _ragdoll.TryGetSafeStandUpPosition(cfg.ragdollGroundLayers, 0.8f, 1.5f, out Vector3 standUpPos);
                ExitRagdollToFloor(standUpPos);
            }
        }
        else
        {
            // 프리팹 본 래그돌이 아직 구성되지 않은 환경에서의 폴백 낙하/회복
            if (_cc != null && !_cc.enabled && (_gameState == null || _gameState.IsInPlayground))
                _cc.enabled = true;

            _fallbackRagdollVelocity.y -= cfg.gravity * dt;
            if (_cc != null && _cc.enabled)
                _cc.Move(_fallbackRagdollVelocity * dt);

            bool grounded = _cc != null && _cc.isGrounded;
            if (grounded && !_ragdollImpactReported)
            {
                _ragdollImpactReported = true;
                ResolveFallDamage()?.ReportRagdollImpact(transform.position.y);
            }

            if (_ragdollElapsed >= cfg.ragdollMinDuration && grounded)
                _ragdollSettleTimer += dt;
            else
                _ragdollSettleTimer = 0f;

            if (_ragdollSettleTimer >= cfg.ragdollSettleHoldTime || _ragdollElapsed >= cfg.ragdollMaxDuration)
                ExitRagdollToFloor(transform.position);
        }
    }

    private void ExitRagdollToFloor(Vector3 standUpPosition)
    {
        ClimbingConfig cfg = GetConfig();

        if (!_ragdollImpactReported)
        {
            _ragdollImpactReported = true;
            ResolveFallDamage()?.ReportRagdollImpact(standUpPosition.y);
        }

        NetState = StateMachine.Floor;
        stateMachine = StateMachine.Floor;
        _surfaceNormal = Vector3.up;
        NetSurfaceNormal = Vector3.up;
        _lungeRemaining = 0f;
        _trackingWallReleaseFall = false;
        _verticalVelocity = cfg.groundedStickVelocity;
        _recoverLockTimer = cfg.standUpRecoverDelay;
        _regrabCooldownTimer = cfg.climbRegrabCooldown;

        if (_localRagdollApplied)
        {
            _ragdoll?.SetRagdollEnabled(false);
            _localRagdollApplied = false;
        }

        Quaternion standRotation = Quaternion.Euler(0f, _yaw, 0f);
        TeleportCharacter(standUpPosition, standRotation);
        ResolveFallDamage()?.OnExitRagdoll(standUpPosition.y);
        ResetAnimatorBools();
    }

    private void ForceExitRagdollAtCurrentPosition()
    {
        Vector3 standPos = (_ragdoll != null && _ragdoll.HasValidRig)
            ? _ragdoll.HipsWorldPosition
            : transform.position;
        ExitRagdollToFloor(standPos);
    }

    private void SyncLocalRagdollPresentation(bool forceResetIfNotRagdoll = false)
    {
        if (Object == null || !Object.IsValid)
            return;

        if (NetState == StateMachine.Ragdoll)
        {
            if (!_localRagdollApplied || _appliedRagdollSequence != RagdollSequence)
            {
                _ragdoll?.SetRagdollEnabled(true, RagdollInitialVelocity);
                if (_cc != null && _ragdoll != null && _ragdoll.HasValidRig)
                    _cc.enabled = false;

                _localRagdollApplied = true;
                _appliedRagdollSequence = RagdollSequence;
            }
        }
        else if (_localRagdollApplied || forceResetIfNotRagdoll)
        {
            _ragdoll?.SetRagdollEnabled(false);
            if (_cc != null && (_gameState == null || _gameState.IsInPlayground))
                _cc.enabled = true;

            _localRagdollApplied = false;
        }
    }

    private bool TryCastClimbRaycast(
        Vector3 origin,
        Vector3 direction,
        float maxDistance,
        LayerMask mask,
        out RaycastHit bestHit)
    {
        bestHit = default;
        if (direction.sqrMagnitude < 0.0001f)
            return false;

        int count = Physics.RaycastNonAlloc(
            origin,
            direction.normalized,
            _raycastScratch,
            maxDistance,
            mask,
            QueryTriggerInteraction.Ignore);

        float closestDist = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _raycastScratch[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.distance < closestDist)
            {
                closestDist = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private bool TryCastClimbSphere(
        Vector3 origin,
        Vector3 direction,
        float radius,
        float maxDistance,
        LayerMask mask,
        out RaycastHit bestHit)
    {
        bestHit = default;
        if (direction.sqrMagnitude < 0.0001f)
            return false;

        Vector3 dir = direction.normalized;
        Vector3 pulledOrigin = origin - dir * radius;
        int count = Physics.SphereCastNonAlloc(
            pulledOrigin,
            radius,
            dir,
            _raycastScratch,
            maxDistance + radius,
            mask,
            QueryTriggerInteraction.Ignore);

        float closestDist = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _raycastScratch[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.distance <= 0.001f)
                continue;

            if (hit.distance < closestDist)
            {
                closestDist = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private void ComputeWallBasis(Vector3 normal, out Vector3 wallUp, out Vector3 wallRight)
    {
        wallUp = Vector3.ProjectOnPlane(Vector3.up, normal);
        if (wallUp.sqrMagnitude < 0.001f)
            wallUp = Vector3.ProjectOnPlane(transform.up, normal);
        if (wallUp.sqrMagnitude < 0.001f)
            wallUp = Vector3.forward;

        wallUp.Normalize();
        wallRight = Vector3.Cross(normal, wallUp);
        if (wallRight.sqrMagnitude < 0.001f)
            wallRight = transform.right;
        wallRight.Normalize();
    }

    private float GetWallFacingYaw(Vector3 surfaceNormal)
    {
        Vector3 flatIntoWall = Vector3.ProjectOnPlane(-surfaceNormal, Vector3.up);
        if (flatIntoWall.sqrMagnitude < 0.001f)
            return _yaw;

        return Quaternion.LookRotation(flatIntoWall.normalized, Vector3.up).eulerAngles.y;
    }

    private bool SpendStamina(float amount)
    {
        if (amount <= 0f)
            return true;

        if (_condition == null || !_condition.CanUseStamina(amount))
            return false;

        return !Object.HasStateAuthority || _condition.TryUseStamina(amount);
    }

    private void SetAnimBool(string parameterName, bool value)
    {
        if (animator != null)
            animator.SetBool(parameterName, value);
    }

    private void ResetAnimatorBools()
    {
        SetAnimBool("Climbing", false);
        SetAnimBool("Walking", false);
        SetAnimBool("Running", false);
        SetAnimBool("Jump", false);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        _jumpPressed = context.started || context.performed;
    }

    public void OnUse(InputAction.CallbackContext context)
    {
        // Item actions are owned by PrototypeItemInput, independently of the shared movement map.
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        _attackPressed = context.started || context.performed;
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        if (Cursor.visible || PrototypeSettingsPanel.IsOpen)
        {
            _mouseDelta = Vector2.zero;
            return;
        }

        _mouseDelta = context.ReadValue<Vector2>() * (PrototypeSettings.Current != null
            ? PrototypeSettings.Current.sensitivity : mouseSensitivity);
        _pitch = Mathf.Clamp(_pitch - _mouseDelta.y, minPitch, maxPitch);

        if (statemachine == StateMachine.Wall)
        {
            float wallYaw = GetWallFacingYaw(_surfaceNormal);
            float rel = Mathf.Clamp(Mathf.DeltaAngle(wallYaw, _yaw) + _mouseDelta.x, -wallYawLimit, wallYawLimit);
            _yaw = wallYaw + rel;
        }
        else
        {
            _yaw += _mouseDelta.x;
        }
    }

    public void OnScroll(InputAction.CallbackContext context)
    {
        if (cam == null)
            return;

        float scrollValue = context.ReadValue<float>();
        _currentCamDistance = Mathf.Clamp(_currentCamDistance + scrollValue * scrollSensitivity, minDistance, maxDistance);
        cam.transform.localPosition = new Vector3(0f, 0f, _currentCamDistance);
    }

    public void OnInteract(InputAction.CallbackContext context) { }
    public void OnCrouch(InputAction.CallbackContext context) { }
    public void OnPrevious(InputAction.CallbackContext context) { }
    public void OnNext(InputAction.CallbackContext context) { }
    public void OnSprint(InputAction.CallbackContext context) { }
    public void OnWhistle(InputAction.CallbackContext context) { }

    public void UseItem()
    {
        UseItem(_playerInventory != null ? _playerInventory.SelectedSlot : -1);
    }

    public void UseItem(int slotIndex)
    {
        if (_playerInventory == null || slotIndex < 0)
            return;

        if (_playerInventory.UseItem(slotIndex))
            Debug.Log($"아이템 사용 (슬롯 {slotIndex + 1})");
    }

    public void NotifyInventoryAdded(ItemData item)
    {
        if (item == null || Object == null || !Object.HasStateAuthority || Object.HasInputAuthority)
            return;

        RpcSyncInventoryAdd(item.ItemName, item.AmountHeal);
    }

    public void NotifyInventoryRemoved(int slotIndex)
    {
        if (Object == null || !Object.HasStateAuthority || Object.HasInputAuthority)
            return;

        RpcSyncInventoryRemove(slotIndex);
    }

    public void NotifyInventoryCleared()
    {
        if (Object == null || !Object.HasStateAuthority || Object.HasInputAuthority)
            return;

        RpcSyncInventoryClear();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RpcSyncInventoryAdd(string itemName, float amountHeal)
    {
        if (Object != null && Object.HasStateAuthority)
            return;

        _playerInventory?.SyncAddItem(new ItemData(itemName, amountHeal));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RpcSyncInventoryRemove(int slotIndex)
    {
        if (Object != null && Object.HasStateAuthority)
            return;

        _playerInventory?.SyncRemoveAt(slotIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RpcSyncInventoryClear()
    {
        if (Object != null && Object.HasStateAuthority)
            return;

        _playerInventory?.SyncClear();
    }

    public void UseWaterGun()
    {
        if (Object == null || !Object.HasStateAuthority) return;
        if (PrototypeItemAim.Raycast(gameObject.scene.GetPhysicsScene(), GetCameraPivotPosition(),
            _latestAimDirection, WaterGunDistance, transform, out RaycastHit hit))
            hit.collider.GetComponentInParent<PlayerCondition>()?.RefreshWaterGunAttack();
    }
}
