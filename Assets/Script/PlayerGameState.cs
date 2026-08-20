using UnityEngine;
using Fusion;
using Script.dotori;


//이번 빌드 변경점 > 메인씬, 로비 씬, 게임 결과 화면 추가,
// 그리고 플레이어 개인별로 이름 설정 가능해짐 > 바꿔야 하는것들 > 그냥 state나 네트워크 관련 변수들 뒤져서 정보 담고있는 변수 끌어와서 덧대기.

public class PlayerGameState : NetworkBehaviour
{
    public GameObject globalVolume;
    public GameObject wbVolume;

    //음
    //player가 저장할 정보 : 죽었는지 탈출했는지> 몇번째로 탈출했는가?도 저장해야하나, 왕관, 게임안에 있는지(서버전달용) 
    [Networked]
    public int Crowns { get; private set; } 

    //추가, 이름 저장용도
    [Networked]
    public NetworkString<_32> DisplayName { get; private set; }
    public int SortNum;

    [Networked]
    public NetworkBool IsDead { get; private set; }

    [Networked]
    public NetworkBool HasEscaped { get; private set; }

    public bool IsInPlayground => !IsDead && !HasEscaped;


    public override void Spawned()
    {
        //if (!Object.HasStateAuthority)
        //    return;

        //헷갈려
        if (Object.HasStateAuthority)
        {
            DisplayName = $"Player {Object.InputAuthority.PlayerId}";
        }
        
        
        SortNum = Object.InputAuthority.PlayerId - 1;
        if (Object.HasInputAuthority && CameraManager.Instance)
        {
            CameraManager.Instance.state = this;
            VolumeManager.instance.player = this;
        }
        //추가했습니다 rpc < chatgpt..
        if (Object.HasInputAuthority && !string.IsNullOrWhiteSpace(PrototypeLobbyBootstrap.LocalPlayerName))
            RpcSetDisplayName(PrototypeLobbyBootstrap.LocalPlayerName);
    }

    //추가 
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcSetDisplayName(string playerName)
    {
        //playerName 참고.
        string trimmedName = string.IsNullOrWhiteSpace(playerName)
            ? $"Player {Object.InputAuthority.PlayerId}"
            : playerName.Trim();

        //이름 길이가 16이하이도록 함.
        DisplayName = trimmedName.Length <= 16 ? trimmedName : trimmedName.Substring(0, 16);
    }

    private bool _wasVisible = true;

    public override void Render()
    {
        bool shouldBeVisible = IsInPlayground;
        
        if (shouldBeVisible != _wasVisible)
        {
            SetVisibility(shouldBeVisible);
            _wasVisible = shouldBeVisible;
        }
    }

    private void SetVisibility(bool isVisible)
    {
        bool isLocal = Object.HasInputAuthority;

        // 모델 등 모든 렌더러 활성/비활성화
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            // 로컬 플레이어(자신)는 1인칭이므로 모델을 켜지 않음
            if (isLocal) renderer.enabled = false;
            else renderer.enabled = isVisible;
        }
        
        // 닉네임 태그 등 UI 활성/비활성화
        foreach (var canvas in GetComponentsInChildren<Canvas>(true))
        {
            // 자신의 닉네임 태그도 1인칭 화면에서는 보이지 않도록 함
            if (isLocal) canvas.enabled = false;
            else canvas.enabled = isVisible;
        }
        
        // 투명 상태일 때 충돌 방지를 위해 CharacterController도 비활성화
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = isVisible;
    }

    public void MarkDead() //죽은거 마킹
    {
        if (!Object.HasStateAuthority || HasEscaped)
            return;

        IsDead = true;
        RenderSettings.fog = false;
    }

    public void MarkEscaped() // 탈출한거 마킹
    {
        if (!Object.HasStateAuthority || IsDead)
            return;

        HasEscaped = true;
        RenderSettings.fog = false;
    }

    public void AddCrown() //왕관추가
    {
        if (!Object.HasStateAuthority)
            return;

        Crowns++;
    }

    public void ResetForNewMatch()
    {
        if (!Object.HasStateAuthority)
            return;

        Crowns = 0;
        IsDead = false;
        HasEscaped = false;
    }
    public void ResetRoundResult()
    {
        if (!Object.HasStateAuthority)
            return;

        IsDead = false;
        HasEscaped = false;
    }

}
