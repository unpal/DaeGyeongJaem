using Cinemachine;
using Fusion;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerCondition))]
public class Un : NetworkBehaviour, RawInput.IPlayerActions
{
    public enum StateMachine
    {
        Floor, // 0~45도: 평지/완만한 경사 (점프/중력 적용)
        Wall, // 45~180도: 수직벽/오버행/천장/기둥 (등반)
        ToWall, // 벽으로 붙기위해 이동중인 상태(등반 하기전 잠깐동안)
        ToFloor
    }

    private Rigidbody _rb;
    public Collider _collider;
    [SerializeField] private StateMachine stateMachine = StateMachine.Floor;
    public StateMachine statemachine => stateMachine;

    private RawInput _rawInput;
    private RawInput.PlayerActions _playerActions;
    private InputAction _sprintAction;
    private PlayerCondition _condition;
    private PlayerGameState _gameState;
    [Networked] private NetworkBool JumpWasPressed { get; set; }

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float rayMaxDistance = 5f;
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private LayerMask wallLayerMask = ~0;
    [SerializeField] private LayerMask groundLayerMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.2f;

    [Header("Stamina")]
    [SerializeField, Min(0f)] private float runSpeed = 8f;
    [SerializeField, Min(0f)] private float sprintDrain = 15f;
    [SerializeField, Min(0f)] private float jumpCost = 10f;
    [SerializeField, Min(0f)] private float climbDrain = 20f;
    [SerializeField, Min(0f)] private float recoverRate = 10f;

    [Header("Climb Shared Settings")]
    [SerializeField]
    private Animator animator;

    private Vector2 _moveInput;
    private bool JumpPressed;
    private bool AttackPressed;
    Vector2 mouseDelta;
    [SerializeField]
    private Vector3 _surfaceNormal = Vector3.up;
    private bool _isClicking = false;
    private float _verticalVelocity;
    private bool _isGrounded;
    private Coroutine _wallCheckCoroutine;
    private Coroutine _wallClimbCoroutine;

    [Header("Camera & Head Tracking")]
    [SerializeField]
    private CinemachineVirtualCamera cam;

    [SerializeField] private Transform headTransform; // 머리 뼈대 (Bone)
    [SerializeField] private Transform camPivot; // 카메라 피벗 (비어있으면 자동 생성)
    [SerializeField] private Vector3 cameraEyeOffset = new Vector3(0f, 0.1f, 0f); // 머리 뼈 기준 눈높이 오프셋
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float minPitch = -80f; // 아래쪽 최대 각도
    [SerializeField] private float maxPitch = 80f; // 위쪽 최대 각도
    [SerializeField] private float wallYawLimit = 70f; // 벽 등반 시 좌우 시야각 한계

    [Header("Wall Climbing")]
    [SerializeField]
    private Transform leftUpperArm;

    [SerializeField] private Transform rightUpperArm;
    [SerializeField] private Transform leftCalf;
    [SerializeField] private Transform rightCalf;
    [SerializeField] private float wallDistance = 0.5f;
    [SerializeField] private float armLength = 0.7f;
    [SerializeField] private float wallClimbDuration = 0.5f;
    [SerializeField] private float tweenConst = 0.1f;
    public float tweenconst => tweenConst;
    [SerializeField] private float wallRotationTweenSpeed = 10f; // 벽 회전 트위닝 속도
    [SerializeField] private int searchCount = 10;
    [SerializeField] private float upGoingDistanceDivider = 0.5f;

    [Header("Scroll / Zoom Settings")]
    [SerializeField]
    private float scrollSensitivity = 0.01f;

    [SerializeField] private float minDistance = -10f;
    [SerializeField] private float maxDistance;
    private float _currentCamDistance;

    // 통합 각도 상태 (Single Source of Truth)
    private float _pitch; // 상하 각도 (Pitch)
    private float _yaw; // 지상: 월드 Yaw, 벽: 벽 기준 상대 Yaw
    private bool _isRightTurn = true;
    public Vector3 _rightHandPos;
    public Vector3 _leftHandPos;
    public Vector3 _rightFootPos;
    public Vector3 _leftFootPos;
    
    public Vector3 _rightHandNormal;
    public Vector3 _leftHandNormal;
    public Vector3 _rightFootNormal;
    public Vector3 _leftFootNormal;
    private Quaternion _smoothedWallRot = Quaternion.identity;


    [Header("IK Settings")]
    [Range(0f, 1f)]
    [SerializeField]
    private float handIKWeight = 1f;
    public float handikweight => handIKWeight;

    [Range(0f, 1f)][SerializeField] private float footIKWeight = 1f;
    public float footikweight => footIKWeight;


    private bool _climbStepActive = false;
    private bool _climbInitialized = false;
    private float _climbTimer = 0f;
    private float _climbUpGoingDistance = 0f;

    [SerializeField]
    private float _transTimeCheck = 0;
    [SerializeField]
    private float _transTime = 0.1f;

    private Vector3 _hitPoint = Vector3.zero;
    private Vector3 _hitNormal = Vector3.zero;
    [SerializeField]
    private float _forceTime = 0.1f;
    [SerializeField]
    private float _forceTimeCheck = 0;

    [Header("Wall Edge / Mantle")]
    [SerializeField] private float mantleDuration = 0.55f;
    [SerializeField] private float mantleForwardOffset = 0.35f;
    [SerializeField] private float mantleRayUp = 1.5f;
    [SerializeField] private float mantleRayDistance = 4f;
    [SerializeField] private float mantleHeightTolerance = 0.1f;

    private float _mantleElapsed;
    private Vector3 _mantleStartPosition;
    private Vector3 _mantleTargetPosition;
    private Quaternion _mantleStartBodyRotation;
    private Quaternion _mantleTargetBodyRotation;
    private Quaternion _mantleStartCameraRotation;
    private Quaternion _mantleTargetCameraRotation;
    private float _mantleTargetYaw;

    private Vector3 _HitPo;
    private Vector3 _HitNo;

    private Vector3 _TransHitPo;
    private Vector3 _TransHitNo;

    [SerializeField]
    private GameObject RayCastObj;


