using System;
using UnityEngine;

public static class PrototypeItemAim
{
    public static bool Raycast(PhysicsScene scene, Vector3 origin, Vector3 direction, float range,
        Transform owner, out RaycastHit nearest)
    {
        nearest = default;
        float magnitude = direction.sqrMagnitude;
        if (float.IsNaN(magnitude) || float.IsInfinity(magnitude) || magnitude < 0.01f) return false;
        RaycastHit[] hits = new RaycastHit[16];
        int count;
        while (true)
        {
            count = scene.Raycast(origin, direction.normalized, hits, range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count < hits.Length) break;
            Array.Resize(ref hits, hits.Length * 2);
        }
        float distance = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (owner != null && hits[i].transform.IsChildOf(owner)) continue;
            if (hits[i].distance >= distance) continue;
            nearest = hits[i]; distance = hits[i].distance; found = true;
        }
        return found;
    }
}
