using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortalEscapeTrigger : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;

    //gameManager가 null 일수도 있음 > 테스트해보기. 서버에서만 호출되고, 외부적으로 호스트인지 검사하지만 그래도. 혹시모르남.

    //들어가면
    private void OnTriggerEnter(Collider other)
    {
        //getcomponent pgs
        PlayerGameState player =
            other.GetComponentInParent<PlayerGameState>();

        //탈출 동시에 여러번 하면 안됨
        if (player == null || !player.IsInPlayground)
            return;

        
        gameManager.ReportPlayerEscaped(player);
    }
}
