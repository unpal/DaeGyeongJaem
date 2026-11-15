
using UnityEngine;
using Photon.Voice.Unity;

public class VoiceMicTest : MonoBehaviour
{
    [SerializeField] private Recorder recorder;

    private float timer;

    void Update()
    {
        if (recorder == null) return;

        timer += Time.deltaTime;
        if (timer < 1f) return;
        timer = 0f;

        var meter = recorder.LevelMeter;

        if (meter != null)
        {
            Debug.Log(
                $"마이크 평균 음량: {meter.CurrentAvgAmp}"
            );
        }

        Debug.Log(
            $"[Voice 진단]\n" +
            $"Recording: {recorder.RecordingEnabled}\n" +
            $"Transmit: {recorder.TransmitEnabled}\n" +
            $"CurrentlyTransmitting: {recorder.IsCurrentlyTransmitting}\n" +
            $"LevelMeter Available: {meter != null}"
        );


    }
}
