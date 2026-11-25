using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 프리팹에 사전에 구성된 본 Rigidbody/Collider/Joint를 제어하여
/// 래그돌 추락 및 기상 복구를 담당하는 컴포넌트.
/// </summary>
[DisallowMultipleComponent]
public class PlayerRagdoll : MonoBehaviour
{
    [Header("Target Rig")]
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private Transform hipsBone;
    [SerializeField] private Transform headBone;
    [SerializeField] private List<Rigidbody> ragdollBodies = new();
    [SerializeField] private List<Collider> ragdollColliders = new();

    private struct BoneRestPose
    {
        public Transform Transform;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
    }

    private readonly List<BoneRestPose> _restPoses = new();
    private Rigidbody _hipsRigidbody;
    private Vector3 _savedModelLocalPos;
    private Quaternion _savedModelLocalRot = Quaternion.identity;
    private bool _hasSavedModelPose;
    private bool _warnedMissingRig;

    public bool IsRagdollActive { get; private set; }
    public bool HasValidRig => ragdollBodies.Count > 0 && hipsBone != null;
    public Animator TargetAnimator => targetAnimator;
    public Transform HipsBone => hipsBone;
    public Transform HeadBone => headBone;
    public Vector3 HipsWorldPosition => hipsBone != null ? hipsBone.position : transform.position;
    public Vector3 HipsVelocity => _hipsRigidbody != null ? _hipsRigidbody.velocity : Vector3.zero;

    private void Awake()
    {
        ResolveTargetRigReferences();
        CollectRagdollBones();
        CacheRestPoses();
        ApplyInactiveState();
    }

    public void ResolveTargetRigReferences()
    {
        if (targetAnimator == null || !targetAnimator.gameObject.activeSelf)
        {
            Animator[] animators = GetComponentsInChildren<Animator>(true);
            foreach (Animator candidate in animators)
            {
                if (candidate == null) continue;
                if (candidate.gameObject.activeSelf)
                {
                    targetAnimator = candidate;
                    break;
                }
            }

            if (targetAnimator == null && animators.Length > 0)
                targetAnimator = animators[0];
        }

        if (targetAnimator != null && targetAnimator.isHuman)
        {
            if (hipsBone == null)
                hipsBone = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (headBone == null)
                headBone = targetAnimator.GetBoneTransform(HumanBodyBones.Head);
        }
    }

    [ContextMenu("Collect Ragdoll Bones")]
    public void CollectRagdollBones()
    {
        ResolveTargetRigReferences();

        ragdollBodies.Clear();
        ragdollColliders.Clear();
        _hipsRigidbody = null;

        Transform searchRoot = targetAnimator != null ? targetAnimator.transform : transform;

        foreach (Rigidbody rb in searchRoot.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb == null || rb.gameObject == gameObject)
                continue;

            ragdollBodies.Add(rb);
        }

        foreach (Collider col in searchRoot.GetComponentsInChildren<Collider>(true))
        {
            if (col == null || col.gameObject == gameObject || col is CharacterController)
                continue;

            ragdollColliders.Add(col);
        }

        if (hipsBone != null)
            _hipsRigidbody = hipsBone.GetComponent<Rigidbody>();
        if (_hipsRigidbody == null && ragdollBodies.Count > 0)
        {
            _hipsRigidbody = ragdollBodies[0];
            if (hipsBone == null)
                hipsBone = _hipsRigidbody.transform;
        }
    }

    public void SetRagdollEnabled(bool enabled, Vector3 initialVelocity = default)
    {
        if (enabled)
        {
            if (IsRagdollActive)
                return;

            if (!HasValidRig)
            {
                ResolveTargetRigReferences();
                CollectRagdollBones();
            }

            if (!HasValidRig)
            {
                if (!_warnedMissingRig)
                {
                    _warnedMissingRig = true;
                    Debug.LogWarning("[PlayerRagdoll] 프리팹에 래그돌 본(Rigidbody/Collider)이 아직 구성되지 않았습니다. Tools/DaeGyeongJaem/Setup Player Ragdoll 또는 Ragdoll Wizard로 구성해 주세요.", this);
                }

                IsRagdollActive = true;
                return;
            }

            if (targetAnimator != null)
            {
                if (!_hasSavedModelPose)
                    CacheRestPoses();

                targetAnimator.enabled = false;
            }

            foreach (Collider col in ragdollColliders)
            {
                if (col != null)
                    col.enabled = true;
            }

            foreach (Rigidbody rb in ragdollBodies)
            {
                if (rb == null) continue;
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.velocity = initialVelocity;
                rb.angularVelocity = Vector3.zero;
                rb.WakeUp();
            }

            IsRagdollActive = true;
        }
        else
        {
            ApplyInactiveState();

            if (targetAnimator != null)
            {
                RestoreRestPoses();
                targetAnimator.enabled = true;
                targetAnimator.Update(0f);
            }

            IsRagdollActive = false;
        }
    }

    public bool IsHipsNearGround(LayerMask groundMask, float checkDistance)
    {
        Vector3 origin = HipsWorldPosition + Vector3.up * 0.15f;
        float castDistance = Mathf.Max(0.1f, checkDistance) + 0.15f;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            0.12f,
            Vector3.down,
            castDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            return true;
        }

        return false;
    }

    public bool TryGetSafeStandUpPosition(
        LayerMask groundMask,
        float probeUp,
        float probeDown,
        out Vector3 standUpPosition)
    {
        Vector3 origin = HipsWorldPosition + Vector3.up * Mathf.Max(0.1f, probeUp);
        float maxDist = Mathf.Max(0.2f, probeUp + probeDown);

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            maxDist,
            groundMask,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        RaycastHit closestHit = default;
        bool found = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                found = true;
            }
        }

        if (found)
        {
            standUpPosition = closestHit.point + Vector3.up * 0.05f;
            return true;
        }

        standUpPosition = HipsWorldPosition;
        return false;
    }

    private void ApplyInactiveState()
    {
        foreach (Rigidbody rb in ragdollBodies)
        {
            if (rb == null) continue;
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        foreach (Collider col in ragdollColliders)
        {
            if (col != null)
                col.enabled = false;
        }
    }

    private void CacheRestPoses()
    {
        _restPoses.Clear();

        if (targetAnimator != null)
        {
            _savedModelLocalPos = targetAnimator.transform.localPosition;
            _savedModelLocalRot = targetAnimator.transform.localRotation;
            _hasSavedModelPose = true;
        }

        foreach (Rigidbody rb in ragdollBodies)
        {
            if (rb == null) continue;
            _restPoses.Add(new BoneRestPose
            {
                Transform = rb.transform,
                LocalPosition = rb.transform.localPosition,
                LocalRotation = rb.transform.localRotation
            });
        }
    }

    private void RestoreRestPoses()
    {
        if (_hasSavedModelPose && targetAnimator != null)
        {
            targetAnimator.transform.localPosition = _savedModelLocalPos;
            targetAnimator.transform.localRotation = _savedModelLocalRot;
        }

        for (int i = 0; i < _restPoses.Count; i++)
        {
            BoneRestPose pose = _restPoses[i];
            if (pose.Transform == null) continue;
            pose.Transform.localPosition = pose.LocalPosition;
            pose.Transform.localRotation = pose.LocalRotation;
        }
    }
}
