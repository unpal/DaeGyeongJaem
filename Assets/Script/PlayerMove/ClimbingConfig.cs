using UnityEngine;

[CreateAssetMenu(fileName = "ClimbingConfig", menuName = "DaeGyeongJaem/Climbing Config")]
public class ClimbingConfig : ScriptableObject
{
    [Header("Ground Movement")]
    [Min(0f)] public float walkSpeed = 5f;
    [Min(0f)] public float runSpeed = 8f;
    [Min(0f)] public float jumpSpeed = 6.5f;
    [Min(0f)] public float gravity = 16f;
    public float groundedStickVelocity = -2f;

    [Header("Stamina (Role-Independent)")]
    [Tooltip("매달려 있거나 등반 이동 시 초당 스태미나 소모량 (방향/각도 무관 동일)")]
    [Min(0f)] public float climbStaminaDrain = 20f;
    [Tooltip("지면 달리기 초당 스태미나 소모량")]
    [Min(0f)] public float sprintStaminaDrain = 15f;
    [Tooltip("점프 1회 스태미나 소모량")]
    [Min(0f)] public float jumpStaminaCost = 10f;
    [Tooltip("지면 정지/걷기 상태 초당 스태미나 회복량")]
    [Min(0f)] public float groundStaminaRecoverRate = 10f;

    [Header("Wall Grab & Climbing")]
    [Tooltip("벽 잡기 레이캐스트 시작 높이 (캐릭터 발 기준 m)")]
    [Min(0.1f)] public float grabRayOriginHeight = 1.45f;
    [Tooltip("벽 잡기 최대 탐지 거리 (m)")]
    [Min(0.2f)] public float grabRayDistance = 1.8f;
    [Tooltip("벽 잡기 보조 스피어캐스트 반경 (m)")]
    [Min(0.05f)] public float grabSphereRadius = 0.25f;
    [Tooltip("벽면 상하좌우 이동 속도 (m/s)")]
    [Min(0.1f)] public float climbSpeed = 3.2f;
    [Tooltip("벽 표면으로부터 유지할 몸체 거리 (m)")]
    [Min(0.1f)] public float wallStandOffDistance = 0.35f;
    [Tooltip("등반 중 벽 접촉 확인 레이 높이 (발 기준 m)")]
    [Min(0.2f)] public float wallProbeHeight = 1.0f;
    [Tooltip("등반 유지용 벽 탐지 최대 거리 (m)")]
    [Min(0.2f)] public float wallKeepContactDistance = 1.1f;
    [Tooltip("벽과 떨어지거나 너무 가까울 때 밀착 보정 강도")]
    [Min(0f)] public float wallPullStrength = 10f;
    [Tooltip("곡면/모서리 이동 시 표면 법선 보간 속도")]
    [Min(0.1f)] public float wallNormalSmoothSpeed = 12f;
    [Tooltip("벽을 마주보는 몸체 회전 보간 속도")]
    [Min(0.1f)] public float wallBodyTurnSpeed = 12f;
    [Tooltip("잡을 수 있는 표면의 최소 기울기 (위쪽 Vector3.up 기준 각도)")]
    [Range(15f, 89f)] public float minWallAngleFromUp = 40f;
    [Tooltip("잡을 수 있는 표면의 최대 기울기 (90 초과 시 오버행 허용)")]
    [Range(90f, 175f)] public float maxWallAngleFromUp = 150f;
    [Tooltip("등반 가능한 레이어 (기본: Ignore Raycast, Player 제외 모든 표면)")]
    public LayerMask climbableLayers = ~((1 << 2) | (1 << 7));
    [Tooltip("손을 놓은 직후 다시 잡기까지 최소 쿨다운 (초)")]
    [Min(0f)] public float climbRegrabCooldown = 0.2f;

