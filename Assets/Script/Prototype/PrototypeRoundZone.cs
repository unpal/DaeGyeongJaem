using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrototypeRoundZone : MonoBehaviour
{
    public enum PrototypeZoneType { Escape, Eliminate }

    private PrototypeRoundManager manager;
    public PrototypeZoneType zoneType;

    public void Initialize(PrototypeRoundManager roundManager, PrototypeZoneType type)
    {
        manager = roundManager;
        zoneType = type;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (manager == null || manager.Object == null || !manager.Object.HasStateAuthority)
            return;

        PlayerGameState player = other.GetComponentInParent<PlayerGameState>();
        if (player == null)
            return;

        if (zoneType == PrototypeZoneType.Escape)
            manager.ReportPlayerEscaped(player);
        else
            manager.ReportPlayerEliminated(player);
    }
}
