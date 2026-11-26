using UnityEngine;

// Scene-authored spawn markers, shared by lobby joins and round resets.
public sealed class PrototypeSpawnLayout : MonoBehaviour
{
    public Transform[] playerSpawns;
    public static Vector3 Get(int index)
    {
        var layout = FindFirstObjectByType<PrototypeSpawnLayout>();
        if (layout != null && layout.playerSpawns != null && index >= 0 && index < layout.playerSpawns.Length &&
            layout.playerSpawns[index] != null) return layout.playerSpawns[index].position;
        return PrototypeSpawnPoints.Get(index);
    }

    private void OnDrawGizmos()
    {
        if (playerSpawns == null) return;
        Gizmos.color = Color.cyan;
        foreach (Transform point in playerSpawns)
            if (point != null) Gizmos.DrawWireSphere(point.position, 0.5f);
    }
}