    public override void Spawned()
    {
        _condition = GetComponent<PlayerCondition>();
        _gameState = GetComponent<PlayerGameState>();
        if(Object.HasInputAuthority)
        {
            _rawInput = new RawInput();
            _playerActions = _rawInput.Player;
            _playerActions.Enable();
            _playerActions.AddCallbacks(this);
            _sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            _sprintAction.AddBinding("<Gamepad>/leftStickPress");
            _sprintAction.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // 카메라 설정
            if (cam == null)
            {
                cam = GetComponentInChildren<CinemachineVirtualCamera>();
                if (cam == null)
                    Debug.LogError("카메라 설정 오류 발생 바로 확인 바람");
            }

            // 머리 뼈 Transform 자동 탐색 (Humanoid Avatar 기준)
            if (headTransform == null && animator != null && animator.isHuman)
            {
                headTransform = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            // 독립 카메라 피벗 생성 (뼈대의 회전 왜곡 영향 차단)
            if (camPivot == null)
            {
                GameObject pivotGo = new GameObject("CameraPivot");
                camPivot = pivotGo.transform;
                camPivot.position = GetCameraPivotPosition();
                camPivot.rotation = transform.rotation;
            }

            if (cam != null)
            {
                // 카메라는 피벗의 자식으로 배치
                cam.transform.SetParent(camPivot, false);
                _currentCamDistance = Mathf.Clamp(cam.transform.localPosition.z, minDistance, maxDistance);
                cam.transform.localPosition = new Vector3(0f, 0f, _currentCamDistance);
                cam.transform.localRotation = Quaternion.identity;
            }
        }
        else
        {
            if (cam != null)
            {
                cam.gameObject.SetActive(false);
            }
        }


        TryGetComponent(out _rb);
        if (!_rb)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
        }

        if (!_collider)
            TryGetComponent(out _collider);

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        _rb.useGravity = false;
        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        // 초기 수평 각도 및 회전 상태 설정
        _yaw = transform.eulerAngles.y;
        _smoothedWallRot = transform.rotation;

 
    }

    //private void Start()
    //{
    //    Cursor.lockState = CursorLockMode.Locked;
    //    Cursor.visible = false;
    //}

    private void OnDestroy()
    {
        _sprintAction?.Dispose();
        _rawInput?.Dispose();
    }

