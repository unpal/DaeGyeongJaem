
using Photon.Voice;
using Photon.Voice.Unity;
using UnityEngine;
using static Photon.Voice.Unity.Recorder;

[RequireComponent(typeof(Recorder))]
public class VoiceMicrophoneSetup : MonoBehaviour
{
    private Recorder recorder;

    private void Awake()
    {
        recorder = GetComponent<Recorder>();

        // Unity 마이크 API 사용
        recorder.MicrophoneType = MicType.Unity;

        // 가상 마이크 지정
        string deviceName = "CABLE Output (VB-Audio Virtual Cable)";

        recorder.MicrophoneDevice = new DeviceInfo(deviceName);

        Debug.Log("[Voice] Selected Microphone: " + deviceName);

        recorder.RecordingEnabled = false;
        recorder.MicrophoneType = Recorder.MicType.Unity;
        recorder.RecordingEnabled = true;
    }
}
