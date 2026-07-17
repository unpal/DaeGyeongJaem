using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

public class PlayerGameState : NetworkBehaviour
{
    //음
    //player가 저장할 정보 : 죽었는지 탈출했는지> 몇번째로 탈출했는가?도 저장해야하나, 왕관, 게임안에 있는지(서버전달용) 
    [Networked]
    public int Crowns { get; private set; } 

    [Networked]
    public NetworkBool IsDead { get; private set; }

    [Networked]
    public NetworkBool HasEscaped { get; private set; }

    public bool IsInPlayground => !IsDead && !HasEscaped;

    public void MarkDead() //죽은거 마킹
    {
        if (!Object.HasStateAuthority || HasEscaped)
            return;

        IsDead = true;
    }

    public void MarkEscaped() // 탈출한거 마킹
    {
        if (!Object.HasStateAuthority || IsDead)
            return;

        HasEscaped = true;
    }

    public void AddCrown() //왕관추가
    {
        if (!Object.HasStateAuthority)
            return;

        Crowns++;
    }

    public void ResetForNextRound() //초기화
    {
        if (!Object.HasStateAuthority)
            return;

        IsDead = false;
        HasEscaped = false;
    }
}