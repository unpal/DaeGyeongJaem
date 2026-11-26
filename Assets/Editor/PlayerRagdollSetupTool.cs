using UnityEditor;
using UnityEngine;

/// <summary>
/// Player.prefab의 활성 Humanoid 본(Char_Mesh_Face_1)에
/// 스케일(lossyScale)이 보정된 래그돌 Rigidbody/Collider/CharacterJoint를 사전 구성하는 에디터 도구.
/// </summary>
public static class PlayerRagdollSetupTool
{
    private const string PlayerPrefabPath = "Assets/Prefab/Player.prefab";

    [MenuItem("Tools/DaeGyeongJaem/Setup Player Ragdoll")]
    public static void SetupPlayerRagdoll()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"[PlayerRagdollSetupTool] 프리팹을 로드할 수 없습니다: {PlayerPrefabPath}");
            return;
        }

        try
        {
            PlayerRagdoll ragdoll = prefabRoot.GetComponent<PlayerRagdoll>();
            if (ragdoll == null)
                ragdoll = prefabRoot.AddComponent<PlayerRagdoll>();

            ragdoll.ResolveTargetRigReferences();
            Animator animator = ragdoll.TargetAnimator;
            if (animator == null || !animator.isHuman)
            {
                Debug.LogError("[PlayerRagdollSetupTool] 활성 Humanoid Animator를 찾지 못했습니다.");
                return;
            }

            ClearExistingRigPhysics(animator.transform);

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            Transform torso = chest != null ? chest : spine;
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

            Transform leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);

            Transform rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            Transform rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);

            Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

            if (hips == null || head == null || torso == null)
            {
                Debug.LogError("[PlayerRagdollSetupTool] 필수 Humanoid 본(Hips/Spine/Head)을 찾지 못했습니다.");
                return;
            }

            // 1. Pelvis (Hips)
            Rigidbody hipsRb = SetupBoneRigidbody(hips, 14f);
            AddWorldBoxCollider(hips, new Vector3(0.32f, 0.22f, 0.24f), new Vector3(0f, 0.05f, 0f));

            // 2. Torso (Chest or Spine)
            Rigidbody torsoRb = SetupBoneRigidbody(torso, 18f);
            AddWorldBoxCollider(torso, new Vector3(0.34f, 0.36f, 0.24f), new Vector3(0f, 0.12f, 0f));
            SetupCharacterJoint(torso, hipsRb, -20f, 20f, 20f, 15f);

            // 3. Head
            SetupBoneRigidbody(head, 6f);
            AddWorldSphereCollider(head, 0.14f, new Vector3(0f, 0.12f, 0f));
            SetupCharacterJoint(head, torsoRb, -35f, 25f, 30f, 20f);

            // 4. Legs
            SetupLimbSegment(leftUpperLeg, leftLowerLeg, hipsRb, 7f, 0.09f, -65f, 45f, 25f, 15f);
            SetupLimbSegment(leftLowerLeg, leftFoot, GetRb(leftUpperLeg), 5f, 0.07f, -100f, 5f, 10f, 10f);

            SetupLimbSegment(rightUpperLeg, rightLowerLeg, hipsRb, 7f, 0.09f, -65f, 45f, 25f, 15f);
            SetupLimbSegment(rightLowerLeg, rightFoot, GetRb(rightUpperLeg), 5f, 0.07f, -100f, 5f, 10f, 10f);

            // 5. Arms
            SetupLimbSegment(leftUpperArm, leftLowerArm, torsoRb, 4f, 0.065f, -70f, 70f, 45f, 25f);
            SetupLimbSegment(leftLowerArm, leftHand, GetRb(leftUpperArm), 3f, 0.055f, -95f, 10f, 15f, 15f);

            SetupLimbSegment(rightUpperArm, rightLowerArm, torsoRb, 4f, 0.065f, -70f, 70f, 45f, 25f);
            SetupLimbSegment(rightLowerArm, rightHand, GetRb(rightUpperArm), 3f, 0.055f, -95f, 10f, 15f, 15f);

            ragdoll.CollectRagdollBones();
            EditorUtility.SetDirty(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            Debug.Log("[PlayerRagdollSetupTool] Player.prefab 래그돌 본 구성 및 저장을 완료했습니다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [MenuItem("Tools/DaeGyeongJaem/Clear Player Ragdoll")]
    public static void ClearPlayerRagdoll()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (prefabRoot == null)
            return;

        try
        {
            PlayerRagdoll ragdoll = prefabRoot.GetComponent<PlayerRagdoll>();
            if (ragdoll != null)
            {
                ragdoll.ResolveTargetRigReferences();
                if (ragdoll.TargetAnimator != null)
                    ClearExistingRigPhysics(ragdoll.TargetAnimator.transform);
                ragdoll.CollectRagdollBones();
            }

            EditorUtility.SetDirty(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            Debug.Log("[PlayerRagdollSetupTool] Player.prefab 래그돌 본 컴포넌트를 정리했습니다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ClearExistingRigPhysics(Transform root)
    {
        foreach (CharacterJoint joint in root.GetComponentsInChildren<CharacterJoint>(true))
            Object.DestroyImmediate(joint);

        foreach (Collider col in root.GetComponentsInChildren<Collider>(true))
        {
            if (col is CharacterController) continue;
            Object.DestroyImmediate(col);
        }

        foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(rb);
    }

    private static Rigidbody SetupBoneRigidbody(Transform bone, float mass)
    {
        Rigidbody rb = bone.gameObject.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.drag = 0.05f;
        rb.angularDrag = 0.4f;
        rb.isKinematic = true;
        rb.detectCollisions = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        return rb;
    }

    private static Rigidbody GetRb(Transform bone)
    {
        return bone != null ? bone.GetComponent<Rigidbody>() : null;
    }

    private static void SetupLimbSegment(
        Transform bone,
        Transform childBone,
        Rigidbody connectedBody,
        float mass,
        float worldRadius,
        float lowTwist,
        float highTwist,
        float swing1,
        float swing2)
    {
        if (bone == null || connectedBody == null)
            return;

        SetupBoneRigidbody(bone, mass);

        Vector3 localEnd = childBone != null
            ? bone.InverseTransformPoint(childBone.position)
            : new Vector3(0f, 0.3f / Mathf.Max(GetAverageLossyScale(bone), 0.001f), 0f);

        int axis = GetDominantAxis(localEnd);
        float localLength = Mathf.Max(localEnd.magnitude, 0.01f);
        float perpScale = GetPerpendicularScale(bone, axis);

        CapsuleCollider cap = bone.gameObject.AddComponent<CapsuleCollider>();
        cap.direction = axis;
        cap.center = localEnd * 0.5f;
        cap.radius = worldRadius / Mathf.Max(perpScale, 0.0001f);
        cap.height = Mathf.Max(localLength, cap.radius * 2f);
        cap.enabled = false;

        SetupCharacterJoint(bone, connectedBody, lowTwist, highTwist, swing1, swing2);
    }

    private static void AddWorldBoxCollider(Transform bone, Vector3 worldSize, Vector3 worldOffset)
    {
        Vector3 lossy = AbsScale(bone.lossyScale);
        BoxCollider box = bone.gameObject.AddComponent<BoxCollider>();
        box.center = bone.InverseTransformPoint(bone.position + worldOffset);
        box.size = new Vector3(
            worldSize.x / Mathf.Max(lossy.x, 0.0001f),
            worldSize.y / Mathf.Max(lossy.y, 0.0001f),
            worldSize.z / Mathf.Max(lossy.z, 0.0001f));
        box.enabled = false;
    }

    private static void AddWorldSphereCollider(Transform bone, float worldRadius, Vector3 worldOffset)
    {
        float avgScale = GetAverageLossyScale(bone);
        SphereCollider sphere = bone.gameObject.AddComponent<SphereCollider>();
        sphere.center = bone.InverseTransformPoint(bone.position + worldOffset);
        sphere.radius = worldRadius / Mathf.Max(avgScale, 0.0001f);
        sphere.enabled = false;
    }

    private static void SetupCharacterJoint(
        Transform bone,
        Rigidbody connectedBody,
        float lowTwist,
        float highTwist,
        float swing1,
        float swing2)
    {
        if (connectedBody == null)
            return;

        CharacterJoint joint = bone.gameObject.AddComponent<CharacterJoint>();
        joint.connectedBody = connectedBody;
        joint.enableProjection = true;
        joint.enableCollision = false;

        joint.lowTwistLimit = new SoftJointLimit { limit = lowTwist };
        joint.highTwistLimit = new SoftJointLimit { limit = highTwist };
        joint.swing1Limit = new SoftJointLimit { limit = swing1 };
        joint.swing2Limit = new SoftJointLimit { limit = swing2 };
    }

    private static int GetDominantAxis(Vector3 v)
    {
        float ax = Mathf.Abs(v.x);
        float ay = Mathf.Abs(v.y);
        float az = Mathf.Abs(v.z);
        if (ax >= ay && ax >= az) return 0;
        if (ay >= ax && ay >= az) return 1;
        return 2;
    }

    private static float GetPerpendicularScale(Transform bone, int axis)
    {
        Vector3 s = AbsScale(bone.lossyScale);
        return axis switch
        {
            0 => (s.y + s.z) * 0.5f,
            1 => (s.x + s.z) * 0.5f,
            _ => (s.x + s.y) * 0.5f
        };
    }

    private static float GetAverageLossyScale(Transform bone)
    {
        Vector3 s = AbsScale(bone.lossyScale);
        return (s.x + s.y + s.z) / 3f;
    }

    private static Vector3 AbsScale(Vector3 s)
    {
        return new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }
}
