
using UnityEngine;
using Fusion;
using Photon.Voice.Fusion;

public class VoiceConnectionTest : MonoBehaviour
{
    [SerializeField] private NetworkRunner runner;

    private FusionVoiceClient voice;
    private string previousState = "";

    private void Update()
    {
        if (runner == null)
            return;

        if (voice == null)
            voice = runner.GetComponent<FusionVoiceClient>();

        if (voice == null)
            return;

        string state = voice.ClientState.ToString();

        if (previousState != state)
        {
            previousState = state;

            Debug.Log(
                $"[Voice 상태 변경]\n" +
                $"State: {state}\n" +
                $"Connected: {voice.Client.IsConnected}\n" +
                $"InRoom: {voice.Client.InRoom}\n" +
                $"Runner Running: {runner.IsRunning}\n" +
                $"Session Valid: {runner.SessionInfo.IsValid}"
            );
        }

        if (Input.GetKeyDown(KeyCode.F8))
        {
            Debug.Log("[Voice] 수동 연결 시도");

            bool result = voice.ConnectAndJoinRoom();

            Debug.Log(
                $"[Voice] 연결 요청 결과: {result}\n" +
                $"현재 상태: {voice.ClientState}\n" +
                $"Voice App Settings 사용: {voice.UseFusionAppSettings}\n" +
                $"Fusion 인증 사용: {voice.UseFusionAuthValues}"
            );
        }
    }
}