    [Header("Wall Top Mantle")]
    [Tooltip("벽 꼭대기 도달 시 자동으로 위쪽 바닥에 올라설지 여부")]
    public bool enableAutoMantle = true;
    [Min(0.2f)] public float mantleRayUpOffset = 1.6f;
    [Min(0.1f)] public float mantleForwardOffset = 0.55f;
    [Min(0.2f)] public float mantleRayDownDistance = 2.4f;
    [Range(5f, 60f)] public float mantleMaxSlopeAngle = 35f;

    [Header("Ragdoll Fall & Recovery")]
    [Tooltip("직접 손을 놓은 뒤 착지 전 이 높이(m) 이상 떨어지면 공중에서 래그돌로 전환 (방안 B)")]
    [Min(0.1f)] public float manualReleaseRagdollFallDistance = 2.5f;
    [Tooltip("스태미나 0으로 손 놓침 시 벽 법선 방향으로 밀려나는 초기 속도 (m/s)")]
    [Min(0f)] public float staminaDepletedPushOffSpeed = 1.2f;
    [Tooltip("래그돌 진입 후 착지/안정화 검사를 시작하기 전 최소 유지 시간 (초)")]
    [Min(0f)] public float ragdollMinDuration = 0.45f;
    [Tooltip("착지 후 일어서기 조건으로 보는 골반 속도 임계값 (m/s)")]
    [Min(0.01f)] public float ragdollSettleSpeed = 0.35f;
    [Tooltip("지면에 닿고 속도가 안정된 뒤 기상까지 대기 시간 (초)")]
    [Min(0.05f)] public float ragdollSettleHoldTime = 0.6f;
    [Tooltip("끼임 등으로 안정화되지 못할 때 강제 기상까지 최대 시간 (초)")]
    [Min(0.5f)] public float ragdollMaxDuration = 5.0f;
    [Tooltip("골반 기준 지면 접촉 확인 거리 (m)")]
    [Min(0.1f)] public float ragdollGroundCheckDistance = 0.65f;
    [Tooltip("래그돌 착지/기상 지면 검사 레이어")]
    public LayerMask ragdollGroundLayers = ~((1 << 2) | (1 << 7));
    [Tooltip("일어난 직후 조작 안정화를 위한 짧은 대기 시간 (초)")]
    [Min(0f)] public float standUpRecoverDelay = 0.15f;

    [Header("Wall Lunge (Phase 2)")]
    [Tooltip("등반 중 위쪽 도약(밀착 대시) 1회 스태미나 소모량")]
    [Min(0f)] public float lungeStaminaCost = 25f;
    [Tooltip("등반 중 위쪽 도약(밀착 대시) 초기 상승 속도 (m/s)")]
    [Min(0.1f)] public float lungeUpwardImpulse = 10f;
    [Tooltip("등반 중 위쪽 도약(밀착 대시) 지속 시간 (초)")]
    [Min(0.05f)] public float lungeDuration = 0.22f;
    [Tooltip("도약 발동 후 다음 도약까지 최소 쿨다운 (초)")]
    [Min(0f)] public float lungeCooldown = 0.35f;

    [Header("Fall Damage")]
    [Tooltip("추락 피해를 받지 않는 최대 안전 낙하 높이 (m)")]
    [Min(0f)] public float fallDamageSafeHeight = 3f;
    [Tooltip("안전 높이를 초과한 1m당 영구 피해량")]
    [Min(0f)] public float fallDamagePerMeter = 12f;
    [Tooltip("래그돌 추락 시 낙하 피해 배율")]
    [Min(0f)] public float ragdollFallDamageMultiplier = 1f;

    public bool IsClimbableSlope(Vector3 surfaceNormal)
    {
        if (surfaceNormal.sqrMagnitude < 0.0001f)
            return false;

        float angleFromUp = Vector3.Angle(Vector3.up, surfaceNormal.normalized);
        return angleFromUp >= minWallAngleFromUp && angleFromUp <= maxWallAngleFromUp;
    }
}
