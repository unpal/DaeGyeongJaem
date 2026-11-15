using System;
using UnityEngine;

// Geometry only: the caller owns role, round and network-authority checks.
public sealed class SeekerCatchQuery
{
    private Collider[] overlaps = new Collider[32];
    private Collider[] originOverlaps = new Collider[8];
    private RaycastHit[] hits = new RaycastHit[16];

    public Transform FindTarget(PhysicsScene scene, Vector3 origin, Vector3 direction,
        float range, float halfAngle, Transform seeker, Func<Collider, Transform> resolveTarget)
    {
        float magnitude = direction.sqrMagnitude;
        if (!scene.IsValid() || !IsFinite(origin) || !IsFinite(direction) || magnitude < 0.01f ||
            float.IsInfinity(magnitude) || !IsFinite(range) || range <= 0f || !IsFinite(halfAngle))
            return null;

        direction.Normalize();
        float minimumDot = Mathf.Cos(Mathf.Clamp(halfAngle, 0f, 89f) * Mathf.Deg2Rad);
        int count = Overlap(scene, origin, range, ref overlaps);
        Transform closest = null;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider candidate = overlaps[i];
            if (IsChildOf(candidate.transform, seeker)) continue;
            Transform target = resolveTarget(candidate);
            if (target == null || target == seeker) continue;

            Vector3 point = candidate.ClosestPoint(origin);
            Vector3 offset = point - origin;
            float distance = offset.magnitude;
            if (distance > range || distance >= closestDistance) continue;
            if (distance > 0.001f && Vector3.Dot(direction, offset / distance) < minimumDot)
                continue;
            if (!HasLineOfSight(scene, origin, point, seeker, target)) continue;

            closest = target;
            closestDistance = distance;
        }
        return closest;
    }

    private bool HasLineOfSight(PhysicsScene scene, Vector3 origin, Vector3 point,
        Transform seeker, Transform target)
    {
        // Raycasts do not report a wall when the ray begins inside it.
        int overlapping = Overlap(scene, origin, 0.005f, ref originOverlaps);
        for (int i = 0; i < overlapping; i++)
        {
            Transform obstacle = originOverlaps[i].transform;
            if (!IsChildOf(obstacle, seeker) && !IsChildOf(obstacle, target)) return false;
        }

        Vector3 offset = point - origin;
        float distance = offset.magnitude;
        if (distance < 0.001f) return true;
        int count;
        while (true)
        {
            count = scene.Raycast(origin, offset / distance, hits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count < hits.Length) break;
            Array.Resize(ref hits, hits.Length * 2);
        }
        for (int i = 0; i < count; i++)
        {
            Transform obstacle = hits[i].collider.transform;
            if (!IsChildOf(obstacle, seeker) && !IsChildOf(obstacle, target)) return false;
        }
        return true;
    }

    private static int Overlap(PhysicsScene scene, Vector3 origin, float radius, ref Collider[] buffer)
    {
        while (true)
        {
            int count = scene.OverlapSphere(origin, radius, buffer,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count < buffer.Length) return count;
            Array.Resize(ref buffer, buffer.Length * 2);
        }
    }

    private static bool IsChildOf(Transform child, Transform root) =>
        root != null && child.IsChildOf(root);

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
}
