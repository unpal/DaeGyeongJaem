using UnityEngine;

public class PlayerIK : MonoBehaviour
{
    public Un Player;

    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Wall Contact")]
    [SerializeField] private float surfaceOffset = 0.025f;
    [SerializeField] private float smoothingSpeed = 12f;

    [Header("Elbow Hint")]
    [Range(0f, 1f)]
    [SerializeField] private float elbowHintWeight = 0.6f;

    [SerializeField] private float elbowOutwardDistance = 0.25f;
    [SerializeField] private float elbowBackDistance = 0.1f;

    [Header("Hand Rotation")]
    [SerializeField] private bool alignHandRotation = true;

    private Vector3 _currentRightHandPos;
    private Vector3 _currentLeftHandPos;
    private Vector3 _currentRightFootPos;
    private Vector3 _currentLeftFootPos;

    private Quaternion _currentRightHandRot;
    private Quaternion _currentLeftHandRot;

    private Transform _leftElbow;
    private Transform _rightElbow;
    private bool _initialized;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator != null && animator.isHuman)
        {
            _leftElbow =
                animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);

            _rightElbow =
                animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || Player == null) return;
        if (layerIndex != 0) return;

        if (!_initialized)
            InitializeIK();

        bool isClimbing =
            Player.statemachine == Un.StateMachine.Wall;

        float handWeight =
            isClimbing ? Player.handikweight : 0f;

        float footWeight =
            isClimbing ? Player.footikweight : 0f;

        float lerpFactor =
            1f - Mathf.Exp(-smoothingSpeed * Time.deltaTime);

        // 손과 발의 위치 IK
        UpdatePosition(
            AvatarIKGoal.RightHand,
            handWeight,
            Player._rightHandPos,
            Player._rightHandNormal,
            ref _currentRightHandPos,
            lerpFactor
        );

        UpdatePosition(
            AvatarIKGoal.LeftHand,
            handWeight,
            Player._leftHandPos,
            Player._leftHandNormal,
            ref _currentLeftHandPos,
            lerpFactor
        );

        UpdatePosition(
            AvatarIKGoal.RightFoot,
            footWeight,
            Player._rightFootPos,
            Player._rightFootNormal,
            ref _currentRightFootPos,
            lerpFactor
        );

        UpdatePosition(
            AvatarIKGoal.LeftFoot,
            footWeight,
            Player._leftFootPos,
            Player._leftFootNormal,
            ref _currentLeftFootPos,
            lerpFactor
        );

        // 팔꿈치가 자연스럽게 굽는 방향 유도
        UpdateElbowHints(isClimbing);

        // 손 회전은 기본적으로 비활성화
        UpdateHandRotation(
            AvatarIKGoal.RightHand,
            handWeight,
            Player._rightHandNormal,
            ref _currentRightHandRot,
            lerpFactor
        );

        UpdateHandRotation(
            AvatarIKGoal.LeftHand,
            handWeight,
            Player._leftHandNormal,
            ref _currentLeftHandRot,
            lerpFactor
        );
    }

    private void InitializeIK()
    {
        _currentRightHandPos =
            animator.GetIKPosition(AvatarIKGoal.RightHand);

        _currentLeftHandPos =
            animator.GetIKPosition(AvatarIKGoal.LeftHand);

        _currentRightFootPos =
            animator.GetIKPosition(AvatarIKGoal.RightFoot);

        _currentLeftFootPos =
            animator.GetIKPosition(AvatarIKGoal.LeftFoot);

        _currentRightHandRot =
            animator.GetIKRotation(AvatarIKGoal.RightHand);

        _currentLeftHandRot =
            animator.GetIKRotation(AvatarIKGoal.LeftHand);

        _initialized = true;
    }

    private void UpdatePosition(
        AvatarIKGoal goal,
        float weight,
        Vector3 surfacePoint,
        Vector3 surfaceNormal,
        ref Vector3 currentPosition,
        float lerpFactor)
    {
        animator.SetIKPositionWeight(goal, weight);

        bool hasTarget =
            surfacePoint.sqrMagnitude > 0.0001f;

        if (weight > 0.001f && hasTarget)
        {
            Vector3 targetPosition = surfacePoint;

            // 벽에서 살짝 떨어진 위치에 손과 발을 둔다.
            if (surfaceNormal.sqrMagnitude > 0.0001f)
            {
                targetPosition +=
                    surfaceNormal.normalized * surfaceOffset;
            }

            currentPosition = Vector3.Lerp(
                currentPosition,
                targetPosition,
                lerpFactor
            );

            animator.SetIKPosition(goal, currentPosition);
        }
        else
        {
            currentPosition = animator.GetIKPosition(goal);
        }
    }

    private void UpdateElbowHints(bool isClimbing)
    {
        float weight = isClimbing ? elbowHintWeight : 0f;

        animator.SetIKHintPositionWeight(
            AvatarIKHint.LeftElbow, weight);

        animator.SetIKHintPositionWeight(
            AvatarIKHint.RightElbow, weight);

        if (!isClimbing || Player == null)
            return;

        Transform root = Player.transform;

        // 왼쪽 팔꿈치: 캐릭터의 왼쪽 바깥 방향
        if (_leftElbow != null)
        {
            Vector3 hintPosition =
                _leftElbow.position
                - root.right * elbowOutwardDistance
                - root.forward * elbowBackDistance;

            animator.SetIKHintPosition(
                AvatarIKHint.LeftElbow,
                hintPosition
            );
        }

        // 오른쪽 팔꿈치: 캐릭터의 오른쪽 바깥 방향
        if (_rightElbow != null)
        {
            Vector3 hintPosition =
                _rightElbow.position
                + root.right * elbowOutwardDistance
                - root.forward * elbowBackDistance;

            animator.SetIKHintPosition(
                AvatarIKHint.RightElbow,
                hintPosition
            );
        }
    }

    private void UpdateHandRotation(
        AvatarIKGoal goal,
        float weight,
        Vector3 surfaceNormal,
        ref Quaternion currentRotation,
        float lerpFactor)
    {
        if (!alignHandRotation ||
            weight <= 0.001f ||
            surfaceNormal.sqrMagnitude < 0.0001f)
        {
            animator.SetIKRotationWeight(goal, 0f);
            currentRotation = animator.GetIKRotation(goal);
            return;
        }

        // Un의 레이캐스트에서 가져온 벽 표면의 법선
        Vector3 wallNormal = surfaceNormal.normalized;

        // 벽의 방향이 바뀌어도 손가락이 위쪽을 향하도록 기준 설정
        Vector3 wallUp =
            Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;

        // 천장처럼 월드 위쪽과 평행한 면을 위한 예외 처리
        if (wallUp.sqrMagnitude < 0.0001f)
        {
            wallUp = Vector3.ProjectOnPlane(
                Player.transform.forward,
                wallNormal
            ).normalized;
        }

        if (wallUp.sqrMagnitude < 0.0001f)
        {
            animator.SetIKRotationWeight(goal, 0f);
            return;
        }

        // 벽의 법선에 맞춰 손 회전을 계산
        Quaternion targetRotation =
            Quaternion.LookRotation(-wallNormal, wallUp);

        // 급격한 회전 대신 부드럽게 회전
        currentRotation = Quaternion.Slerp(
            currentRotation,
            targetRotation,
            lerpFactor
        );

        animator.SetIKRotationWeight(goal, weight);
        animator.SetIKRotation(goal, currentRotation);
    }
}