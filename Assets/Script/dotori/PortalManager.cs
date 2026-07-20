using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * 특정 개수의 오브젝트(포탈)을 활성화시키는 코드입니다
 * 게임매니저 등의 외부에서 호출해야 합니다
 */
public class PortalManager : NetworkBehaviour
{
    public List<GameObject> portals = new List<GameObject>();

    public void ActivateRandomPortals(int n)
    {
        if (portals == null || portals.Count == 0) return;
        //포탈 개수 2개이상이면 버그남 이거 고쳐야됨
        //List<GameObject> availablePortals = new List<GameObject>();
        //foreach (var portal in portals)
        //{
        //    if (portal != null)
        //        availablePortals.Add(portal);
        //}

        int count = Mathf.Min(n, portals.Count);
        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, portals.Count);
            RpcPortalActive(randomIndex);
        }
    }
    [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
    public void RpcPortalActive(int i)
    {
        portals[i].SetActive(true);
    }
    [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
    public void RpcDeactivateAllPortals()
    {
        if (portals == null)
            return;

        foreach (GameObject portal in portals)
        {
            if (portal != null)
                portal.SetActive(false);
        }
    }
}