    void OnEnable()
    {
        if (_rawInput != null)
            _playerActions.Enable();
        _sprintAction?.Enable();
    }
    public NetworkInputData GetNetworkInput()
    {
        NetworkInputData data = new NetworkInputData();

        data.Move = _moveInput;
        data.Look = mouseDelta;

        data.Buttons.Set(
            (int)PlayerButtons.Jump,
            JumpPressed);

        data.Buttons.Set(
            (int)PlayerButtons.Attack,
            AttackPressed);

        data.Buttons.Set(
            (int)PlayerButtons.Sprint,
            _sprintAction != null && _sprintAction.IsPressed());

        return data;
    }
    void OnDisable()
    {
        if (_rawInput != null)
            _playerActions.Disable();
        _sprintAction?.Disable();
        _moveInput = Vector2.zero;
        JumpPressed = false;
        AttackPressed = false;
        if (_wallCheckCoroutine != null)
        {
            StopCoroutine(_wallCheckCoroutine);
            _wallCheckCoroutine = null;
        }

        if (_wallClimbCoroutine != null)
        {
            StopCoroutine(_wallClimbCoroutine);
            _wallClimbCoroutine = null;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (_gameState == null || !_gameState.IsInPlayground || _condition.IsGameOver)
        {
            if (stateMachine == StateMachine.Wall)
                TransitionToFloor();
            _rb.velocity = Vector3.zero;
            return;
        }

        if (!GetInput(out NetworkInputData data))
            return;

        switch (stateMachine)
        {
            case StateMachine.Floor:
            {
                FloorMove(data);

                if (data.Buttons.IsSet(PlayerButtons.Attack) && CanSpendStamina(climbDrain * Runner.DeltaTime))
                    WallCheck();

                break;
            }

            case StateMachine.Wall:
            {

                // WallClimb 안에서 ToFloor로 전환했다면 이 틱에
                // 일반 벽타기 해제 로직이 상태를 덮어쓰지 않게 한다.
                if (stateMachine == StateMachine.Wall &&
                    !data.Buttons.IsSet(PlayerButtons.Attack) ||
                    !SpendStamina(climbDrain * Runner.DeltaTime))
                {
                    TransitionToFloor();
                }
                    else
                    {
                        WallClimb(data);
                        if (climbDrain > 0f && _condition.CurrentStamina <= 0f && stateMachine == StateMachine.Wall)
                            TransitionToFloor();
                    }
                    break;
            }

            case StateMachine.ToWall:
                TransitionToWall(_hitPoint, _hitNormal);
                break;

            case StateMachine.ToFloor:
                UpdateMantleToFloor();
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
        JumpWasPressed = data.Buttons.IsSet(PlayerButtons.Jump);
    }

    private bool CanSpendStamina(float amount)
    {
        return amount == 0f || _condition.CanUseStamina(amount);
    }

    private bool SpendStamina(float amount)
    {
        if (!CanSpendStamina(amount))
            return false;

        if (amount == 0f)
            return true;

        return Object.HasStateAuthority
            ? _condition.TryUseStamina(amount)
            : true;
    }

    public void ResetForNextRound()
    {
        if (Object != null && Object.HasStateAuthority)
            RpcResetMovement();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcResetMovement()
    {
        if (_rb == null)
            return;

        TransitionToFloor();
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _verticalVelocity = 0f;
        _isGrounded = false;
        _climbInitialized = false;
        _climbStepActive = false;
        _climbTimer = 0f;
        _climbUpGoingDistance = 0f;
        _isRightTurn = true;
        if (Object.HasStateAuthority || Object.HasInputAuthority)
            JumpWasPressed = false;
    }

    /// <summary>
    /// 단일 Pitch/Yaw 각도를 기준으로 몸체(Body)와 카메라 피벗(CameraPivot)의 위치/회전을 동기화합니다.
    /// </summary>
    private void LateUpdate()
    {
        if (Object == null || !Object.HasInputAuthority)
            return;

        if (camPivot == null)
            return;

        camPivot.position = GetCameraPivotPosition();

        // 모서리 전환 중에는 _surfaceNormal을 이용해 벽 회전을 다시 계산하지 않는다.
        // 전환 시작 시 저장한 회전값을 기준으로 몸체와 카메라를 함께 보간한다.
        if (stateMachine == StateMachine.ToFloor)
        {
            float duration = Mathf.Max(mantleDuration, 0.01f);
            float t = Mathf.Clamp01(_mantleElapsed / duration);
            float smoothT = t * t * (3f - 2f * t);

            transform.rotation = Quaternion.Slerp(
                _mantleStartBodyRotation,
                _mantleTargetBodyRotation,
                smoothT);

            camPivot.rotation = Quaternion.Slerp(
                _mantleStartCameraRotation,
                _mantleTargetCameraRotation,
                smoothT);

            return;
        }

        if (stateMachine == StateMachine.Floor)
        {
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            camPivot.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            return;
        }

        // Wall / ToWall: 벽의 Up 방향을 기준으로 회전한다.
        Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, _surfaceNormal).normalized;
        if (wallUp.sqrMagnitude < 0.001f)
            wallUp = transform.up;

        Quaternion targetWallBaseRot = Quaternion.LookRotation(-_surfaceNormal, wallUp);
        float blend = 1f - Mathf.Exp(-wallRotationTweenSpeed * Time.deltaTime);
        _smoothedWallRot = Quaternion.Slerp(_smoothedWallRot, targetWallBaseRot, blend);

        transform.rotation = _smoothedWallRot;
        camPivot.rotation = _smoothedWallRot * Quaternion.Euler(_pitch, _yaw, 0f);
    }

    private Vector3 GetCameraPivotPosition()
    {
        return headTransform != null
            ? headTransform.position + transform.TransformDirection(cameraEyeOffset)
            : transform.position + Vector3.up * 1.5f;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    private void FloorMove(NetworkInputData data)
    {
        float deltaTime = Runner.DeltaTime;
        float checkDist = _collider != null ? _collider.bounds.extents.y + 0.1f : groundCheckDistance;
        _isGrounded = Physics.Raycast(transform.position, Vector3.down, checkDist, groundLayerMask);
        bool jumped = false;

        if (_isGrounded)
        {
            if (data.Buttons.IsSet(PlayerButtons.Jump) && !JumpWasPressed && SpendStamina(jumpCost))
            {
                _verticalVelocity = jumpForce;
                _isGrounded = false;
                jumped = true;
            }
            else if (_verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
        }
        else
        {
            _verticalVelocity -= gravity * deltaTime;
        }

        //카메라를 LateUpdate에서 바꿔주기에 Networktansfrom에서 컴포넌트 내에 변경이 있을 경우 해당 컴포넌트의 값이 LateUpdate에서 변경된 값이 적용되지 않는다.


        // 내 캐릭터가 보는 위치(y축 회전 값)
        Quaternion yawRotation =
            Quaternion.Euler(0f, _yaw, 0f);


        // y 축 회전값을 통한 내가 보는 곳에 대한 앞, 옆 찾기
        Vector3 forward =
            yawRotation * Vector3.forward;

        Vector3 right =
            yawRotation * Vector3.right;

        // 찾은 앞, 옆을 통해 이동할 위치를 측정한다.
        Vector3 moveDirection =
            right * data.Move.x +
            forward * data.Move.y;


        // 대각선에 대한 보값을 제거한다.
        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        // 마지막으로 이동할 속도를 곱한다.
        bool tryingToSprint = data.Buttons.IsSet(PlayerButtons.Sprint) && data.Move.sqrMagnitude > 0.01f;
        bool sprinting = tryingToSprint && _condition.CanSprint && SpendStamina(sprintDrain * deltaTime);
        Vector3 moveInput = Vector3.ClampMagnitude(moveDirection, 1f);
        var move = moveInput * (sprinting ? runSpeed : moveSpeed);

        if (_isGrounded && !jumped && !tryingToSprint && !data.Buttons.IsSet(PlayerButtons.Attack))
            _condition.RecoverStamina(recoverRate * deltaTime);



        // 움직일 때 모션이 작동하도록 한다.
        if (move.sqrMagnitude > 0)
        {
            animator.SetBool("Running", true);
        }
        else
        {
            animator.SetBool("Running", false);
        }

        move.y = _verticalVelocity;

        _rb.velocity = move;


    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started || context.performed)
        {
            JumpPressed = true;
        }
        else
        {
            JumpPressed = false;
        }
    }

    public void OnClick(InputAction.CallbackContext context)
    {
        //if (context.started || (context.performed && _wallCheckCoroutine == null))
        //{
        //    _wallCheckCoroutine ??= StartCoroutine(WallCheckCoroutine());
        //}
        //else if (context.canceled)
        //{
        //    if (_wallCheckCoroutine != null)
        //    {
        //        StopCoroutine(_wallCheckCoroutine);
        //        _wallCheckCoroutine = null;
        //    }

        //    if (stateMachine == StateMachine.Wall)
        //    {
        //        TransitionToFloor();
        //    }
        //}

        if (context.started || (context.performed))
        {
            AttackPressed = true;
        }
        else
        {
            AttackPressed = false;

        }
    }

    private void WallCheck()
    {

        var rayDirTransform = headTransform != null ? headTransform : transform;
        var ray = new Ray
        {
            origin = transform.position,
            direction = rayDirTransform.forward
        };
        Vector3 vector3 = ray.origin;
        vector3.y += 1;
        ray.origin = vector3;
        Debug.DrawRay(ray.origin, ray.direction * rayMaxDistance, Color.red);
        if (Physics.Raycast(ray, out RaycastHit transHit, rayMaxDistance, wallLayerMask))
        {
            _transTimeCheck = _transTime;
            stateMachine = StateMachine.ToWall;
            _hitPoint = transHit.point;
            _hitNormal = transHit.normal;
            //TransitionToWall(transHit.point, transHit.normal);

        }
    }

    private void TransitionToWall(Vector3 hitPoint, Vector3 hitNormal)
    {
        // 땅에서 벽으로 갈 때 _transTime 만큼의 시간을 소비해서 선형 이동을 하게 설정
        _transTimeCheck -= Runner.DeltaTime;

        if ((_transTimeCheck < 0))
        {
            // _transTime 만큼의 시간이 끝나면 StateMachine 변경
            stateMachine = StateMachine.Wall;
        }
        _surfaceNormal = hitNormal;
        //stateMachine = StateMachine.Wall;

        // 벽 상태 진입 시 물리 속도 초기화 (잔여 속도로 밀려나는 현상 방지)
        _rb.velocity = Vector3.zero;
        _verticalVelocity = 0f;
        //_rb.isKinematic = true;

        // 벽에 붙을 때 벽 기준 상대 Yaw를 0(정면)으로 초기화
        _yaw = 0f;

        Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, hitNormal).normalized;
        if (wallUp.sqrMagnitude < 0.001f)
        {
            wallUp = Vector3.up;
        }
        Vector3 velo = Vector3.zero;

        transform.position = Vector3.SmoothDamp(transform.position,hitPoint + hitNormal * wallDistance,ref velo, _transTime);
        _smoothedWallRot = Quaternion.LookRotation(-hitNormal, wallUp);
        transform.rotation = _smoothedWallRot;


        _climbInitialized = false;
        _climbStepActive = false;
        _climbTimer = 0f;
        _climbUpGoingDistance = 0f;
    }

    private void TransitionToFloor(bool isForce = false)
    {
        if (isForce)
        {
            // 전환 중 매 Tick 다시 시작하지 않도록 한다.
            if (stateMachine == StateMachine.ToFloor)
                return;

            if (TryGetMantleTarget(out Vector3 targetPosition, out Quaternion targetRotation))
            {
                BeginMantleToFloor(targetPosition, targetRotation);
            }
            else
            {
                // 위쪽에 올라갈 수 있는 평지를 찾지 못했다면 벽타기만 종료한다.
                SetFloorYawFromCamera();
                FinishTransitionToFloor();
            }

            return;
        }

        SetFloorYawFromCamera();
        FinishTransitionToFloor();
    }

    private void SetFloorYawFromCamera()
    {
        Vector3 lookForward = camPivot != null ? camPivot.forward : transform.forward;
        Vector3 floorForward = Vector3.ProjectOnPlane(lookForward, Vector3.up).normalized;

        if (floorForward.sqrMagnitude > 0.001f)
            _mantleTargetYaw = Quaternion.LookRotation(floorForward, Vector3.up).eulerAngles.y;
        else
            _mantleTargetYaw = transform.eulerAngles.y;
    }

    private bool TryGetMantleTarget(out Vector3 targetPosition, out Quaternion targetRotation)
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;

        // 벽타기 중 transform.forward는 벽을 향하므로, 그 방향으로 벽 끝 너머를 탐색한다.
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(-_surfaceNormal, Vector3.up).normalized;

        if (forward.sqrMagnitude < 0.001f)
            return false;

        Vector3 rayOrigin = transform.position
            + forward * (wallDistance + mantleForwardOffset)
            + Vector3.up * mantleRayUp;

        Debug.DrawRay(rayOrigin, Vector3.down * mantleRayDistance, Color.yellow);

        // 위쪽 평지 탐색은 groundLayerMask를 사용한다.
        if (!Physics.Raycast(
            rayOrigin, Vector3.down, out RaycastHit topHit,
            mantleRayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // 수직 벽이나 천장은 평지로 취급하지 않는다.
        if (Vector3.Angle(topHit.normal, Vector3.up) > 30f)
            return false;

        float halfHeight = _collider != null ? _collider.bounds.extents.y : 0.9f;
        targetPosition = topHit.point
            + Vector3.up * (halfHeight + mantleHeightTolerance)
            + forward * mantleForwardOffset;

        targetRotation = Quaternion.LookRotation(forward, Vector3.up);
        return true;
    }

    private void BeginMantleToFloor(Vector3 targetPosition, Quaternion targetRotation)
    {
        stateMachine = StateMachine.ToFloor;
        _mantleElapsed = 0f;

        _mantleStartPosition = transform.position;
        _mantleTargetPosition = targetPosition;
        _mantleStartBodyRotation = transform.rotation;
        _mantleTargetBodyRotation = targetRotation;

        Vector3 cameraForward = camPivot != null ? camPivot.forward : transform.forward;
        Vector3 flatCameraForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up).normalized;
        if (flatCameraForward.sqrMagnitude < 0.001f)
            flatCameraForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        if (flatCameraForward.sqrMagnitude > 0.001f)
            _mantleTargetYaw = Quaternion.LookRotation(flatCameraForward, Vector3.up).eulerAngles.y;
        else
            _mantleTargetYaw = targetRotation.eulerAngles.y;

        _mantleStartCameraRotation = camPivot != null ? camPivot.rotation : transform.rotation;
        _mantleTargetCameraRotation = Quaternion.Euler(_pitch, _mantleTargetYaw, 0f);

        _rb.velocity = Vector3.zero;
        _rb.isKinematic = true;
        _verticalVelocity = 0f;
        _climbInitialized = false;
        _climbStepActive = false;
        _climbTimer = 0f;
    }

    private void UpdateMantleToFloor()
    {
        _mantleElapsed += Runner.DeltaTime;

        float duration = Mathf.Max(mantleDuration, 0.01f);
        float t = Mathf.Clamp01(_mantleElapsed / duration);
        float smoothT = t * t * (3f - 2f * t);

        transform.position = Vector3.Lerp(_mantleStartPosition, _mantleTargetPosition, smoothT);

        if (t >= 1f)
        {
            transform.position = _mantleTargetPosition;
            FinishTransitionToFloor();
        }
    }

    private void FinishTransitionToFloor()
    {
        stateMachine = StateMachine.Floor;
        _rb.isKinematic = false;
        _rb.velocity = Vector3.zero;
        _verticalVelocity = 0f;

        _yaw = _mantleTargetYaw;
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        _smoothedWallRot = transform.rotation;

        _climbInitialized = false;
        _climbStepActive = false;
        _climbTimer = 0f;
    }

    private IEnumerator WallCheckCoroutine()
    {
        while (true)
        {
            var rayDirTransform = headTransform != null ? headTransform : transform;
            var ray = new Ray
            {
                origin = transform.position,
                direction = rayDirTransform.forward
            };
            Debug.DrawRay(ray.origin, ray.direction * rayMaxDistance, Color.red);
            if (Physics.Raycast(ray, out _, rayMaxDistance, wallLayerMask))
            {
                _wallCheckCoroutine = null;

                if (_wallClimbCoroutine != null)
                {
                    StopCoroutine(_wallClimbCoroutine);
                }

                _wallClimbCoroutine = StartCoroutine(WallClimbCoroutine());
                yield break;
            }

            yield return null;
        }
    }

    private Vector3 SimpleRaycast(
       Vector3 origin,
       Vector3 direction,
       float rayMaxDistance,
       out Vector3 hitNormal)
    {
        if (Physics.Raycast(
            origin,
            direction,
            out RaycastHit hit,
            rayMaxDistance,
            wallLayerMask,
            QueryTriggerInteraction.Ignore))
        {
            hitNormal = hit.normal;
            return hit.point;
        }
        //
        // 벽을 찾지 못했다면 가짜 위치를 만들지 않는다.
        hitNormal = Vector3.zero;
        return Vector3.zero;
    }

    // Fusion 에서는 yieid return 을 사용할 수 없음
    // 그렇기에 Coroutine을 제거하고 일반 함수로 변경해 벽을 타게 변경함
    private void WallClimb(NetworkInputData data)
    {

        float dt = Runner.DeltaTime;

        // 1. 벽 진입 직후 한 번만 손/발 초기화

        if (!_climbInitialized)
        {
            _rightHandPos = SimpleRaycast(
                rightUpperArm.position,
                transform.forward,
                rayMaxDistance,
                out _rightHandNormal
            );

            _leftHandPos = SimpleRaycast(
                leftUpperArm.position,
                transform.forward,
                rayMaxDistance,
                out _leftHandNormal
            );

            _rightFootPos = SimpleRaycast(
                rightCalf.position,
                transform.forward,
                rayMaxDistance,
                out _rightFootNormal
            );

            _leftFootPos = SimpleRaycast(
                leftCalf.position,
                transform.forward,
                rayMaxDistance,
                out _leftFootNormal
            );

            _climbInitialized = true;
            _climbStepActive = false;
            _climbTimer = 0f;
        }
        if(data.Move.sqrMagnitude <= 0 )
        {
            return;
        }
        // 2. 현재 climbing step이 끝났다면 다음 손/발 동작을 계산한다.

        if (!_climbStepActive)
        {
            Transform arm;
            Transform calf;

            // 원본과 동일
            if (_isRightTurn)
            {
                arm = rightUpperArm;
                calf = leftCalf;
            }
            else
            {
                arm = leftUpperArm;
                calf = rightCalf;
            }

            if (arm == null || calf == null)
                return;

            // =====================================================
            // 2-1. 팔 Raycast
            // =====================================================
            var armLowerBound =
                (transform.forward - (transform.up * data.Move.y + transform.right * data.Move.x)).normalized;

            var armUpperBound =
                transform.up * data.Move.y + transform.right * data.Move.x;

            RaycastHit lastHit = default;
            bool anyHit = false;

            for (var i = 0; i < searchCount; i++)
            {
                var midDir =
                    (armLowerBound + armUpperBound).normalized;

                var query = new Ray
                {
                    origin = arm.position,
                    direction = midDir
                };

                if (Physics.Raycast(
                    query,
                    out RaycastHit hit,
                    armLength,
                    wallLayerMask))
                {
                    armLowerBound = midDir;
                    lastHit = hit;
                    anyHit = true;
                }
                else
                {
                    armUpperBound = midDir;
                }
            }

            // =====================================================
            // 2-2. 손 IK 목표 위치 설정
            // =====================================================

            if (anyHit)
            {
                if (_isRightTurn)
                {
                    _rightHandPos = lastHit.point;
                    _rightHandNormal = lastHit.normal;
                }
                else
                {
                    _leftHandPos = lastHit.point;
                    _leftHandNormal = lastHit.normal;
                }
            }

            // =====================================================
            // 2-3. 반대쪽 발 Raycast
            // =====================================================

            var calfRay = new Ray
            {
                origin = calf.position,
                direction = transform.forward
            };

            Debug.DrawRay(
                calfRay.origin,
                calfRay.direction * rayMaxDistance,
                Color.red
            );

            if (Physics.Raycast(
                calfRay,
                out RaycastHit calfHit,
                rayMaxDistance,
                wallLayerMask))
            {
                if (_isRightTurn)
                {
                    _leftFootPos = calfHit.point;
                    _leftFootNormal = calfHit.normal;
                }
                else
                {
                    _rightFootPos = calfHit.point;
                    _rightFootNormal = calfHit.normal;
                }
            }

            // =====================================================
            // 2-4. 손/발 Raycast Normal 평균
            // =====================================================

            Vector3 avgNormal = Vector3.zero;
            int normalCount = 0;

            if (_rightHandNormal.sqrMagnitude > 0.001f)
            {
                avgNormal += _rightHandNormal;
                normalCount++;
            }

            if (_leftHandNormal.sqrMagnitude > 0.001f)
            {
                avgNormal += _leftHandNormal;
                normalCount++;
            }

            if (_rightFootNormal.sqrMagnitude > 0.001f)
            {
                avgNormal += _rightFootNormal;
                normalCount++;
            }

            if (_leftFootNormal.sqrMagnitude > 0.001f)
            {
                avgNormal += _leftFootNormal;
                normalCount++;
            }

            if (normalCount > 0)
            {
                _surfaceNormal =
                    (avgNormal / normalCount).normalized;
            }

            // =====================================================
            // 2-5. 벽 각도 확인
            // =====================================================

            if (Vector3.Angle((transform.up * data.Move.y + transform.right * data.Move.x) , _surfaceNormal) < 23f)
            {
                TransitionToFloor(true);
                return;
            }

            // =====================================================
            // 2-6. Wall Move 계산
            // =====================================================



            Vector3 wallMove =
                Vector3.ProjectOnPlane(
                    (transform.up * data.Move.y + transform.right * data.Move.x),
                    _surfaceNormal
                ).normalized;

            if (wallMove.sqrMagnitude < 0.001f)
            {
                wallMove = transform.up;
            }

            // =====================================================
            // 2-7. 이번 climbing step의 상승 거리 계산
            //
            // 원본 코드 그대로:
            //
            // reachUp = 손 위치와 Raycast 위치의
            //           wallUp 방향 거리
            //
            // upGoingDistance =
            //           reachUp * upGoingDistanceDivider
            // =====================================================

            if (anyHit)
            {
                Vector3 DistanceToHitAndArm = lastHit.point - arm.position;

                //if(DistanceToHitAndArm.y < 0.2f)
                //{
                //    TransitionToFloor(true);
                //    return;
                //}
                float reachMove =
                    Vector3.Dot(
                        DistanceToHitAndArm,
                        wallMove
                    );

                _climbUpGoingDistance =
                    Mathf.Max(
                        0f,
                        reachMove * upGoingDistanceDivider
                    );
            }
            else
            {
                TransitionToFloor(true);
                return;
                //_climbUpGoingDistance = 0f;
            }

            // =====================================================
            // 2-8. 새로운 climbing step 시작
            // =====================================================

            _climbTimer = 0f;
            _climbStepActive = true;
        }

        // =========================================================
        // 3. 현재 climbing step 진행
        // =========================================================

        float duration =
            Mathf.Max(wallClimbDuration, 0.01f);

        // 이번 Tick에서 실제로 사용할 시간
        float stepDeltaTime =
            Mathf.Min(
                dt,
                duration - _climbTimer
            );

        if (stepDeltaTime > 0f)
        {
            // 현재 벽 기준의 위쪽 방향
            Vector3 WallMove =
                Vector3.ProjectOnPlane(
                    (transform.up * data.Move.y + transform.right * data.Move.x),
                    _surfaceNormal
                ).normalized;

            if (WallMove.sqrMagnitude < 0.001f)
            {
                WallMove = transform.up;
            }

            // =====================================================
            // 원본 Coroutine의:
            //
            // transform.position +=
            //     wallUp * (upGoingDistance * dt / duration);
            //
            // 를 그대로 Fusion Tick으로 변환
            // =====================================================

            transform.position +=
                WallMove *
                (
                    _climbUpGoingDistance *
                    (stepDeltaTime / duration)
                );

            // =====================================================
            // 벽과의 거리 실시간 보정
            // =====================================================

            if (Physics.Raycast(
                transform.position,
                -_surfaceNormal,
                out RaycastHit moveHit,
                rayMaxDistance,
                wallLayerMask))
            {
                float currentDistanceToWall =
                    Vector3.Dot(
                        transform.position - moveHit.point,
                        _surfaceNormal
                    );

                float distanceError =
                    wallDistance -
                    currentDistanceToWall;

                transform.position +=
                    _surfaceNormal * distanceError;
            }

            _climbTimer += stepDeltaTime;
        }
        // =========================================================
        // 4. 현재 climbing step 완료
        // =========================================================

        if (_climbTimer >= duration)
        {
            _climbTimer = duration;

            // 원본의:
            //
            // _isRightTurn = !_isRightTurn;
            //
            // 과 동일
            _isRightTurn = !_isRightTurn;

            _climbTimer = 0f;
            _climbStepActive = false;
        }
    }

    //private void WallClimb(NetworkInputData data)
    //{
    //    try
    //    {
    //        var ray = new Ray
    //        {
    //            origin = transform.position,
    //            direction = transform.forward
    //        };
    //        if (Physics.Raycast(ray, out RaycastHit transHit, rayMaxDistance, wallLayerMask))
    //        {
    //            var angle = Vector3.Angle(Vector3.up, transHit.normal);
    //            if (angle >= 45f)
    //            {
    //                TransitionToWall(transHit.point, transHit.normal);
    //            }
    //            else
    //            {
    //                TransitionToFloor();
    //                return;
    //            }
    //        }

    //        // 손과 발의 초기 위치 및 법선 설정
    //        _rightHandPos = SimpleRaycast(rightUpperArm.position, transform.forward, rayMaxDistance, out _rightHandNormal);
    //        _leftHandPos = SimpleRaycast(leftUpperArm.position, transform.forward, rayMaxDistance, out _leftHandNormal);
    //        _rightFootPos = SimpleRaycast(rightCalf.position, transform.forward, rayMaxDistance, out _rightFootNormal);
    //        _leftFootPos = SimpleRaycast(leftCalf.position, transform.forward, rayMaxDistance, out _leftFootNormal);

    //        while (stateMachine == StateMachine.Wall)
    //        {
    //            Transform arm;
    //            Transform calf;
    //            if (_isRightTurn)
    //            {
    //                arm = rightUpperArm;
    //                calf = leftCalf;
    //            }
    //            else
    //            {
    //                arm = leftUpperArm;
    //                calf = rightCalf;
    //            }

    //            if (arm == null || calf == null)
    //            {
    //                return;
    //            }

    //            RaycastHit hit;

    //            var armLowerBound = (transform.forward - transform.up).normalized;
    //            var armUpperBound = transform.up;
    //            RaycastHit lastHit = default;
    //            bool anyHit = false;

    //            for (var i = 0; i < searchCount; i++)
    //            {
    //                var midDir = (armLowerBound + armUpperBound).normalized;
    //                var query = new Ray
    //                {
    //                    origin = arm.position,
    //                    direction = midDir,
    //                };
    //                if (Physics.Raycast(query, out hit, armLength, wallLayerMask))
    //                {
    //                    armLowerBound = midDir;
    //                    lastHit = hit;
    //                    anyHit = true;
    //                }
    //                else
    //                {
    //                    armUpperBound = midDir;
    //                }
    //            }

    //            if (anyHit)
    //            {
    //                if (_isRightTurn)
    //                {
    //                    _rightHandPos = lastHit.point;
    //                    _rightHandNormal = lastHit.normal;
    //                }
    //                else
    //                {
    //                    _leftHandPos = lastHit.point;
    //                    _leftHandNormal = lastHit.normal;
    //                }
    //            }

    //            var calfRay = new Ray
    //            {
    //                origin = calf.position,
    //                direction = transform.forward
    //            };
    //            Debug.DrawRay(calfRay.origin, calfRay.direction * rayMaxDistance, Color.red);
    //            if (Physics.Raycast(calfRay, out hit, rayMaxDistance, wallLayerMask))
    //            {
    //                if (_isRightTurn)
    //                {
    //                    _leftFootPos = hit.point;
    //                    _leftFootNormal = hit.normal;
    //                }
    //                else
    //                {
    //                    _rightFootPos = hit.point;
    //                    _rightFootNormal = hit.normal;
    //                }
    //            }

    //            // 1. 손발 ray들의 hit normal 평균 계산
    //            Vector3 avgNormal = Vector3.zero;
    //            int normalCount = 0;
    //            if (_rightHandNormal.sqrMagnitude > 0.001f) { avgNormal += _rightHandNormal; normalCount++; }
    //            if (_leftHandNormal.sqrMagnitude > 0.001f) { avgNormal += _leftHandNormal; normalCount++; }
    //            if (_rightFootNormal.sqrMagnitude > 0.001f) { avgNormal += _rightFootNormal; normalCount++; }
    //            if (_leftFootNormal.sqrMagnitude > 0.001f) { avgNormal += _leftFootNormal; normalCount++; }

    //            if (normalCount > 0)
    //            {
    //                _surfaceNormal = (avgNormal / normalCount).normalized;
    //            }

    //            // surface normal이 45도 미만(평지/완만한 경사)이면 Floor로 복귀
    //            if (Vector3.Angle(Vector3.up, _surfaceNormal) < 23f)
    //            {
    //                TransitionToFloor();
    //                return;
    //            }

    //            // 2. 손발 ray 평균 법선을 바탕으로 wallUp 결정
    //            var wallUp = Vector3.ProjectOnPlane(Vector3.up, _surfaceNormal).normalized;
    //            if (wallUp.sqrMagnitude < 0.001f)
    //            {
    //                wallUp = transform.up;
    //            }

    //            float upGoingDistance;
    //            if (anyHit)
    //            {
    //                var reachUp = Vector3.Dot(lastHit.point - arm.position, wallUp);
    //                upGoingDistance = Mathf.Max(0f, reachUp * upGoingDistanceDivider);
    //            }
    //            else
    //            {
    //                upGoingDistance = 0f;
    //            }

    //            // 위치 이동 보간
    //            float duration = Mathf.Max(wallClimbDuration, 0.01f);
    //            float counter = 0f;
    //            while (counter < duration && stateMachine == StateMachine.Wall)
    //            {
    //                float dt = Time.deltaTime;
    //                counter += dt;

    //                // 캐릭터 위치 이동
    //                transform.position += wallUp * (upGoingDistance * (dt / duration));

    //                // 이동 중 벽면과의 거리 실시간 보정 (몸체 로테이팅 법선은 손발 평균 _surfaceNormal 유지)
    //                if (Physics.Raycast(transform.position, -_surfaceNormal, out var moveHit,
    //                        rayMaxDistance,
    //                        wallLayerMask))
    //                {
    //                    var currentDistanceToWall = Vector3.Dot(transform.position - moveHit.point, _surfaceNormal);
    //                    var distanceError = wallDistance - currentDistanceToWall;
    //                    transform.position += _surfaceNormal * distanceError;
    //                }

    //                return;
    //            }

    //            _isRightTurn = !_isRightTurn;
    //        }
    //    }
    //    finally
    //    {
    //        _wallClimbCoroutine = null;
    //    }
    //}
    private IEnumerator WallClimbCoroutine()
    {
        try
        {
            var ray = new Ray
            {
                origin = transform.position,
                direction = transform.forward
            };
            if (Physics.Raycast(ray, out RaycastHit transHit, rayMaxDistance, wallLayerMask))
            {
                var angle = Vector3.Angle(Vector3.up, transHit.normal);
                Debug.Log(transHit);
                if (angle >= 45f)
                {
                    TransitionToWall(transHit.point, transHit.normal);
                    Debug.Log(angle);
                }
                else
                {
                    TransitionToFloor();
                    yield break;
                }
            }

            // 손과 발의 초기 위치 및 법선 설정
            _rightHandPos = SimpleRaycast(rightUpperArm.position, transform.forward, rayMaxDistance, out _rightHandNormal);
            _leftHandPos = SimpleRaycast(leftUpperArm.position, transform.forward, rayMaxDistance, out _leftHandNormal);
            _rightFootPos = SimpleRaycast(rightCalf.position, transform.forward, rayMaxDistance, out _rightFootNormal);
            _leftFootPos = SimpleRaycast(leftCalf.position, transform.forward, rayMaxDistance, out _leftFootNormal);

            while (stateMachine == StateMachine.Wall)
            {
                Transform arm;
                Transform calf;
                if (_isRightTurn)
                {
                    arm = rightUpperArm;
                    calf = leftCalf;
                }
                else
                {
                    arm = leftUpperArm;
                    calf = rightCalf;
                }

                if (arm == null || calf == null)
                {
                    yield return null;
                    continue;
                }

                RaycastHit hit;

                var armLowerBound = (transform.forward - transform.up).normalized;
                var armUpperBound = transform.up;
                RaycastHit lastHit = default;
                bool anyHit = false;

                for (var i = 0; i < searchCount; i++)
                {
                    var midDir = (armLowerBound + armUpperBound).normalized;
                    var query = new Ray
                    {
                        origin = arm.position,
                        direction = midDir,
                    };
                    if (Physics.Raycast(query, out hit, armLength, wallLayerMask))
                    {
                        armLowerBound = midDir;
                        lastHit = hit;
                        anyHit = true;
                    }
                    else
                    {
                        armUpperBound = midDir;
                    }
                }

                if (anyHit)
                {
                    if (_isRightTurn)
                    {
                        _rightHandPos = lastHit.point;
                        _rightHandNormal = lastHit.normal;
                    }
                    else
                    {
                        _leftHandPos = lastHit.point;
                        _leftHandNormal = lastHit.normal;
                    }
                }

                var calfRay = new Ray
                {
                    origin = calf.position,
                    direction = transform.forward
                };
                Debug.DrawRay(calfRay.origin, calfRay.direction * rayMaxDistance, Color.red);
                if (Physics.Raycast(calfRay, out hit, rayMaxDistance, wallLayerMask))
                {
                    if (_isRightTurn)
                    {
                        _leftFootPos = hit.point;
                        _leftFootNormal = hit.normal;
                    }
                    else
                    {
                        _rightFootPos = hit.point;
                        _rightFootNormal = hit.normal;
                    }
                }

                // 1. 손발 ray들의 hit normal 평균 계산
                Vector3 avgNormal = Vector3.zero;
                int normalCount = 0;
                if (_rightHandNormal.sqrMagnitude > 0.001f) { avgNormal += _rightHandNormal; normalCount++; }
                if (_leftHandNormal.sqrMagnitude > 0.001f) { avgNormal += _leftHandNormal; normalCount++; }
                if (_rightFootNormal.sqrMagnitude > 0.001f) { avgNormal += _rightFootNormal; normalCount++; }
                if (_leftFootNormal.sqrMagnitude > 0.001f) { avgNormal += _leftFootNormal; normalCount++; }

                if (normalCount > 0)
                {
                    _surfaceNormal = (avgNormal / normalCount).normalized;
                }

                // surface normal이 45도 미만(평지/완만한 경사)이면 Floor로 복귀
                if (Vector3.Angle(Vector3.up, _surfaceNormal) < 23f)
                {
                    TransitionToFloor();
                    yield break;
                }

                // 2. 손발 ray 평균 법선을 바탕으로 wallUp 결정
                var wallUp = Vector3.ProjectOnPlane(Vector3.up, _surfaceNormal).normalized;
                if (wallUp.sqrMagnitude < 0.001f)
                {
                    wallUp = transform.up;
                }

                float upGoingDistance;
                if (anyHit)
                {
                    var reachUp = Vector3.Dot(lastHit.point - arm.position, wallUp);
                    upGoingDistance = Mathf.Max(0f, reachUp * upGoingDistanceDivider);
                }
                else
                {
                    upGoingDistance = 0f;
                }

                // 위치 이동 보간
                float duration = Mathf.Max(wallClimbDuration, 0.01f);
                float counter = 0f;
                while (counter < duration && stateMachine == StateMachine.Wall)
                {
                    float dt = Time.deltaTime;
                    counter += dt;

                    // 캐릭터 위치 이동
                    transform.position += wallUp * (upGoingDistance * (dt / duration));

                    // 이동 중 벽면과의 거리 실시간 보정 (몸체 로테이팅 법선은 손발 평균 _surfaceNormal 유지)
                    if (Physics.Raycast(transform.position, -_surfaceNormal, out var moveHit,
                            rayMaxDistance,
                            wallLayerMask))
                    {
                        var currentDistanceToWall = Vector3.Dot(transform.position - moveHit.point, _surfaceNormal);
                        var distanceError = wallDistance - currentDistanceToWall;
                        transform.position += _surfaceNormal * distanceError;
                    }

                    yield return null;
                }

                _isRightTurn = !_isRightTurn;
            }
        }
        finally
        {
            _wallClimbCoroutine = null;
        }
    }


    // 현재 보간된 IK 위치를 기억할 변수 추가
    public Vector3 _currentRightHandPos ;
    public Vector3 _currentLeftHandPos ;
    public Vector3 _currentRightFootPos ;
    public Vector3 _currentLeftFootPos ;

    //private void OnAnimatorIK(int layerIndex)
    //{
    //    Debug.Log("IK 호출");

    //    if (animator == null) return;
    //    if (layerIndex != 0) return; // 베이스 레이어에서만 실행 (다중 레이어 중복 호출 방지)

    //    float currentHandWeight = (stateMachine == StateMachine.Wall) ? handIKWeight : 0f;
    //    float currentFootWeight = (stateMachine == StateMachine.Wall) ? footIKWeight : 0f;

    //    // 목표 위치가 아직 잡히지 않은 경우(Vector3.zero) 애니메이션 기본 위치로 초기화
    //    if (_currentRightHandPos == Vector3.zero) _currentRightHandPos = animator.GetIKPosition(AvatarIKGoal.RightHand);
    //    if (_currentLeftHandPos == Vector3.zero) _currentLeftHandPos = animator.GetIKPosition(AvatarIKGoal.LeftHand);
    //    if (_currentRightFootPos == Vector3.zero) _currentRightFootPos = animator.GetIKPosition(AvatarIKGoal.RightFoot);
    //    if (_currentLeftFootPos == Vector3.zero) _currentLeftFootPos = animator.GetIKPosition(AvatarIKGoal.LeftFoot);

    //    // --- 오른손 ---
    //    IKSet(AvatarIKGoal.RightHand, currentHandWeight, _rightHandPos, ref _currentRightHandPos);

    //    // --- 왼손 ---
    //    IKSet(AvatarIKGoal.LeftHand, currentHandWeight, _leftHandPos, ref _currentLeftHandPos);

    //    // --- 오른발 ---
    //    IKSet(AvatarIKGoal.RightFoot, currentFootWeight, _rightFootPos, ref _currentRightFootPos);

    //    // --- 왼발 ---
    //    IKSet(AvatarIKGoal.LeftFoot, currentFootWeight, _leftFootPos, ref _currentLeftFootPos);
    //}

    //private void IKSet(AvatarIKGoal goal, float weight, Vector3 targetPos, ref Vector3 currentSmoothedPos)
    //{
    //    animator.SetIKPositionWeight(goal, weight);

    //    if (weight > 0.001f && targetPos != Vector3.zero)
    //    {
    //        // 이전 프레임의 보간 위치에서 목표 위치로 서서히 이동
    //        currentSmoothedPos = Vector3.Lerp(currentSmoothedPos, targetPos, tweenConst);
    //        animator.SetIKPosition(goal, currentSmoothedPos);
    //    }
    //    else
    //    {
    //        // IK가 꺼져있을 때는 기본 애니메이션 위치 동기화
    //        currentSmoothedPos = animator.GetIKPosition(goal);
    //    }
    //}


    public void OnLook(InputAction.CallbackContext context)
    {
        mouseDelta = context.ReadValue<Vector2>() * mouseSensitivity;
        float mouseX = mouseDelta.x;
        float mouseY = mouseDelta.y;

        // 1. Pitch 누적 및 클램핑 (공통)
        _pitch = Mathf.Clamp(_pitch - mouseY, minPitch, maxPitch);

        // 2. 상태별 Yaw 처리
        if (stateMachine == StateMachine.Floor)
        {
            // 지상: 월드 360도 자유 회전
            _yaw += mouseX;
        }
        else
        {
            // 벽 등반: 벽면 기준 둘러보기 범위 제한
            _yaw = Mathf.Clamp(_yaw + mouseX, -wallYawLimit, wallYawLimit);
        }
    }

    public void OnScroll(InputAction.CallbackContext context)
    {
        if (cam == null) return;

        float scrollValue = context.ReadValue<float>();
        _currentCamDistance += scrollValue * scrollSensitivity;
        _currentCamDistance = Mathf.Clamp(_currentCamDistance, minDistance, maxDistance);

        // 왜곡 없는 로컬 Z축 줌인/줌아웃
        cam.transform.localPosition = new Vector3(0f, 0f, _currentCamDistance);
    }
}
