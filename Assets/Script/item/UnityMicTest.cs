
using UnityEngine;

public class UnityMicTest : MonoBehaviour
{
    private AudioClip micClip;
    private string micDevice;

    private const int SampleRate = 48000;
    private const int SampleCount = 1024;

    private float[] samples = new float[SampleCount];
    private float timer;

    void Start()
    {
        Debug.Log("===== Unity Microphone Test =====");

        foreach (string device in Microphone.devices)
        {
            Debug.Log("감지된 마이크: " + device);
        }

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("Unity에서 마이크를 찾지 못했습니다.");
            return;
        }

        // USB 마이크 우선 선택
        micDevice = null;

        foreach (string device in Microphone.devices)
        {
            if (device.Contains("USB audio CODEC"))
            {
                micDevice = device;
                break;
            }
        }

        // USB 마이크를 찾지 못하면 첫 번째 장치 사용
        if (string.IsNullOrEmpty(micDevice))
            micDevice = Microphone.devices[0];

        Debug.Log("선택한 마이크: " + micDevice);

        micClip = Microphone.Start(
            micDevice,
            true,
            1,
            SampleRate
        );
    }

    void Update()
    {
        if (micClip == null) return;

        timer += Time.deltaTime;
        if (timer < 0.5f) return;
        timer = 0f;

        int position = Microphone.GetPosition(micDevice);

        if (position < SampleCount)
        {
            Debug.Log("마이크 초기화 대기 중: " + position);
            return;
        }

        int offset = position - SampleCount;

        if (!micClip.GetData(samples, offset))
        {
            Debug.LogWarning("마이크 데이터 읽기 실패");
            return;
        }

        float sum = 0f;

        foreach (float sample in samples)
            sum += sample * sample;

        float rms = Mathf.Sqrt(sum / samples.Length);

        Debug.Log(
            $"Unity 마이크 입력 RMS: {rms:F6}"
        );
    }

    void OnDestroy()
    {
        if (!string.IsNullOrEmpty(micDevice) &&
            Microphone.IsRecording(micDevice))
        {
            Microphone.End(micDevice);
        }
    }
}
