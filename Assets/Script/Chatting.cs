using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using UnityEngine.UI;
using UnityEngine.UIElements;
using TMPro;
public class Chatting : NetworkBehaviour
{
    [SerializeField] private Canvas ChattingCanvas;
    [SerializeField] private TMP_InputField ChattingInput;
    [SerializeField] private GameObject ChattingContainer;
    [SerializeField] private ScrollView ChattingScrollview;//123
    [SerializeField] private TextMeshProUGUI ChattingTMP;
    public override void Spawned()
    {
    }

    // Update is called once per frame
    public override void FixedUpdateNetwork()
    {

    }

    public void OnButtonChatting()
    {
        if (ChattingInput == null)
            return;

        RpcSendChatting(ChattingInput.text);

        ChattingInput.text = null;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcSendChatting(string Chat)
    {
        string PlayerName = $"Player {Object.InputAuthority.PlayerId} : ";
        string SumChat = PlayerName + Chat;
        ChattingTMP.text = SumChat;
        Instantiate(ChattingTMP, ChattingContainer.transform,false);
    }
}
